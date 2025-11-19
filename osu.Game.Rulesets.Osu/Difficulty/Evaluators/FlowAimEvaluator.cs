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
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuPrev2Obj = (OsuDifficultyHitObject)current.Previous(1);

            double currDistanceDifference = Math.Abs(osuCurrObj.LazyJumpDistance - osuPrevObj.LazyJumpDistance);
            double prevDistanceDifference = Math.Abs(osuPrevObj.LazyJumpDistance - osuPrev2Obj.LazyJumpDistance);

            double jerk = Math.Sqrt(Math.Max(0, Math.Abs(currDistanceDifference - prevDistanceDifference) - 5) / 10);

            int i = 0;
            var loopObj = osuCurrObj;
            double curAngleChange = directionChange(current);

            double angleChangeSum = 0;

            while (i <= 6 && Math.Abs(osuCurrObj.AdjustedDeltaTime - loopObj.AdjustedDeltaTime) < 25)
            {
                loopObj = (OsuDifficultyHitObject)osuCurrObj.Previous(i);

                if (loopObj.IsNull())
                    break;

                angleChangeSum += directionChange(loopObj);
                i++;
            }

            if (osuCurrObj.Angle.IsNotNull())
            {
                if (Math.Abs(osuCurrObj.AdjustedDeltaTime - osuPrevObj.AdjustedDeltaTime) > 25)
                {
                    jerk *= 0.1;
                }

                // Nerf the third note of bursts as its angle is not representative of its flow difficulty
                if (Math.Abs(osuCurrObj.AdjustedDeltaTime - osuPrev2Obj.AdjustedDeltaTime) > 25)
                {
                    jerk *= 0.1 + calcAcuteAngleBonus(osuCurrObj.Angle.Value);
                }
            }

            double averageDirectionChange = angleChangeSum / 15;

            double velocity = (osuCurrObj.LazyJumpDistance + osuPrevObj.TravelDistance) / osuCurrObj.AdjustedDeltaTime;

            double antiFlowBonus = 0.8 + Math.Pow((jerk + curAngleChange + averageDirectionChange) / 3, 1.5);

            // Value distance exponentially
            double difficulty = velocity * antiFlowBonus;

            difficulty *= osuCurrObj.SmallCircleBonus;

            return difficulty * 1.2;
        }

        private static double directionChange(DifficultyHitObject current)
        {
            double directionChangeFactor = 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            if (osuCurrObj.AngleSigned.IsNull() || osuPrevObj.AngleSigned.IsNull() ||
                osuCurrObj.Angle.IsNull() || osuPrevObj.Angle.IsNull()) return directionChangeFactor;

            double signedAngleDifference = Math.Abs(osuCurrObj.AngleSigned.Value - osuPrevObj.AngleSigned.Value);

            // Account for the fact that you can aim patterns in a straight line
            signedAngleDifference *= calculateLinearity(osuCurrObj);

            double angleDifference = Math.Abs(osuCurrObj.Angle.Value - osuPrevObj.Angle.Value);

            directionChangeFactor += Math.Max(signedAngleDifference, angleDifference);

            double acuteBonus = calcAcuteAngleBonus(osuCurrObj.Angle.Value) * 2 * DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance, OsuDifficultyHitObject.NORMALISED_RADIUS, OsuDifficultyHitObject.NORMALISED_RADIUS * 3);

            var osuPrev2Obj = (OsuDifficultyHitObject)current.Previous(1);
            if (Math.Abs(osuCurrObj.AdjustedDeltaTime - osuPrevObj.AdjustedDeltaTime) > 25 ||
                Math.Abs(osuCurrObj.AdjustedDeltaTime - osuPrev2Obj.AdjustedDeltaTime) > 25)
                return acuteBonus;

            directionChangeFactor = Math.Max(directionChangeFactor, acuteBonus);

            return directionChangeFactor;
        }

        private static double calculateLinearity(OsuDifficultyHitObject current)
        {
            var curBaseObj = (OsuHitObject)current.BaseObject;
            var prevBaseObj = (OsuHitObject)current.Previous(0).BaseObject;
            var prev2BaseObj = (OsuHitObject)current.Previous(1).BaseObject;

            Vector2 lineVector = prev2BaseObj.StackedEndPosition - curBaseObj.StackedEndPosition;
            Vector2 toMiddle = prevBaseObj.StackedEndPosition - curBaseObj.StackedEndPosition;

            float dotToMiddleLine = Vector2.Dot(toMiddle, lineVector);
            float dotLineLine = Vector2.Dot(lineVector, lineVector);

            float projectionScalar = dotToMiddleLine / dotLineLine;

            Vector2 projection = lineVector * projectionScalar;

            float scalingFactor = OsuDifficultyHitObject.NORMALISED_RADIUS / (float)curBaseObj.Radius;

            double perpendicularDistance = curBaseObj.StackedPosition.Equals(prev2BaseObj.StackedPosition)
                ? current.LazyJumpDistance
                : (toMiddle * scalingFactor - projection * scalingFactor).Length;

            return 1 - DifficultyCalculationUtils.Smootherstep(perpendicularDistance, OsuDifficultyHitObject.NORMALISED_RADIUS, OsuDifficultyHitObject.NORMALISED_RADIUS * 1.5);
        }

        private static double calcAcuteAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(140), double.DegreesToRadians(70));
    }
}
