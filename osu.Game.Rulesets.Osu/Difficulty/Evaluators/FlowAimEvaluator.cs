// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;
using osu.Game.Rulesets.Osu.Difficulty;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowAimEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance, OsuDifficultyTuning tuning)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double currVelocity = osuCurrObj.LazyJumpDistance / osuCurrObj.AdjustedDeltaTime;
            double prevVelocity = osuPrevObj.LazyJumpDistance / osuPrevObj.AdjustedDeltaTime;

            double sliderBonus = 0;

            double currLazyJumpDistance = AdjustFlowDistance(osuCurrObj, tuning);

            // Base snap difficulty is velocity.
            double difficulty = Math.Pow(currLazyJumpDistance, tuning.FlowDistanceExponent) / osuCurrObj.AdjustedDeltaTime;

            // But if the last object is a slider, then we extend the travel velocity through the slider into the current object.
            if (osuPrevObj.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = osuPrevObj.TravelDistance / osuPrevObj.TravelTime; // calculate the slider velocity from slider head to slider end.
                double movementVelocity = osuCurrObj.MinimumJumpDistance / osuCurrObj.MinimumJumpTime; // calculate the movement velocity from slider end to current object

                difficulty = Math.Max(difficulty, movementVelocity + travelVelocity); // take the larger total combined velocity.
            }

            difficulty += CalculateJerk(current, tuning) * tuning.FlowJerkScale;

            difficulty *= 1 + CalculateAngularVelocity(current) * tuning.FlowAngularVelocityScale;

            if (osuPrevObj.BaseObject is Slider)
            {
                // Reward sliders based on velocity.
                sliderBonus = osuPrevObj.TravelDistance / osuPrevObj.TravelTime;
            }

            // Add in additional slider velocity bonus.
            if (withSliderTravelDistance)
                difficulty += sliderBonus * tuning.FlowSliderBonusScale;

            return difficulty * tuning.FlowOverallScale * osuCurrObj.SmallCircleBonus;
        }

        /// <summary>
        /// Approximate the amount of unnecessary distance the cursor will travel in an arc attempting to flow between notes
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>

        public static double AdjustFlowDistance(DifficultyHitObject current, OsuDifficultyTuning tuning)
        {
            var osuCurr = (OsuDifficultyHitObject)current;
            var osuPrev = (OsuDifficultyHitObject)current.Previous(0);

            // If angle is missing, it's just distance
            if (!osuCurr.Angle.HasValue)
                return osuCurr.LazyJumpDistance;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            double angle = osuCurr.Angle.Value;
            double distanceTravelled = osuCurr.LazyJumpDistance;

            double maxBonusAngle = tuning.FlowMaxAngleRadians;

            if (angle >= maxBonusAngle)
                return distanceTravelled;

            //extra distance is a function of previous velocity, your arc will be less tight if you're coming in hot
            double previousVelocity = osuPrev.LazyJumpDistance / osuPrev.AdjustedDeltaTime;

            //the sharper the angle, the more inefficient the real path will be
            double angleScale = 1.0 - DifficultyCalculationUtils.Smootherstep(angle, 0, maxBonusAngle);

            //nerf cheesable distances where the angle isn't indicative of the path the cursor takes between notes
            angleScale *= DifficultyCalculationUtils.Smootherstep(osuCurr.LazyJumpDistance, radius, radius * 2);

            angleScale *= 1 - DifficultyCalculationUtils.Smootherstep(GetOverlapness(current), 0, tuning.FlowOverlapNerfMax);

            double velocityBonus = 1 + Math.Pow(previousVelocity, tuning.FlowVelocityBonusExponent) * angleScale * tuning.FlowVelocityBonusScale;

            return Math.Pow(distanceTravelled, velocityBonus);
        }

        private static double SignedAngleDiff(double a, double b)
        {
            double d = a - b;

            if (d > Math.PI)
                d -= 2 * Math.PI;
            else if (d < -Math.PI)
                d += 2 * Math.PI;

            return d;
        }

        public static double CalculateAngularVelocity(DifficultyHitObject current)
        {
            var curr = (OsuDifficultyHitObject)current;
            var prev = (OsuDifficultyHitObject)current.Previous(0);

            if (prev == null)
                return 0;

            if (!curr.AngleSigned.HasValue || !prev.AngleSigned.HasValue)
                return 0;

            double dTheta =
                Math.Abs(SignedAngleDiff(
                    curr.AngleSigned.Value,
                    prev.AngleSigned.Value
                ));

            return (dTheta / curr.DeltaTime);
        }

        public static double CalculateJerk(DifficultyHitObject current, OsuDifficultyTuning tuning)
        {
            var c = (OsuDifficultyHitObject)current;
            var p1 = (OsuDifficultyHitObject)current.Previous(0);
            var p2 = (OsuDifficultyHitObject)current.Previous(1);
            var p3 = (OsuDifficultyHitObject)current.Previous(2);

            if (p1 == null || p2 == null || p3 == null)
                return 0;

            double dt3 = c.AdjustedDeltaTime;
            double dt2 = p1.AdjustedDeltaTime;
            double dt1 = p2.AdjustedDeltaTime;

            if (dt1 <= 0 || dt2 <= 0 || dt3 <= 0)
                return 0;

            double v1 = p2.LazyJumpDistance / dt1;
            double v2 = p1.LazyJumpDistance / dt2;
            double v3 = c.LazyJumpDistance / dt3;

            double dtA2 = 0.5 * (dt1 + dt2);
            double dtA3 = 0.5 * (dt2 + dt3);
            if (dtA2 <= 0 || dtA3 <= 0)
                return 0;

            double a2 = (v2 - v1) / dtA2;
            double a3 = (v3 - v2) / dtA3;

            // jerk (px/ms^3)
            double dtJ = 0.5 * (dtA2 + dtA3);
            if (dtJ <= 0)
                return 0;

            double jerk = Math.Abs(a3 - a2) / dtJ;

            // threshold + scale like before (but you WILL need retuning!)
            double excess = jerk - tuning.FlowJerkDistanceThreshold;
            return Math.Sqrt(Math.Max(0, excess) / tuning.FlowJerkDistanceScale);
        }

        public static double GetOverlapness(DifficultyHitObject current)
        {
            if (!OsuDifficultyHitObject.IsValid(current, 1))
                return 0;

            OsuHitObject o1 = (OsuHitObject)current.BaseObject, o2 = (OsuHitObject)current.Previous(0).BaseObject;

            double distance = Vector2.Distance(o1.StackedPosition, o2.StackedPosition);
            double radius = o1.Radius;

            return Math.Clamp(1 - Math.Pow(Math.Max(distance - radius, 0) / radius, 2), 0, 1);
        }
    }
}
