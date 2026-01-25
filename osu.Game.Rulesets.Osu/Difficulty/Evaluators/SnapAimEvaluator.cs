// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AimEvaluator
    {
        private const double wide_angle_multiplier = 1.5;
        private const double acute_angle_multiplier = 2.3;
        private const double slider_multiplier = 1.5;
        private const double velocity_change_multiplier = 0.75;
        private const double wiggle_multiplier = 1.02; // WARNING: Increasing this multiplier beyond 1.02 reduces difficulty as distance increases. Refer to the desmos link above the wiggle bonus calculation

        /// <summary>
        /// Evaluates the difficulty of aiming a movement, based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the movement,</description></item>
        /// <item><description>angle difficulty,</description></item>
        /// <item><description>and sharp velocity increases.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOfMovement(DifficultyHitObject current, Movement currentMovement)
        {
            if (current.BaseObject is Spinner || current.Index < 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuLastLastObj = (OsuDifficultyHitObject)current.Previous(1);

            if (osuLastLastObj != null && osuLastLastObj.BaseObject is Spinner)
                osuLastLastObj = null;

            int indexOfMovement = osuCurrObj.Movements.IndexOf(currentMovement);

            var previousMovement = indexOfMovement > 0
                ? osuCurrObj.Movements[indexOfMovement - 1]
                : osuLastObj.Movements.Last();

            var prevPrevMovement = indexOfMovement > 1
                ? osuCurrObj.Movements[indexOfMovement - 2]
                : osuLastObj.Movements.Count > 1
                    ? osuLastObj.Movements[^2]
                    : osuLastLastObj?.Movements.LastOrDefault();

            return calcMovementStrain(current, currentMovement, previousMovement, prevPrevMovement, indexOfMovement > 0);
        }

        private static double calcMovementStrain(DifficultyHitObject current, Movement currentMovement, Movement previousMovement, Movement? prevPrevMovement, bool isNested)
        {
            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            var osuCurrObj = (OsuDifficultyHitObject)current;

            double currVelocity = currentMovement.Distance / (currentMovement.IsNested ? Math.Pow(currentMovement.Time, 1) : currentMovement.Time);
            double prevVelocity = previousMovement.Distance / (previousMovement.IsNested ? Math.Pow(previousMovement.Time, 1) : previousMovement.Time);

            double wideAngleBonus = 0;
            double acuteAngleBonus = 0;
            double velocityChangeBonus = 0;
            double wiggleBonus = 0;

            double aimStrain = currVelocity;

            if (prevPrevMovement != null)
            {
                double currAngle = currentMovement.Angle(previousMovement);
                double lastAngle = previousMovement.Angle(prevPrevMovement);

                // Rewarding angles, take the smaller velocity as base.
                double angleBonus = Math.Min(currVelocity, prevVelocity);

                if (Math.Max(currentMovement.Time, previousMovement.Time) < 1.25 * Math.Min(currentMovement.Time, previousMovement.Time)) // If rhythms are the same.
                {
                    acuteAngleBonus = calcAcuteAngleBonus(currAngle);

                    // Penalize angle repetition.
                    acuteAngleBonus *= 0.08 + 0.92 * (1 - Math.Min(acuteAngleBonus, Math.Pow(calcAcuteAngleBonus(lastAngle), 3)));

                    // Apply acute angle bonus for BPM above 300 1/2 and distance more than one diameter
                    acuteAngleBonus *= angleBonus *
                                       DifficultyCalculationUtils.Smootherstep(DifficultyCalculationUtils.MillisecondsToBPM(currentMovement.Time, 2), 300, 400) *
                                       DifficultyCalculationUtils.Smootherstep(currentMovement.Distance, diameter, diameter * 2);
                }

                wideAngleBonus = calcWideAngleBonus(currAngle);

                // Penalize angle repetition.
                wideAngleBonus *= 1 - Math.Min(wideAngleBonus, Math.Pow(calcWideAngleBonus(lastAngle), 3));

                // Apply full wide angle bonus for distance more than SINGLE_SPACING_THRESHOLD
                wideAngleBonus *= angleBonus * Math.Pow(DifficultyCalculationUtils.Smoothstep(currentMovement.Distance, 0, SpeedAimEvaluator.SINGLE_SPACING_THRESHOLD), 3.0);

                // Apply wiggle bonus for jumps that are [radius, 3*diameter] in distance, with < 110 angle
                // https://www.desmos.com/calculator/dp0v0nvowc
                wiggleBonus = angleBonus
                              * DifficultyCalculationUtils.Smootherstep(currentMovement.Distance, radius, diameter)
                              * Math.Pow(DifficultyCalculationUtils.ReverseLerp(currentMovement.Distance, diameter * 3, diameter), 1.8)
                              * DifficultyCalculationUtils.Smootherstep(currAngle, double.DegreesToRadians(110), double.DegreesToRadians(60))
                              * DifficultyCalculationUtils.Smootherstep(previousMovement.Distance, radius, diameter)
                              * Math.Pow(DifficultyCalculationUtils.ReverseLerp(previousMovement.Distance, diameter * 3, diameter), 1.8)
                              * DifficultyCalculationUtils.Smootherstep(lastAngle, double.DegreesToRadians(110), double.DegreesToRadians(60));

                var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);
                var osuLast2Obj = (OsuDifficultyHitObject)current.Previous(2);

                if (osuLast2Obj != null)
                {
                    // If objects just go back and forth through a middle point - don't give as much wide bonus
                    // Use Previous(2) and Previous(0) because angles calculation is done prevprev-prev-curr, so any object's angle's center point is always the previous object
                    var lastBaseObject = (OsuHitObject)osuLastObj.BaseObject;
                    var last2BaseObject = (OsuHitObject)osuLast2Obj.BaseObject;

                    float distance = (last2BaseObject.StackedPosition - lastBaseObject.StackedPosition).Length;

                    if (distance < 1)
                    {
                        wideAngleBonus *= 1 - 0.35 * (1 - distance);
                    }
                }
            }

            if (Math.Max(prevVelocity, currVelocity) != 0)
            {
                // Scale with ratio of difference compared to 0.5 * max dist.
                double distRatio = DifficultyCalculationUtils.Smoothstep(Math.Abs(prevVelocity - currVelocity) / Math.Max(prevVelocity, currVelocity), 0, 1);

                // Reward for % distance up to 125 / strainTime for overlaps where velocity is still changing.
                double overlapVelocityBuff = Math.Min(diameter * 1.25 / Math.Min(currentMovement.Time, previousMovement.Time), Math.Abs(prevVelocity - currVelocity));

                velocityChangeBonus = overlapVelocityBuff * distRatio;

                // Penalize for rhythm changes.
                velocityChangeBonus *= Math.Pow(Math.Min(currentMovement.Time, previousMovement.Time) / Math.Max(currentMovement.Time, previousMovement.Time), 2);
            }

            if (isNested)
            {
                aimStrain *= 8.0;
            }

            aimStrain += wiggleBonus * wiggle_multiplier;
            aimStrain += velocityChangeBonus * velocity_change_multiplier;

            // Add in acute angle bonus or wide angle bonus, whichever is larger.
            aimStrain += Math.Max(acuteAngleBonus * acute_angle_multiplier, wideAngleBonus * wide_angle_multiplier);

            if (!isNested)
            {
                // Apply high circle size and high bpm bonuses only to the main movements
                aimStrain *= osuCurrObj.SmallCircleBonus;
                aimStrain *= highBpmBonus(currentMovement.Time, currentMovement.Distance);
            }

            return aimStrain;
        }

        // We decrease strain for distances <radius to fix cases where doubles with no aim requirement
        // have their strain buffed incredibly high due to the delta time.
        // These objects do not require any movement, so it does not make sense to award them.
        private static double highBpmBonus(double ms, double distance) => 1 / (1 - Math.Pow(0.15, ms / 1000))
                                                                          * DifficultyCalculationUtils.Smootherstep(distance, 0, OsuDifficultyHitObject.NORMALISED_RADIUS);

        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(40), double.DegreesToRadians(140));

        private static double calcAcuteAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(140), double.DegreesToRadians(40));
    }
}
