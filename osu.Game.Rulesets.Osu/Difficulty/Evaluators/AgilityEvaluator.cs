// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AgilityEvaluator
    {
        // === Tunable constants for MassBalancer ===
        public static double baseBPMConstant = 240.0;
        public static double agilityExponent = 4.0;
        public static double agilityOverallMultiplier = 0.02;
        public static double agilityVelocityChangeMultiplier = 0.1;
        public static double angleBonusMultiplier = 0.35;
        public static double distanceBonusMultiplier = 0.00000000175;

        public static double EvaluateDifficultyOfMovement(DifficultyHitObject current, Movement currentMovement)
        {
            if (current.BaseObject is Spinner || current.Index < 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            double agilityBonus = 0;

            var previousMovement = currentMovement.PreviousMovement!;
            var prevPrevMovement = previousMovement.PreviousMovement;

            if (prevPrevMovement != null)
            {
                const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

                var osuCurrObj = (OsuDifficultyHitObject)current;
                var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

                double currStrainTime = currentMovement.Time;
                double lastStrainTime = previousMovement.Time;

                double currVelocity = currentMovement.Distance / currStrainTime;
                double prevVelocity = previousMovement.Distance / lastStrainTime;

                double currDistanceMultiplier = Smootherstep(currentMovement.Distance / radius, 1, 2);
                double prevDistanceMultiplier = Smootherstep(previousMovement.Distance / radius, 1, 2);

                double currTime = currStrainTime + lastStrainTime * (1 - prevDistanceMultiplier);
                double prevTime = lastStrainTime;

                double currAngle = currentMovement.Angle(previousMovement);



                double angleBonus = 0.25 * Smootherstep(double.RadiansToDegrees(currAngle), 40, 120);

                double velocityChangeBonus = Math.Abs(prevVelocity - currVelocity) * agilityVelocityChangeMultiplier;

                double baseBpm = baseBPMConstant / (1 + angleBonus * currDistanceMultiplier * prevDistanceMultiplier);

                agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, 3) - 1);
            }

            if (currentMovement.IsNested)
            {
                if (!previousMovement.IsNested && current.BaseObject is SliderEndCircle)
                    agilityBonus *= 8;
                else
                    agilityBonus *= 0;
            }


            return agilityBonus * 0.195;
        }
    }
}
