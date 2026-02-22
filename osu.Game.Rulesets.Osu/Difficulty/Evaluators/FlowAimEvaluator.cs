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

        public static double EvaluateDifficultyOfMovement(DifficultyHitObject current, Movement currentMovement)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            var previousMovement = currentMovement.PreviousMovement!;
            var prevPrevMovement = previousMovement.PreviousMovement;

            double currVelocity = osuCurrObj.LazyJumpDistance / osuCurrObj.AdjustedDeltaTime;
            double prevVelocity = osuPrevObj.LazyJumpDistance / osuPrevObj.AdjustedDeltaTime;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            double adjustedDistanceScale = 1.0;
            double sliderBonus = 0;

            double wiggleBonus = 0;

            double currDistance = AdjustFlowDistance(current, currentMovement, previousMovement, prevPrevMovement);

            // Base snap difficulty is velocity.
            double difficulty = currDistance / osuCurrObj.AdjustedDeltaTime;

            difficulty *= 1 + CalculateJerk(current) * 0.3;

            difficulty += CalculateAngularVelocity(current, currentMovement, previousMovement, prevPrevMovement) * 15;

            wiggleBonus *= 1 - DifficultyCalculationUtils.Smootherstep(GetOverlapness(current), 0, 1);

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

            if (currentMovement.IsNested)
            {
                if (!previousMovement.IsNested && current.BaseObject is SliderEndCircle)
                    difficulty *= 8;
                else
                    difficulty *= 0.0025;
            }

            if (!currentMovement.IsNested)
            {
                // Apply high circle size and high bpm bonuses only to the main movements
                difficulty *= osuCurrObj.SmallCircleBonus;
            }

            return difficulty * 1.55;
        }

        /// <summary>
        /// Approximate the amount of unnecessary distance the cursor will travel in an arc attempting to flow between notes
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>

        public static double AdjustFlowDistance(DifficultyHitObject current, Movement currentMovement, Movement previousMovement, Movement? prevPrevMovement)
        {
            var osuCurr = (OsuDifficultyHitObject)current;
            var osuPrev = (OsuDifficultyHitObject)current.Previous(0);

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            double currAngle = currentMovement.Angle(previousMovement);
            double distanceTravelled = currentMovement.Distance;

            double maxBonusAngle = double.DegreesToRadians(170);

            if (currAngle >= maxBonusAngle)
                return distanceTravelled;

            //extra distance is a function of previous velocity, your arc will be less tight if you're coming in hot
            double previousVelocity = previousMovement.Distance / previousMovement.Time;

            //the sharper the angle, the more inefficient the real path will be
            double angleScale = 1.0 - DifficultyCalculationUtils.Smootherstep(currAngle, 0, maxBonusAngle);

            //nerf cheesable distances where the angle isn't indicative of the path the cursor takes between notes
            angleScale *= DifficultyCalculationUtils.Smootherstep(currentMovement.Distance, radius, radius * 2);

            angleScale *= 1 - DifficultyCalculationUtils.Smootherstep(GetOverlapness(current), 0, 0.05);


            double velocityBonus = 1 + Math.Pow(previousVelocity, 1) * angleScale * 0.65;

            return Math.Pow(distanceTravelled, velocityBonus);
        }

        private static double signedAngleDiff(double a, double b)
        {
            double d = a - b;

            if (d > Math.PI)
                d -= 2 * Math.PI;
            else if (d < -Math.PI)
                d += 2 * Math.PI;

            return d;
        }

        public static double CalculateAngularVelocity(DifficultyHitObject current, Movement currentMovement, Movement previousMovement, Movement? prevPrevMovement)
        {

            var curr = (OsuDifficultyHitObject)current;

            if (prevPrevMovement == null)
                return 0;

            double currAngleSigned = currentMovement.Angle(previousMovement, true);
            double lastAngleSigned = previousMovement.Angle(prevPrevMovement, true);



            double dTheta =
                Math.Abs(signedAngleDiff(
                    currAngleSigned,
                    lastAngleSigned
                ));

            return (dTheta / curr.DeltaTime);
        }

        public static double CalculateJerk(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuPrev2Obj = (OsuDifficultyHitObject)current.Previous(1);
            var osuPrev3Obj = (OsuDifficultyHitObject)current.Previous(2);

            if (osuPrevObj == null || osuPrev2Obj == null || osuPrev3Obj == null)
                return 0;

            double currDistanceDifference = Math.Abs(osuCurrObj.LazyJumpDistance - osuPrevObj.LazyJumpDistance);
            double prevDistanceDifference = Math.Abs(osuPrevObj.LazyJumpDistance - osuPrev2Obj.LazyJumpDistance);

            return Math.Sqrt(Math.Max(0, Math.Abs(currDistanceDifference - prevDistanceDifference) - 5) / 5);
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
