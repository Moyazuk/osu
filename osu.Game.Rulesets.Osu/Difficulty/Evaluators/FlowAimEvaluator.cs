// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using static osu.Game.Rulesets.Osu.Difficulty.Preprocessing.OsuDifficultyHitObject;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowAimEvaluator
    {

        public static double angleScaleMultiplier = 0.4;
        public static double flowOverallMultiplier = 0.725;
        public static double velocityChangeMultiplier = 4;
        public static double angularVelocityMultiplier = 0.05;

        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double currVelocity = osuCurrObj.LazyJumpDistance / osuCurrObj.AdjustedDeltaTime;
            double prevVelocity = osuPrevObj.LazyJumpDistance / osuPrevObj.AdjustedDeltaTime;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            double adjustedDistanceScale = 1.0;
            double sliderBonus = 0;

            double wiggleBonus = 0;

            if (osuCurrObj.Angle.HasValue &&
                osuPrevObj?.Angle != null &&
                Math.Abs(osuCurrObj.DeltaTime - osuPrevObj.DeltaTime) < 25)
            {
                double angleDifference = Math.Abs(osuCurrObj.Angle.Value - osuPrevObj.Angle.Value);
                double angleDifferenceAdjusted = Math.Sin(angleDifference / 2) * 180.0;
                double angularVelocity = angleDifferenceAdjusted / (0.1 * osuCurrObj.AdjustedDeltaTime);
                double angularVelocityBonus = Math.Max(0.0, Math.Pow(angularVelocity, 0.5) - 1.0);
                //nerf cheesable distances where the angle isn't indicative of the path the cursor takes between notes
                //angularVelocityBonus *= DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance, radius * 0.5, radius * 2);
                adjustedDistanceScale = 1 + angularVelocityBonus * 0.05;

                // Apply wiggle bonus for jumps that are [radius, 3*diameter] in distance, with < 110 angle
                // https://www.desmos.com/calculator/dp0v0nvowc
                wiggleBonus = currVelocity
                              * DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance, radius, diameter)
                              * DifficultyCalculationUtils.Smootherstep(osuCurrObj.Angle.Value, double.DegreesToRadians(110), double.DegreesToRadians(60))
                              * DifficultyCalculationUtils.Smootherstep(osuPrevObj.LazyJumpDistance, radius, diameter)
                              * DifficultyCalculationUtils.Smootherstep(osuPrevObj.Angle.Value, double.DegreesToRadians(110), double.DegreesToRadians(60));
            }

            double currLazyJumpDistance = AdjustFlowDistance(osuCurrObj);

            // Base snap difficulty is velocity.
            double difficulty = Math.Pow(currLazyJumpDistance, adjustedDistanceScale) / osuCurrObj.AdjustedDeltaTime;

            // But if the last object is a slider, then we extend the travel velocity through the slider into the current object.
            if (osuPrevObj.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = osuPrevObj.TravelDistance / osuPrevObj.TravelTime; // calculate the slider velocity from slider head to slider end.
                double movementVelocity = osuCurrObj.MinimumJumpDistance / osuCurrObj.MinimumJumpTime; // calculate the movement velocity from slider end to current object

                difficulty = Math.Max(difficulty, movementVelocity + travelVelocity); // take the larger total combined velocity.
            }

            if (osuPrevObj.BaseObject is Slider)
            {
                // Reward sliders based on velocity.
                sliderBonus = osuPrevObj.TravelDistance / osuPrevObj.TravelTime;
            }

            double flowVelChange = Math.Abs(prevVelocity - currVelocity);

            difficulty += flowVelChange * velocityChangeMultiplier;

            wiggleBonus *= 1 - DifficultyCalculationUtils.Smootherstep(GetOverlapness(current), 0, 1);

            difficulty += wiggleBonus * 1200;

            // Flow aim is harder on High BPM
            const double base_speedflow_multiplier = 0.175; // Base multiplier for speedflow bonus
            const double bpm_factor = 12; // How steep the bonus is, higher values means more bonus for high BPM

            // Autobalance, it's expected for bonus multiplier to be 1 for the bpm base
            double bpmBase = DifficultyCalculationUtils.BPMToMilliseconds(220, 4);
            double bpmFactorMultiplierAtBase = bpmBase / (bpmBase - bpm_factor) - 1;
            double multiplier = base_speedflow_multiplier / bpmFactorMultiplierAtBase;

            // Start from base of the bonus
            double speeflowBonus = multiplier * diameter / osuCurrObj.AdjustedDeltaTime;

            // Spacing factor, reward up to 1 radius. The reason why we're doing this is because we want to be close live speedflow
            // If we won't do this - it will be similar to multiplicative speed and distance bonuses, not additive
            speeflowBonus *= DifficultyCalculationUtils.Smoothstep(osuCurrObj.LazyJumpDistance, -radius, radius);

            // Bpm factor
            speeflowBonus *= (osuCurrObj.AdjustedDeltaTime / (osuCurrObj.AdjustedDeltaTime - bpm_factor) - 1);

            difficulty += 0;

            // Add in additional slider velocity bonus.
            if (withSliderTravelDistance)
                difficulty += sliderBonus * 0.3;

            return difficulty * 0.825 * osuCurrObj.SmallCircleBonus;
        }

        /// <summary>
        /// Approximate the amount of unnecessary distance the cursor will travel in an arc attempting to flow between notes
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>

        public static double AdjustFlowDistance(DifficultyHitObject current)
        {
            var osuCurr = (OsuDifficultyHitObject)current;
            var osuPrev = (OsuDifficultyHitObject)current.Previous(0);

            // If angle is missing, it's just distance
            if (!osuCurr.Angle.HasValue)
                return osuCurr.LazyJumpDistance;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            double angle = osuCurr.Angle.Value;
            double distanceTravelled = osuCurr.LazyJumpDistance;

            double maxBonusAngle = double.DegreesToRadians(170);

            if (angle >= maxBonusAngle)
                return distanceTravelled;

            //extra distance is a function of previous velocity, your arc will be less tight if you're coming in hot
            double previousVelocity = osuPrev.LazyJumpDistance / osuPrev.AdjustedDeltaTime;

            //the sharper the angle, the more inefficient the real path will be
            double angleScale = 1.0 - DifficultyCalculationUtils.Smootherstep(angle, 0, maxBonusAngle);

            //nerf cheesable distances where the angle isn't indicative of the path the cursor takes between notes
            angleScale *= DifficultyCalculationUtils.Smootherstep(osuCurr.LazyJumpDistance, radius, radius * 2);

            angleScale *= 1 - DifficultyCalculationUtils.Smootherstep(GetOverlapness(current), 0, 0.05);


            double velocityBonus = 1.1 + Math.Pow(previousVelocity, 1) * angleScale * 0.35;

            return Math.Pow(distanceTravelled, velocityBonus);
        }

        public static double GetOverlapness(DifficultyHitObject current)
        {
            if (!IsValid(current, 1))
                return 0;

            OsuHitObject o1 = (OsuHitObject)current.BaseObject, o2 = (OsuHitObject)current.Previous(0).BaseObject;

            double distance = Vector2.Distance(o1.StackedPosition, o2.StackedPosition);
            double radius = o1.Radius;

            return Math.Clamp(1 - Math.Pow(Math.Max(distance - radius, 0) / radius, 2), 0, 1);
        }
    }
}
