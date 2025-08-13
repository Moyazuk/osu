﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class SnapAimEvaluator
    {
        private const double wide_angle_multiplier = 1.5;
        private const double acute_angle_multiplier = 2.55;
        private const double slider_multiplier = 1.35;
        private const double velocity_change_multiplier = 0.75;
        private const double wiggle_multiplier = 1.02;

        /// <summary>
        /// Evaluates the difficulty of aiming the current object, based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>angle difficulty,</description></item>
        /// <item><description>sharp velocity increases,</description></item>
        /// <item><description>and slider difficulty.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool includeSliders, bool withCheesability)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = includeSliders ? (OsuDifficultyHitObject)current.Previous(0) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(0);
            var osuLastLastObj = includeSliders ? (OsuDifficultyHitObject)current.Previous(1) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(1);
            var osuLast2Obj = includeSliders ? (OsuDifficultyHitObject)current.Previous(2) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(2);

            if (!(includeSliders || osuCurrObj.IsTapObject || osuCurrObj.PrevTapStrainTime is not null))
                return 0;

            if (osuCurrObj.PrevMinimumJumpTime is null || osuLastObj is null)
            {
                return 0;
            }

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            double currStrainTime = includeSliders ? osuCurrObj.MinimumJumpTime : osuCurrObj.TapStrainTime;
            double prevStrainTime = includeSliders ? (double)osuCurrObj.PrevMinimumJumpTime! : (double)osuCurrObj.PrevTapStrainTime!;
            double truePrevStrainTime = includeSliders ? osuLastObj.MinimumJumpTime : osuLastObj.TapStrainTime;

            double currDistance = includeSliders ? osuCurrObj.LazyJumpDistance : osuCurrObj.SliderlessJumpDistance;
            double prevDistance = includeSliders ? osuCurrObj.PrevLazyJumpDistance : osuCurrObj.PrevSliderlessJumpDistance;
            double truePrevDistance = includeSliders ? osuLastObj.LazyJumpDistance : osuLastObj.SliderlessJumpDistance;

            // Calculate the velocity to the current hitobject, which starts with a base distance / time assuming the last object is a hitcircle.
            double currVelocity = currDistance / currStrainTime;

            // As above, do the same for the previous hitobject.
            double prevVelocity = prevDistance / prevStrainTime;

            // Used only for velocity change bonus to avoid certain buzz sliders being worth too much
            double truePrevVelocity = truePrevDistance / truePrevStrainTime;

            double wideAngleBonus = 0;
            double acuteAngleBonus = 0;
            double velocityChangeBonus = 0;
            double wiggleBonus = 0;
            double sliderBonus = 0;

            double aimStrain = currVelocity; // Start strain with regular velocity.

            double? currAngle = includeSliders ? osuCurrObj.Angle : osuCurrObj.SliderlessAngle;
            double? lastAngle = includeSliders ? osuCurrObj.PrevAngle : osuCurrObj.PrevSliderlessAngle;
            double? trueLastAngle = includeSliders ? osuLastObj.Angle : osuLastObj.SliderlessAngle;

            if (currAngle is not null && lastAngle is not null && osuLastObj.IsTapObject)
            {
                double currAngleValue = currAngle.Value;
                double lastAngleValue = lastAngle.Value;

                // Rewarding angles, take the smaller velocity as base.
                double angleBonus = Math.Min(currVelocity, prevVelocity);

                if (Math.Max(currStrainTime, prevStrainTime) < 1.25 * Math.Min(currStrainTime, prevStrainTime)) // If rhythms are the same.
                {
                    acuteAngleBonus = calcAcuteAngleBonus(currAngleValue);

                    // Pretend this is repetition nerf
                    acuteAngleBonus *= 0.08 + 0.92 * (1 - Math.Min(acuteAngleBonus, Math.Pow(calcAcuteAngleBonus(lastAngleValue), 3)));

                    // Apply acute angle bonus for BPM above 300 1/2 and distance more than one diameter
                    acuteAngleBonus *= angleBonus *
                                       DifficultyCalculationUtils.Smootherstep(DifficultyCalculationUtils.MillisecondsToBPM(currStrainTime, 2), 300, 400) *
                                       DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance, diameter, diameter * 2);
                }

                wideAngleBonus = calcWideAngleBonus(currAngleValue);

                // Penalize angle repetition.
                wideAngleBonus *= 1 - Math.Min(wideAngleBonus, Math.Pow(calcWideAngleBonus(lastAngleValue), 3));

                // Apply full wide angle bonus for distance more than one diameter
                wideAngleBonus *= angleBonus * DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance, 0, diameter);

                wiggleBonus = angleBonus
                              * DifficultyCalculationUtils.Smootherstep(currDistance, radius, diameter)
                              * Math.Pow(DifficultyCalculationUtils.ReverseLerp(currDistance, diameter * 3, diameter), 1.8)
                              * DifficultyCalculationUtils.Smootherstep(currAngleValue, double.DegreesToRadians(110), double.DegreesToRadians(60))
                              * DifficultyCalculationUtils.Smootherstep(currDistance, radius, diameter)
                              * Math.Pow(DifficultyCalculationUtils.ReverseLerp(currDistance, diameter * 3, diameter), 1.8)
                              * DifficultyCalculationUtils.Smootherstep(lastAngleValue, double.DegreesToRadians(110), double.DegreesToRadians(60));

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

            else if (currAngle is not null && lastAngle is not null && !osuLastObj.IsTapObject)
            {
                double currAngleValue = currAngle.Value;
                double lastAngleValue = lastAngle.Value;

                // Rewarding angles, take the smaller velocity as base.
                double angleBonus = Math.Min(currVelocity, prevVelocity);

                if (Math.Max(currStrainTime, prevStrainTime) < 1.25 * Math.Min(currStrainTime, prevStrainTime)) // If rhythms are the same.
                {
                    acuteAngleBonus = calcAcuteAngleBonus(currAngleValue);

                    // Pretend this is repetition nerf
                    acuteAngleBonus *= 0.08 + 0.92 * (1 - Math.Min(acuteAngleBonus, Math.Pow(calcAcuteAngleBonus(lastAngleValue), 3)));

                    // Apply acute angle bonus for BPM above 300 1/2 and distance more than one diameter
                    acuteAngleBonus *= angleBonus *
                                       DifficultyCalculationUtils.Smootherstep(DifficultyCalculationUtils.MillisecondsToBPM(currStrainTime, 2), 300, 400) *
                                       DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance, diameter, diameter * 2);
                }

                wiggleBonus = angleBonus
                              * DifficultyCalculationUtils.Smootherstep(currDistance, radius, diameter)
                              * Math.Pow(DifficultyCalculationUtils.ReverseLerp(currDistance, diameter * 3, diameter), 1.8)
                              * DifficultyCalculationUtils.Smootherstep(currAngleValue, double.DegreesToRadians(110), double.DegreesToRadians(60))
                              * DifficultyCalculationUtils.Smootherstep(currDistance, radius, diameter)
                              * Math.Pow(DifficultyCalculationUtils.ReverseLerp(currDistance, diameter * 3, diameter), 1.8)
                              * DifficultyCalculationUtils.Smootherstep(lastAngleValue, double.DegreesToRadians(110), double.DegreesToRadians(60));
            }

            if (Math.Max(truePrevVelocity, currVelocity) != 0)
            {
                // Scale with ratio of difference compared to 0.5 * max dist.
                double distRatio = DifficultyCalculationUtils.Smoothstep(Math.Abs(truePrevVelocity - currVelocity) / Math.Max(truePrevVelocity, currVelocity), 0, 1);

                // Reward for % distance up to 125 / strainTime for overlaps where velocity is still changing.
                double overlapVelocityBuff = Math.Min(diameter * 1.25 / Math.Min(currStrainTime, truePrevStrainTime), Math.Abs(truePrevVelocity - currVelocity));

                velocityChangeBonus = overlapVelocityBuff * distRatio;

                // Penalize for rhythm changes.
                velocityChangeBonus *= Math.Pow(Math.Min(currStrainTime, truePrevStrainTime) / Math.Max(currStrainTime, truePrevStrainTime), 2);
            }

            // This bonus is so maps like /b/2844649 don't lose following the replacement of xexxar sliders
            // It should be removed once a better solution is found
            if (!osuCurrObj.IsTapObject)
            {
                sliderBonus = osuCurrObj.LazyJumpDistance / osuCurrObj.MinimumJumpTime;

                // This is stupid so you know it's carried over from Xexxar
                if (osuCurrObj.Parent?.BaseObject is Slider currSlider)
                    sliderBonus *= Math.Pow(1 + currSlider.RepeatCount / 2.5, 1.0 / 2.5);
            }

            aimStrain += wiggleBonus * wiggle_multiplier;
            aimStrain += velocityChangeBonus * velocity_change_multiplier;

            if (includeSliders)
                aimStrain += sliderBonus * slider_multiplier;

            // Add in acute angle bonus or wide angle bonus, whichever is larger.
            aimStrain += wideAngleBonus * wide_angle_multiplier;

            // Apply high circle size bonus
            aimStrain *= osuCurrObj.SmallCircleBonus;

            return aimStrain;
        }

        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(40), double.DegreesToRadians(140));

        private static double calcAcuteAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(140), double.DegreesToRadians(40));
    }
}
