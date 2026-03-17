// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim
{
    public static class SnapAimEvaluator
    {
        private const double wide_angle_multiplier = 1.05;
        private const double acute_angle_multiplier = 2.41;
        private const double velocity_change_multiplier = 0.9;
        private const double wiggle_multiplier = 1.02; // WARNING: Increasing this multiplier beyond 1.02 reduces difficulty as distance increases. Refer to the desmos link above the wiggle bonus calculation
        private const double maximum_repetition_nerf = 0.15;
        private const double maximum_vector_influence = 0.5;

        private const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
        private const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

        /// <summary>
        /// Evaluates the difficulty of aiming the current object, based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>angle difficulty,</description></item>
        /// <item><description>sharp velocity increases,</description></item>
        /// <item><description>and slider difficulty.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, Movement currentMovement)
        {
            if (current.BaseObject is Spinner || current.Index < 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;

            var previousMovement = currentMovement.PreviousMovement!;
            var prevPrevMovement = previousMovement.PreviousMovement;

            double currVelocity = currentMovement.Distance / currentMovement.Time;
            double prevVelocity = previousMovement.Distance / previousMovement.Time;

            double wideAngleBonus = 0;
            double acuteAngleBonus = 0;
            double velocityChangeBonus = 0;
            double wiggleBonus = 0;

            double aimStrain = currVelocity; // Start strain with regular velocity.

            if (prevPrevMovement != null)
            {
                double currAngle = currentMovement.Angle(previousMovement);
                double lastAngle = previousMovement.Angle(prevPrevMovement);

                // Rewarding angles, take the smaller velocity as base.
                double angleBonus = Math.Min(currVelocity, prevVelocity);

                wideAngleBonus = calcWideAngleBonus(currAngle);

                wideAngleBonus *= 1;

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
                        wideAngleBonus *= 1 - 0.55 * (1 - distance);
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

            // Penalize angle repetition.
            if (prevPrevMovement != null)
                aimStrain *= vectorAngleRepetition(currentMovement, previousMovement, prevPrevMovement);

            aimStrain += wideAngleBonus * wide_angle_multiplier * highBpmBonus(currentMovement.Time, currentMovement.Distance);
            aimStrain += velocityChangeBonus * velocity_change_multiplier;

            // Add in acute angle bonus or wide angle bonus, whichever is larger.
            aimStrain += wiggleBonus * wiggle_multiplier;

            // Apply high circle size bonus
            if (currentMovement.PrimaryMovement)
                aimStrain *= osuCurrObj.SmallCircleBonus;

            return aimStrain;
        }

        // We decrease strain for distances <radius to fix cases where doubles with no aim requirement
        // have their strain buffed incredibly high due to the delta time.
        // These objects do not require any movement, so it does not make sense to award them.
        private static double highBpmBonus(double ms, double distance) => 1 / (1 - Math.Pow(0.03, Math.Pow(ms / 1000, 0.65)))
                                                                          * DifficultyCalculationUtils.Smootherstep(distance, 0, OsuDifficultyHitObject.NORMALISED_RADIUS);

        private static double vectorAngleRepetition(Movement current, Movement previous, Movement prevPrevious)
        {
            const double note_limit = 6;

            double constantAngleCount = 0;
            double notesProcessed = 0;

            var loopMovement = current;

            while (notesProcessed < note_limit)
            {
                loopMovement = loopMovement.PreviousMovement;

                if (loopMovement.IsNull())
                    break;

                if (Math.Abs(current.Time - loopMovement.Time) > 25)
                    break;

                if (current.PreviousMovement != null && loopMovement.PreviousMovement != null)
                {
                    double angleDifference = Math.Abs(current.NormalizedAngleVector(current.PreviousMovement) - loopMovement.NormalizedAngleVector(loopMovement.PreviousMovement));
                    constantAngleCount += Math.Cos(8 * Math.Min(double.DegreesToRadians(11.25), angleDifference));
                }

                notesProcessed++;
            }

            double vectorRepetition = Math.Pow(Math.Min(0.5 / constantAngleCount, 1), 2);

            double stackFactor = DifficultyCalculationUtils.Smootherstep(current.Distance, 0, OsuDifficultyHitObject.NORMALISED_DIAMETER);

            double currAngle = current.Angle(previous);
            double lastAngle = previous.Angle(prevPrevious);

            double angleDifferenceAdjusted = Math.Cos(2 * Math.Min(double.DegreesToRadians(45), Math.Abs(currAngle - lastAngle) * stackFactor));

            double baseNerf = 1 - maximum_repetition_nerf * CalcAcuteAngleBonus(lastAngle) * angleDifferenceAdjusted;

            return Math.Pow(baseNerf + (1 - baseNerf) * vectorRepetition * maximum_vector_influence * stackFactor, 2);
        }

        /*
        private static double vectorAngleRepetition(OsuDifficultyHitObject current, OsuDifficultyHitObject previous)
        {
            if (current.Angle == null || previous.Angle == null)
                return 1;

            const double note_limit = 6;

            double constantAngleCount = 0;

            for (int index = 0; index < note_limit; index++)
            {
                var loopObj = (OsuDifficultyHitObject)current.Previous(index);

                if (loopObj.IsNull())
                    break;

                // Only consider vectors in the same jump section, stopping to change rhythm ruins momentum
                if (Math.Max(current.AdjustedDeltaTime, loopObj.AdjustedDeltaTime) > 1.1 * Math.Min(current.AdjustedDeltaTime, loopObj.AdjustedDeltaTime))
                    break;

                if (loopObj.NormalisedVectorAngle.IsNotNull() && current.NormalisedVectorAngle.IsNotNull())
                {
                    double angleDifference = Math.Abs(current.NormalisedVectorAngle.Value - loopObj.NormalisedVectorAngle.Value);
                    // Refer to this desmos for tuning, constants need to be precise so that values stay within the range of 0 and 1.
                    // https://www.desmos.com/calculator/a8jesv5sv2
                    constantAngleCount += Math.Cos(8 * Math.Min(double.DegreesToRadians(11.25), angleDifference));
                }
            }

            double vectorRepetition = Math.Pow(Math.Min(0.5 / constantAngleCount, 1), 2);

            double stackFactor = DifficultyCalculationUtils.Smootherstep(current.LazyJumpDistance, 0, OsuDifficultyHitObject.NORMALISED_DIAMETER);

            double currAngle = current.Angle.Value;
            double lastAngle = previous.Angle.Value;

            double angleDifferenceAdjusted = Math.Cos(2 * Math.Min(double.DegreesToRadians(45), Math.Abs(currAngle - lastAngle) * stackFactor));

            double baseNerf = 1 - maximum_repetition_nerf * CalcAcuteAngleBonus(lastAngle) * angleDifferenceAdjusted;

            return Math.Pow(baseNerf + (1 - baseNerf) * vectorRepetition * maximum_vector_influence * stackFactor, 2);
        }
        */
        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(40), double.DegreesToRadians(140));

        public static double CalcAcuteAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(140), double.DegreesToRadians(40));
    }
}
