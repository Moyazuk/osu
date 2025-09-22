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
        private const double acute_angle_multiplier = 2.6;
        private const double slider_multiplier = 1.35;
        private const double velocity_change_multiplier = 0.75;

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

            double currStrainTime = withSliderTravelDistance ? osuCurrObj.MinimumJumpTime : osuCurrObj.TapStrainTime;

            double lastStrainTime = withSliderTravelDistance ? (double)osuCurrObj.PrevMinimumJumpTime! : (double)osuCurrObj.PrevTapStrainTime!;

            double truePrevStrainTime = withSliderTravelDistance ? osuLastObj.MinimumJumpTime : osuLastObj.TapStrainTime;

            double currDistance = withSliderTravelDistance ? osuCurrObj.LazyJumpDistance : osuCurrObj.SliderlessJumpDistance;
            double prevDistance = withSliderTravelDistance ? osuCurrObj.PrevLazyJumpDistance : osuCurrObj.PrevSliderlessJumpDistance;
            double truePrevDistance = withSliderTravelDistance ? osuLastObj.LazyJumpDistance : osuLastObj.SliderlessJumpDistance;

            if (withCheesability)
            {
                currStrainTime += osuCurrObj.ExtraDeltaTime;
                lastStrainTime += osuLastObj.ExtraDeltaTime;
            }

            // Calculate the velocity to the current hitobject, which starts with a base distance / time assuming the last object is a hitcircle.
            double currVelocity = currDistance / currStrainTime;


            // As above, do the same for the previous hitobject.
            double prevVelocity = prevDistance / lastStrainTime;

            // Used only for velocity change bonus to avoid certain buzz sliders being worth too much
            double truePrevVelocity = truePrevDistance / truePrevStrainTime;


            double wideAngleBonus = 0;
            double angleRepetitionNerf = 0;
            double sliderBonus = 0;
            double velocityChangeBonus = 0;

            double aimStrain = currVelocity; // Start strain with regular velocity.

            double? currAngle = withSliderTravelDistance ? osuCurrObj.Angle : osuCurrObj.SliderlessAngle;
            double? lastAngle = withSliderTravelDistance ? osuCurrObj.PrevAngle : osuCurrObj.PrevSliderlessAngle;
            double? trueLastAngle = withSliderTravelDistance ? osuLastObj.Angle : osuLastObj.SliderlessAngle;

            if (currAngle is not null && lastAngle is not null && osuLastObj.IsTapObject)
            {
                double currAngleValue = currAngle.Value;
                double lastAngleValue = lastAngle.Value;
                //double lastLastAngle = osuLastLastObj.Angle.Value;

                // Rewarding angles, take the smaller velocity as base.
                double angleBonus = Math.Min(currVelocity, prevVelocity);

                double wideAngleBase = Math.Min(currVelocity, prevVelocity);

                double baseFactor = 1 - 0.15 * DifficultyCalculationUtils.Smootherstep(currAngleValue, double.DegreesToRadians(90), double.DegreesToRadians(30)) * AngleDifference(currAngleValue, lastAngleValue);

                // Penalize acute angle repetition.
                angleRepetitionNerf = Math.Pow(baseFactor + (1 - baseFactor) * 0.95 * AngleVectorRepetition(osuCurrObj), 2);

                //angleRepetitionNerf *= 1 - DifficultyCalculationUtils.Smootherstep(currAngle, double.DegreesToRadians(90), double.DegreesToRadians(60));

                wideAngleBonus = calcWideAngleBonus(currAngleValue);

                wideAngleBase /= Math.Pow(Math.Max(osuLastObj.AdjustedDeltaTime, osuCurrObj.AdjustedDeltaTime), 2);

                // Apply full wide angle bonus for distance more than one diameter
                wideAngleBonus *= wideAngleBase * DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance, 0, diameter);

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

            if (!double.IsNaN(velocityChangeBonus))
            {
                aimStrain += velocityChangeBonus * 0.35;
                //Console.WriteLine($"velC = {velocityChangeBonus}");
            }




            // Add in acute angle bonus or wide angle bonus, whichever is larger.
            aimStrain += wideAngleBonus * 12000;

            //aimStrain += angleChangeBonus * 0.5;

            // Penalize angle repetition.
            aimStrain *= angleRepetitionNerf;

            //Console.WriteLine($"AngleChangeBonus = {angleChangeBonus}");

            // Apply high circle size bonus
            aimStrain *= osuCurrObj.SmallCircleBonus;

            // Add in additional slider velocity bonus.
            if (withSliderTravelDistance)
                aimStrain += sliderBonus * 0.3;


            if (double.IsNaN(aimStrain) || double.IsInfinity(aimStrain))
            {
                Console.WriteLine(

                    $"[SnapEval] Index={osuCurrObj.Index} " +
                    $"Type={osuCurrObj.BaseObject.GetType().Name} " +
                    $"LastType={osuLastObj.BaseObject.GetType().Name} " +
                    $"aimStrain={aimStrain} " +
                    $"velc={velocityChangeBonus} " +
                    $"currStrainTime={currStrainTime} " +
                    $"lastStrainTime={lastStrainTime} " +
                    $"lastTapStrainTime={osuLastObj.TapStrainTime} " +
                    $"tapStrainTime={osuCurrObj.TapStrainTime} " +
                    $"truePrevStrainTime={truePrevStrainTime} " +
                    $"withSliderTravelDistance={withSliderTravelDistance}"
                );
            }

            return aimStrain;
        }

        public static double AngleDifference(double curAngle, double lastAngle)
        {
            return Math.Cos(2 * Math.Min(Math.PI / 4, Math.Abs(curAngle - lastAngle)));
        }

        public static double AngleVectorRepetition(OsuDifficultyHitObject current)
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

                if (loopObj.VectorAngle.IsNotNull() && current.VectorAngle.IsNotNull())
                {
                    double angleDifference = Math.Abs(current.VectorAngle.Value - loopObj.VectorAngle.Value);
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
