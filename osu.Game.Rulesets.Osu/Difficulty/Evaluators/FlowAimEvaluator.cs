// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowAimEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = withSliderTravelDistance ? (OsuDifficultyHitObject)osuCurrObj.PreviousFlowRelevant(0) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(0);
            var osuPrev2Obj = withSliderTravelDistance ? (OsuDifficultyHitObject)osuCurrObj.PreviousFlowRelevant(1) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(1);
            var osuLast2Obj = withSliderTravelDistance ? (OsuDifficultyHitObject)osuCurrObj.PreviousFlowRelevant(2) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(2);

            if (!(withSliderTravelDistance || osuCurrObj.IsTapObject || osuCurrObj.PrevTapStrainTime is not null))
                return 0;

            if (osuPrevObj is null || osuPrev2Obj is null)
                return 0;

            double currDistanceDifference = Math.Abs(osuCurrObj.LazyJumpDistance - osuPrevObj.LazyJumpDistance);
            double prevDistanceDifference = Math.Abs(osuPrevObj.LazyJumpDistance - osuPrev2Obj.LazyJumpDistance);

            double jerk = Math.Abs(currDistanceDifference - prevDistanceDifference);

            double angleDifferenceAdjusted = Math.Sin(directionChange(osuCurrObj, osuPrevObj, osuPrev2Obj) / 2) * 180;

            double acuteBonus = 0;

            if (osuCurrObj.Angle.IsNotNull())
            {
                acuteBonus = calcAcuteAngleBonus(osuCurrObj.Angle.Value) * 4 * calculateLinearity(osuCurrObj, osuPrevObj, osuPrev2Obj);

                // Nerf the third note of bursts as its angle is not representative of its flow difficulty
                if (Math.Abs(osuCurrObj.AdjustedDeltaTime - osuPrev2Obj.AdjustedDeltaTime) > 25)
                {
                    angleDifferenceAdjusted *= DifficultyCalculationUtils.Smootherstep(osuCurrObj.Angle.Value, double.DegreesToRadians(160), double.DegreesToRadians(90));
                    jerk *= DifficultyCalculationUtils.Smootherstep(osuCurrObj.Angle.Value, double.DegreesToRadians(160), double.DegreesToRadians(90));
                }
            }

            double angularChangeBonus = Math.Max(0.0, 1.3 * Math.Log10(angleDifferenceAdjusted));

            double antiFlowBonus = Math.Min(1, jerk / 15) + Math.Max(angularChangeBonus * Math.Clamp(jerk / 30, 0.3, 1), acuteBonus);

            // Value distance exponentially
            double difficulty = Math.Pow(osuCurrObj.LazyJumpDistance + osuPrevObj.TravelDistance, 1.75) / osuCurrObj.AdjustedDeltaTime;

            difficulty += (osuCurrObj.LazyJumpDistance / osuCurrObj.AdjustedDeltaTime) * antiFlowBonus * 10;

            // Apply high circle size bonus
            if (osuCurrObj.IsTapObject)
                difficulty *= osuCurrObj.SmallCircleBonus;

            return difficulty * 0.685;
        }

        private static double directionChange(OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuPrevObj, OsuDifficultyHitObject osuPrev2Obj)
        {
            double directionChangeFactor = 0;

            if (osuCurrObj.AngleSigned.IsNull() || osuPrevObj.AngleSigned.IsNull() ||
                osuCurrObj.Angle.IsNull() || osuPrevObj.Angle.IsNull()) return directionChangeFactor;

            double signedAngleDifference = Math.Abs(osuCurrObj.AngleSigned.Value - osuPrevObj.AngleSigned.Value);

            // Account for the fact that you can aim patterns in a straight line
            signedAngleDifference *= calculateLinearity(osuCurrObj, osuPrevObj, osuPrev2Obj);

            double angleDifference = Math.Abs(osuCurrObj.Angle.Value - osuPrevObj.Angle.Value);

            directionChangeFactor += Math.Max(signedAngleDifference, angleDifference);

            return directionChangeFactor;
        }

        private static double calculateLinearity(OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuPrevObj, OsuDifficultyHitObject osuPrev2Obj)
        {
            var curBaseObj = (OsuHitObject)osuCurrObj.BaseObject;
            var prevBaseObj = (OsuHitObject)osuPrevObj.BaseObject;
            var prev2BaseObj = (OsuHitObject)osuPrev2Obj.BaseObject;

            Vector2 lineVector = prev2BaseObj.StackedEndPosition - curBaseObj.StackedEndPosition;
            Vector2 toMiddle = prevBaseObj.StackedEndPosition - curBaseObj.StackedEndPosition;

            float dotToMiddleLine = Vector2.Dot(toMiddle, lineVector);
            float dotLineLine = Vector2.Dot(lineVector, lineVector);

            float projectionScalar = dotToMiddleLine / dotLineLine;

            Vector2 projection = lineVector * projectionScalar;

            float scalingFactor = OsuDifficultyHitObject.NORMALISED_RADIUS / (float)curBaseObj.Radius;

            double perpendicularDistance = curBaseObj.StackedPosition.Equals(prev2BaseObj.StackedPosition)
                ? osuCurrObj.LazyJumpDistance
                : (toMiddle * scalingFactor - projection * scalingFactor).Length;

            return DifficultyCalculationUtils.Smootherstep(perpendicularDistance, OsuDifficultyHitObject.NORMALISED_RADIUS, OsuDifficultyHitObject.NORMALISED_RADIUS * 1.5);
        }

        private static double calcAcuteAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(140), double.DegreesToRadians(70));
    }
}
