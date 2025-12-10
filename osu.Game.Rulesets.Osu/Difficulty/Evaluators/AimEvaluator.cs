// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AimEvaluator
    {
        private const double wide_angle_multiplier = 1.5;
        private const double velocity_change_multiplier = 0.75;
        private const double wiggle_multiplier = 1.02;

        public const double SLIDER_MULTIPLIER = 1.27;

        /// <summary>
        /// Evaluates the difficulty of aiming the current object, based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>angle difficulty,</description></item>
        /// <item><description>sharp velocity increases,</description></item>
        /// <item><description>and slider difficulty.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance, bool withCheesability)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuLastLastObj = (OsuDifficultyHitObject)current.Previous(1);
            var osuLast2Obj = (OsuDifficultyHitObject)current.Previous(2);

            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            double currStrainTime = osuCurrObj.MinimumJumpTime;
            double lastStrainTime = osuLastObj.MinimumJumpTime;

            if (withCheesability)
            {
                currStrainTime += osuCurrObj.ExtraDeltaTime;
                lastStrainTime += osuLastObj.ExtraDeltaTime;
            }

            // Calculate the velocity to the current hitobject, which starts with a base distance / time assuming the last object is a hitcircle.
            double currVelocity = osuCurrObj.LazyJumpDistance / currStrainTime;

            // As above, do the same for the previous hitobject.
            double prevVelocity = osuLastObj.LazyJumpDistance / lastStrainTime;

            double wideAngleBonus = 0;
            double sliderBonus = 0;
            double velocityChangeBonus = 0;
            double angleRepetitionNerf = 1;

            double aimStrain = currVelocity; // Start strain with regular velocity.

            if (osuCurrObj.Angle != null && osuLastObj.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuLastObj.Angle.Value;

                double baseFactor = 1 - 0.15 * DifficultyCalculationUtils.Smoothstep(lastAngle, double.DegreesToRadians(90), double.DegreesToRadians(40)) * angleDifference(currAngle, lastAngle);

                // Penalize angle repetition.
                angleRepetitionNerf = Math.Pow(baseFactor + (1 - baseFactor) * angleVectorRepetition(osuCurrObj), 2);

                wideAngleBonus = calcWideAngleBonus(currAngle);

                double wideAngleBase = Math.Min(currVelocity, prevVelocity);

                wideAngleBonus *= wideAngleBase / Math.Max(osuLastObj.AdjustedDeltaTime, osuCurrObj.AdjustedDeltaTime);
            }

            // We want to use the average velocity over the whole object when awarding differences, not the individual jump and slider path velocities.
            prevVelocity = (osuLastObj.LazyJumpDistance + osuLastLastObj.TravelDistance) / osuLastObj.MinimumJumpTime;
            currVelocity = (osuCurrObj.LazyJumpDistance + osuLastObj.TravelDistance) / osuCurrObj.MinimumJumpTime;

            if (Math.Max(prevVelocity, currVelocity) != 0)
            {
                // Scale with ratio of difference compared to 0.5 * max dist.
                double distRatio = DifficultyCalculationUtils.Smoothstep(Math.Abs(prevVelocity - currVelocity) / Math.Max(prevVelocity, currVelocity), 0, 1);

                // Reward for % distance up to 125 / strainTime for overlaps where velocity is still changing.
                double overlapVelocityBuff = Math.Min(diameter * 1.25 / Math.Min(osuCurrObj.AdjustedDeltaTime, osuLastObj.AdjustedDeltaTime), Math.Abs(prevVelocity - currVelocity));

                velocityChangeBonus = overlapVelocityBuff * distRatio;

                // Penalize for rhythm changes.
                velocityChangeBonus *= Math.Pow(Math.Min(osuCurrObj.AdjustedDeltaTime, osuLastObj.AdjustedDeltaTime) / Math.Max(osuCurrObj.AdjustedDeltaTime, osuLastObj.AdjustedDeltaTime), 2);
            }

            if (osuLastObj.BaseObject is Slider)
            {
                // Reward sliders based on velocity.
                sliderBonus = osuLastObj.TravelDistance / osuLastObj.TravelTime;
            }

            aimStrain *= angleRepetitionNerf;

            aimStrain += velocityChangeBonus * 0.3;

            aimStrain += wideAngleBonus * 60;

            // Apply high circle size bonus
            aimStrain *= osuCurrObj.SmallCircleBonus;

            // Add in additional slider velocity bonus.
            if (withSliderTravelDistance)
                aimStrain += sliderBonus * 0.4;

            return aimStrain;
        }

        private static double angleDifference(double curAngle, double lastAngle)
        {
            return Math.Cos(2 * Math.Min(Math.PI / 4, Math.Abs(curAngle - lastAngle)));
        }

        private static double angleVectorRepetition(OsuDifficultyHitObject current)
        {
            const double note_limit = 6;

            double constantAngleCount = 0;
            int index = 0;
            double notesProcessed = 0;

            while (notesProcessed < note_limit)
            {
                var loopObj = (OsuDifficultyHitObject)current.Previous(index);

                if (loopObj.IsNull())
                    break;

                if (Math.Abs(current.DeltaTime - loopObj.DeltaTime) > 25)
                    break;

                if (loopObj.NormalisedVectorAngle.IsNotNull() && current.NormalisedVectorAngle.IsNotNull())
                {
                    double angleDifference = Math.Abs(current.NormalisedVectorAngle.Value - loopObj.NormalisedVectorAngle.Value);
                    constantAngleCount += Math.Cos(8 * Math.Min(Math.PI / 16, angleDifference));
                }

                notesProcessed++;
                index++;
            }

            return Math.Pow(Math.Min(0.5 / constantAngleCount, 1), 2);
        }

        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(60), double.DegreesToRadians(110));
        public static double CalcAcuteAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(140), double.DegreesToRadians(40));
    }
}
