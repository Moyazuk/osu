// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;


namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AgilityEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = withSliderTravelDistance ? (OsuDifficultyHitObject)current.Previous(0) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(0);
            var osuLastLastObj = withSliderTravelDistance ? (OsuDifficultyHitObject)current.Previous(1) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(1);
            var osuLast2Obj = withSliderTravelDistance ? (OsuDifficultyHitObject)current.Previous(2) : (OsuDifficultyHitObject)osuCurrObj.PreviousTap(2);

            const float maxSliderRadius = OsuDifficultyHitObject.MAXIMUM_SLIDER_RADIUS;

            if (!(withSliderTravelDistance || osuCurrObj.IsTapObject || osuCurrObj.PrevTapStrainTime is not null))
                return 0;

            if (osuCurrObj.PrevMinimumJumpTime is null || osuPrevObj is null)
            {
                return 0;
            }


            double baseFactor = 1;
            double wideBonus = 1;

            double currStrainTime = withSliderTravelDistance ? osuCurrObj.MinimumJumpTime : osuCurrObj.TapStrainTime;
            double prevStrainTime = withSliderTravelDistance ? (double)osuCurrObj.PrevMinimumJumpTime! : (double)osuCurrObj.PrevTapStrainTime!;

            double? currAngle = withSliderTravelDistance ? osuCurrObj.Angle : osuCurrObj.SliderlessAngle;
            double? lastAngle = withSliderTravelDistance ? osuCurrObj.PrevAngle : osuCurrObj.PrevSliderlessAngle;

            double currDistanceMultiplier = Smootherstep(osuCurrObj.LazyJumpDistance / radius, 1, 2);
            double prevDistanceMultiplier = Smootherstep(osuPrevObj.LazyJumpDistance / radius, 1, 2);

            double angleBonus = 0;


            if (currAngle is not null && lastAngle is not null)
            {
                double currAngleValue = currAngle.Value;
                double lastAngleValue = lastAngle.Value;


                angleBonus = 0.35 * Smootherstep(currAngleValue, 0, double.DegreesToRadians(120));

                baseFactor = 1 - 0.25 * DifficultyCalculationUtils.Smoothstep(lastAngleValue, double.DegreesToRadians(90), double.DegreesToRadians(40)) * angleDifference(currAngleValue, lastAngleValue);
            }

            // Penalize angle repetition.
            double angleRepetitionNerf = Math.Pow(baseFactor + (1 - baseFactor) * angleVectorRepetition(osuCurrObj), 2);




            double distanceBonus = 0.00000000175 * Math.Pow(osuCurrObj.LazyJumpDistance, 3) *
                                   Smootherstep(MillisecondsToBPM(osuCurrObj.AdjustedDeltaTime, 2), 280, 320);

            // We reward high bpm more for wider angles, but only when both current and previous distance are over 0.5 radii.
            double baseBpm = 240.0 / (1 + (angleBonus + distanceBonus) * currDistanceMultiplier * prevDistanceMultiplier);

            // Agility bonus of 1 at base BPM.
            double agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currStrainTime, prevStrainTime), 2) / baseBpm, 4) - 1);

            double difficulty = agilityBonus * angleRepetitionNerf;

            if (!osuCurrObj.IsTapObject && osuCurrObj.LazyJumpDistance < maxSliderRadius)
            {
                difficulty *= 0.0045;
            }

            // Apply high circle size bonus
            if (osuCurrObj.IsTapObject)
                difficulty *= osuCurrObj.SmallCircleBonus;

            return difficulty * 0.575;
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
    }
}
