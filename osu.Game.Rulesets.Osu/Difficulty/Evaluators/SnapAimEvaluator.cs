﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class SnapAimEvaluator
    {
        private const double wide_angle_multiplier = 1.5;
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
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = withSliderTravelDistance ? (OsuDifficultyHitObject)current.Previous(0) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(0);
            var osuLastLastObj = withSliderTravelDistance ? (OsuDifficultyHitObject)current.Previous(1) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(1);
            var osuLast2Obj = withSliderTravelDistance ? (OsuDifficultyHitObject)current.Previous(2) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(2);

            if (!(withSliderTravelDistance || osuCurrObj.IsTapObject || osuCurrObj.PrevTapStrainTime is not null))
                return 0;

            if (osuCurrObj.PrevMinimumJumpTime is null || osuLastObj is null)
            {
                return 0;
            }

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;
            const float maxSliderRadius = OsuDifficultyHitObject.MAXIMUM_SLIDER_RADIUS;

            double currStrainTime = withSliderTravelDistance ? osuCurrObj.MinimumJumpTime : osuCurrObj.TapStrainTime;
            double prevStrainTime = withSliderTravelDistance ? (double)osuCurrObj.PrevMinimumJumpTime! : (double)osuCurrObj.PrevTapStrainTime!;
            double truePrevStrainTime = withSliderTravelDistance ? osuLastObj.MinimumJumpTime : osuLastObj.TapStrainTime;

            double currDistance = withSliderTravelDistance ? osuCurrObj.LazyJumpDistance : osuCurrObj.SliderlessJumpDistance;
            double prevDistance = withSliderTravelDistance ? osuCurrObj.PrevLazyJumpDistance : osuCurrObj.PrevSliderlessJumpDistance;
            double truePrevDistance = withSliderTravelDistance ? osuLastObj.LazyJumpDistance : osuLastObj.SliderlessJumpDistance;

            // Calculate the velocity to the current hitobject, which starts with a base distance / time assuming the last object is a hitcircle.
            double currVelocity = currDistance / currStrainTime;

            // As above, do the same for the previous hitobject.
            double prevVelocity = prevDistance / prevStrainTime;

            // Used only for velocity change bonus to avoid certain buzz sliders being worth too much
            double truePrevVelocity = truePrevDistance / truePrevStrainTime;

            double travelVelocity = 0;
            double movementVelocity = 0;


            double wideAngleBonus = 0;
            double sliderBonus = 0;
            double velocityChangeBonus = 0;
            double wiggleBonus = 0;
            double angleRepetitionNerf = 1;
            double sliderAngleRepetitionNerf = 1;

            double aimStrain = 0;

            double? currAngle = withSliderTravelDistance ? osuCurrObj.Angle : osuCurrObj.SliderlessAngle;
            double? lastAngle = withSliderTravelDistance ? osuCurrObj.PrevAngle : osuCurrObj.PrevSliderlessAngle;
            double? trueLastAngle = withSliderTravelDistance ? osuLastObj.Angle : osuLastObj.SliderlessAngle;

            if (currAngle is not null && lastAngle is not null && osuLastObj.IsTapObject)
            {
                double currAngleValue = currAngle.Value;
                double lastAngleValue = lastAngle.Value;

                double baseFactor = 1 - 0.15 * DifficultyCalculationUtils.Smoothstep(lastAngleValue, double.DegreesToRadians(90), double.DegreesToRadians(40)) * angleDifference(currAngleValue, lastAngleValue);

                // Penalize angle repetition.
                angleRepetitionNerf = Math.Pow(baseFactor + (1 - baseFactor) * angleVectorRepetition(osuCurrObj), 2);

                // Rewarding angles, take the smaller velocity as base.
                double angleBonus = Math.Min(currVelocity, prevVelocity);

                wideAngleBonus = calcWideAngleBonus(currAngleValue);

                double wideBaseFactor = 1 - 0.3 * DifficultyCalculationUtils.Smoothstep(currAngleValue, double.DegreesToRadians(140), double.DegreesToRadians(90)) * angleDifference(currAngleValue, lastAngleValue);

                // Penalize angle repetition.
                wideAngleBonus *= angleBonus * Math.Pow(wideBaseFactor + (1 - wideBaseFactor) * angleVectorRepetition(osuCurrObj), 2);

                // Apply wiggle bonus for jumps that are [radius, 3*diameter] in distance, with < 110 angle
                // https://www.desmos.com/calculator/dp0v0nvowc
                wiggleBonus = angleBonus
                              * DifficultyCalculationUtils.Smootherstep(currDistance, radius, diameter)
                              * Math.Pow(DifficultyCalculationUtils.ReverseLerp(currDistance, diameter * 3, diameter), 1.8)
                              * DifficultyCalculationUtils.Smootherstep(currAngleValue, double.DegreesToRadians(110), double.DegreesToRadians(60))
                              * DifficultyCalculationUtils.Smootherstep(prevDistance, radius, diameter)
                              * Math.Pow(DifficultyCalculationUtils.ReverseLerp(prevDistance, diameter * 3, diameter), 1.8)
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

            if (osuLastObj.BaseObject is Slider)
            {
                // Reward sliders based on velocity.
                sliderBonus = osuLastObj.TravelDistance / osuLastObj.TravelTime;
            }

            // Add in acute angle bonus or wide angle bonus, whichever is larger.
            aimStrain += Math.Max(
                currVelocity * angleRepetitionNerf + wideAngleBonus * 1.2,
                Math.Max(travelVelocity, movementVelocity) * sliderAngleRepetitionNerf);

            aimStrain += wiggleBonus * 2;
            aimStrain += velocityChangeBonus * velocity_change_multiplier;

            // Apply high circle size bonus
            if (osuCurrObj.IsTapObject)
                aimStrain *= osuCurrObj.SmallCircleBonus;

            // Add in additional slider velocity bonus.
            if (withSliderTravelDistance)
                aimStrain += sliderBonus * 2;

            return aimStrain * 20.5;
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

        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(40), double.DegreesToRadians(140));
    }
}
