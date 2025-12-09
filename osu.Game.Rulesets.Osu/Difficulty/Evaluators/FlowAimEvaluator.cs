// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowAimEvaluator
    {
        // The reason why this exist in evaluator instead of FlowAim skill - it's because it's very important to keep flowaim in the same scaling as snapaim on evaluator level
        private const double flow_multiplier = 1.1;

        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLast0Obj = (OsuDifficultyHitObject)current.Previous(0);
            var osuLast1Obj = (OsuDifficultyHitObject)current.Previous(1);

            // Start with velocity
            double velocity = osuCurrObj.LazyJumpDistance / osuCurrObj.AdjustedDeltaTime;

            if (osuLast0Obj.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = osuLast0Obj.TravelDistance / osuLast0Obj.TravelTime; // calculate the slider velocity from slider head to slider end.
                double movementVelocity = osuCurrObj.MinimumJumpDistance / osuCurrObj.MinimumJumpTime; // calculate the movement velocity from slider end to current object

                velocity = Math.Max(velocity, movementVelocity + travelVelocity); // take the larger total combined velocity.
            }

            double flowDifficulty = velocity;

            double angleBonus = 0;

            if (osuCurrObj.AngleSigned != null && osuLast0Obj.AngleSigned != null && osuLast1Obj.AngleSigned != null)
            {
                double acuteAngleBonus = CalculateFlowAcuteAngleBonus(current);
                double angleChangeBonus = CalculateFlowAngleChangeBonus(current);

                // If all three notes are overlapping - don't reward angle bonuses as you don't have to do additional movement
                double overlappedNotesWeight = 1;

                if (current.Index > 2)
                {
                    double o1 = getOverlapness(osuCurrObj, osuLast0Obj);
                    double o2 = getOverlapness(osuCurrObj, osuLast1Obj);
                    double o3 = getOverlapness(osuLast0Obj, osuLast1Obj);

                    overlappedNotesWeight = 1 - o1 * o2 * o3;
                }

                angleBonus = Math.Max(acuteAngleBonus, angleChangeBonus) * overlappedNotesWeight;
            }

            flowDifficulty += angleBonus;

            flowDifficulty += CalculateFlowVelocityChangeBonus(current);

            flowDifficulty *= 1.1;

            if (osuLast0Obj.BaseObject is Slider && withSliderTravelDistance)
            {
                double sliderBonus = osuLast0Obj.TravelDistance / osuLast0Obj.TravelTime;
                flowDifficulty += sliderBonus * AimEvaluator.SLIDER_MULTIPLIER;
            }

            return flowDifficulty * osuCurrObj.SmallCircleBonus;
        }

        private static double getOverlapness(OsuDifficultyHitObject odho1, OsuDifficultyHitObject odho2)
        {
            OsuHitObject o1 = (OsuHitObject)odho1.BaseObject, o2 = (OsuHitObject)odho2.BaseObject;

            double distance = Vector2.Distance(o1.StackedPosition, o2.StackedPosition);
            double radius = o1.Radius;

            return Math.Clamp(1 - Math.Pow(Math.Max(distance - radius, 0) / radius, 2), 0, 1);
        }

        // This bonus accounts for the fact that flow is circular movement, therefore flowing on sharp angles is harder.
        public static double CalculateFlowAcuteAngleBonus(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;

            if (osuCurrObj.Angle == null)
                return 0;

            double currAngle = (double)osuCurrObj.Angle;

            double currAngleBonus = AimEvaluator.CalcAcuteAngleBonus(currAngle);

            double currVelocity = osuCurrObj.LazyJumpDistance / osuCurrObj.AdjustedDeltaTime;
            double acuteAngleBonus = currVelocity * currAngleBonus;

            return acuteAngleBonus;
        }

        // This bonus accounts for flow aim being harder when angle is changing.
        public static double CalculateFlowAngleChangeBonus(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLast0Obj = (OsuDifficultyHitObject)current.Previous(0);
            var osuLast1Obj = (OsuDifficultyHitObject)current.Previous(0);

            if (osuCurrObj.AngleSigned == null || osuLast0Obj.AngleSigned == null)
                return 0;

            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            double currVelocity = osuCurrObj.LazyJumpDistance / osuCurrObj.AdjustedDeltaTime;
            double prevVelocity = osuLast0Obj.LazyJumpDistance / osuLast0Obj.AdjustedDeltaTime;

            double currAngle = osuCurrObj.AngleSigned.Value;
            double lastAngle = osuLast0Obj.AngleSigned.Value;

            double baseVelocity = Math.Min(currVelocity, prevVelocity);
            double angleChangeBonus = Math.Pow(Math.Sin((currAngle - lastAngle) / 2), 2) * baseVelocity;

            // Take the largest of last 3 distances and if it's too small - decrease flow angle change bonus, because it's cheesable
            double largestPrevDistance = Math.Max(Math.Max(osuCurrObj.LazyJumpDistance, osuLast0Obj.LazyJumpDistance), osuLast1Obj.LazyJumpDistance);
            angleChangeBonus *= DifficultyCalculationUtils.ReverseLerp(largestPrevDistance, 0, diameter);

            return angleChangeBonus * 1.2;
        }

        public static double CalculateFlowVelocityChangeBonus(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 2 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLast0Obj = (OsuDifficultyHitObject)current.Previous(0);
            var osuLast1Obj = (OsuDifficultyHitObject)current.Previous(1);
            var osuLast2Obj = (OsuDifficultyHitObject)current.Previous(2);

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            double currVelocity = osuCurrObj.LazyJumpDistance / osuCurrObj.AdjustedDeltaTime;
            double prevVelocity = osuLast0Obj.LazyJumpDistance / osuLast0Obj.AdjustedDeltaTime;

            double minVelocity = Math.Min(currVelocity, prevVelocity);
            double maxVelocity = Math.Max(currVelocity, prevVelocity);

            double deltaVelocity = maxVelocity - minVelocity;

            // Buff acceleration 2 times more than decceleration
            if (currVelocity > prevVelocity) deltaVelocity *= 2;

            // Don't buff velocity increase if previous note was slower
            if (currVelocity > prevVelocity)
                deltaVelocity *= DifficultyCalculationUtils.Smoothstep(osuCurrObj.AdjustedDeltaTime, osuLast0Obj.AdjustedDeltaTime * 0.55, osuLast0Obj.AdjustedDeltaTime * 0.75);

            double prev1Distance = osuLast1Obj.LazyJumpDistance;
            double prev2Distance = osuLast2Obj?.LazyJumpDistance ?? 0;

            // If previously there was slow flow pattern - sudden velocity change is much easier because you could flow faster to give yourself more time
            // Add radius to account for distance potenitally being very small
            double distanceSimilarityFactor = DifficultyCalculationUtils.ReverseLerp(prev1Distance + radius, (prev2Distance + radius) * 0.8, (prev2Distance + radius) * 0.95);
            double distanceFactor = 0.5 + 0.5 * DifficultyCalculationUtils.ReverseLerp(Math.Max(prev1Distance, prev2Distance), diameter * 1.5, diameter * 0.75);
            // There also should be something like angleFactor, because if it has aim-control difficulty - you can't really speed-up flow aim that easily

            deltaVelocity *= 1 - 0.5 * distanceSimilarityFactor * distanceFactor;

            // Penalize rhythm change
            deltaVelocity *= DifficultyCalculationUtils.ReverseLerp(osuLast0Obj.AdjustedDeltaTime, osuCurrObj.AdjustedDeltaTime * 0.55, osuCurrObj.AdjustedDeltaTime * 0.75);

            // Don't reward very big differences too much
            if (deltaVelocity > minVelocity * 2)
            {
                double rescaledBonus = deltaVelocity - minVelocity * 2;
                return minVelocity * 2 + Math.Sqrt(2 * rescaledBonus + 1) - 1;
            }

            return deltaVelocity;
        }
    }
}
