// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public const double PERFORMANCE_BASE_MULTIPLIER = 1.14; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things.

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        private double effectiveMissCount;

        private double hitWindow300;
        private double deviation, speedDeviation;

        public OsuPerformanceCalculator()
            : base(new OsuRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var osuAttributes = (OsuDifficultyAttributes)attributes;

            accuracy = score.Accuracy;
            scoreMaxCombo = score.MaxCombo;
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            effectiveMissCount = calculateEffectiveMissCount(osuAttributes);

            double multiplier = PERFORMANCE_BASE_MULTIPLIER;

            if (score.Mods.Any(m => m is OsuModNoFail))
                multiplier *= Math.Max(0.90, 1.0 - 0.02 * effectiveMissCount);

            if (score.Mods.Any(m => m is OsuModSpunOut) && totalHits > 0)
                multiplier *= 1.0 - Math.Pow((double)osuAttributes.SpinnerCount / totalHits, 0.85);

            if (score.Mods.Any(h => h is OsuModRelax))
            {
                // https://www.desmos.com/calculator/bc9eybdthb
                // we use OD13.3 as maximum since it's the value at which great hitwidow becomes 0
                // this is well beyond currently maximum achievable OD which is 12.17 (DTx2 + DA with OD11)
                double okMultiplier = Math.Max(0.0, osuAttributes.OverallDifficulty > 0.0 ? 1 - Math.Pow(osuAttributes.OverallDifficulty / 13.33, 1.8) : 1.0);
                double mehMultiplier = Math.Max(0.0, osuAttributes.OverallDifficulty > 0.0 ? 1 - Math.Pow(osuAttributes.OverallDifficulty / 13.33, 5) : 1.0);

                // As we're adding Oks and Mehs to an approximated number of combo breaks the result can be higher than total hits in specific scenarios (which breaks some calculations) so we need to clamp it.
                effectiveMissCount = Math.Min(effectiveMissCount + countOk * okMultiplier + countMeh * mehMultiplier, totalHits);
            }

            double clockRate = getClockRate(score);
            hitWindow300 = 80 - 6 * osuAttributes.OverallDifficulty;

            deviation = calculateEffectiveDeviation(score, osuAttributes);
            speedDeviation = calculateSpeedDeviation(score, osuAttributes);

            double aimValue = computeAimValue(score, osuAttributes);
            double speedValue = computeSpeedValue(score, osuAttributes);
            double accuracyValue = computeAccuracyValue(score);
            double flashlightValue = computeFlashlightValue(score, osuAttributes);

            double totalValue =
                Math.Pow(
                    Math.Pow(aimValue, 1.1) +
                    Math.Pow(speedValue, 1.1) +
                    Math.Pow(accuracyValue, 1.1) +
                    Math.Pow(flashlightValue, 1.1), 1.0 / 1.1
                ) * multiplier;

            return new OsuPerformanceAttributes
            {
                Aim = aimValue,
                Speed = speedValue,
                Accuracy = accuracyValue,
                Flashlight = flashlightValue,
                EffectiveMissCount = effectiveMissCount,
                EffectiveDeviation = deviation,
                SpeedDeviation = speedDeviation,
                Total = totalValue
            };
        }

        private double computeAimValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            double aimValue = Math.Pow(5.0 * Math.Max(1.0, attributes.AimDifficulty / 0.0675) - 4.0, 3.0) / 100000.0;

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            aimValue *= lengthBonus;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                aimValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), effectiveMissCount);

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 31 / 3.0)
                approachRateFactor = 0.05 * (attributes.ApproachRate - 31 / 3.0);
            else if (attributes.ApproachRate < 8.0)
                approachRateFactor = 0.05 * (8.0 - attributes.ApproachRate);

            if (score.Mods.Any(h => h is OsuModRelax))
                approachRateFactor = 0.0;

            aimValue *= 1.0 + approachRateFactor * lengthBonus; // Buff for longer maps with high AR.

            if (score.Mods.Any(m => m is OsuModBlinds))
                aimValue *= 1.3 + (totalHits * (0.0016 / (1 + 2 * effectiveMissCount)) * Math.Pow(accuracy, 16)) * (1 - 0.003 * attributes.DrainRate * attributes.DrainRate);
            else if (score.Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                aimValue *= 1.0 + 0.04 * (12 - attributes.ApproachRate);
            }

            // We assume 15% of sliders in a map are difficult since there's no way to tell from the performance calculator.
            double estimateDifficultSliders = attributes.SliderCount * 0.15;

            if (attributes.SliderCount > 0)
            {
                double estimateSliderEndsDropped = Math.Clamp(Math.Min(countOk + countMeh + countMiss, attributes.MaxCombo - scoreMaxCombo), 0, estimateDifficultSliders);
                double sliderNerfFactor = (1 - attributes.SliderFactor) * Math.Pow(1 - estimateSliderEndsDropped / estimateDifficultSliders, 3) + attributes.SliderFactor;
                aimValue *= sliderNerfFactor;
            }

            double aimDeviation = calculateAimDeviation(score, attributes);
            aimValue *= Math.Pow(SpecialFunctions.Erf(20 / aimDeviation), 1.5);
            aimValue *= 0.98 + Math.Pow(100.0 / 9, 2) / 2500; // OD 11.1 SS stays the same.

            return aimValue;
        }

        private double computeSpeedValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || speedDeviation == double.PositiveInfinity)
                return 0.0;

            double speedValue = Math.Pow(5.0 * Math.Max(1.0, attributes.SpeedDifficulty / 0.0675) - 4.0, 3.0) / 100000.0;

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            speedValue *= lengthBonus;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                speedValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 31 / 3.0)
                approachRateFactor = 0.05 * (attributes.ApproachRate - 31 / 3.0);

            speedValue *= 1.0 + approachRateFactor * lengthBonus; // Buff for longer maps with high AR.

            if (score.Mods.Any(m => m is OsuModBlinds))
            {
                // Increasing the speed value by object count for Blinds isn't ideal, so the minimum buff is given.
                speedValue *= 1.12;
            }
            else if (score.Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                speedValue *= 1.0 + 0.04 * (12 - attributes.ApproachRate);
            }

            speedValue *= Math.Pow(SpecialFunctions.Erf(20 / speedDeviation), 1.5);
            speedValue *= 0.95 + Math.Pow(100.0 / 9, 2) / 750; // OD 11.1 SS stays the same.

            speedValue *= calculateSpeedRakeNerf(attributes);

            return speedValue;
        }

        private double computeAccuracyValue(ScoreInfo score)
        {
            if (score.Mods.Any(h => h is OsuModRelax))
                return 0.0;

            double accuracyValue = 120 * Math.Pow(7.5 / deviation, 2);

            // Increasing the accuracy value by object count for Blinds isn't ideal, so the minimum buff is given.
            if (score.Mods.Any(m => m is OsuModBlinds))
                accuracyValue *= 1.14;
            else if (score.Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
                accuracyValue *= 1.08;

            if (score.Mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            return accuracyValue;
        }

        private double computeFlashlightValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (!score.Mods.Any(h => h is OsuModFlashlight) || deviation == double.PositiveInfinity)
                return 0.0;

            double flashlightValue = Math.Pow(attributes.FlashlightDifficulty, 2.0) * 25.0;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                flashlightValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            flashlightValue *= getComboScalingFactor(attributes);

            // Account for shorter maps having a higher ratio of 0 combo/100 combo flashlight radius.
            flashlightValue *= 0.7 + 0.1 * Math.Min(1.0, totalHits / 200.0) +
                               (totalHits > 200 ? 0.2 * Math.Min(1.0, (totalHits - 200) / 200.0) : 0.0);

            // Scale the flashlight value with deviation
            flashlightValue *= SpecialFunctions.Erf(35 / deviation);
            flashlightValue *= 0.98 + Math.Pow(100.0 / 9, 2) / 2500;  // OD 11 SS stays the same.

            return flashlightValue;
        }

        private double calculateEffectiveMissCount(OsuDifficultyAttributes attributes)
        {
            // Guess the number of misses + slider breaks from combo
            double comboBasedMissCount = 0.0;

            if (attributes.SliderCount > 0)
            {
                double fullComboThreshold = attributes.MaxCombo - 0.1 * attributes.SliderCount;
                if (scoreMaxCombo < fullComboThreshold)
                    comboBasedMissCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);
            }

            // Clamp miss count to maximum amount of possible breaks
            comboBasedMissCount = Math.Min(comboBasedMissCount, countOk + countMeh + countMiss);

            return Math.Max(countMiss, comboBasedMissCount);
        }

        private double calculateAimDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            double hitWindow300 = 80 - 6 * attributes.OverallDifficulty;
            double aimDeviation = hitWindow300 / (Math.Sqrt(2) * SpecialFunctions.ErfInv(countGreat / (totalHits + 1.0)));
            double arBonusBegin = 8.0;

            if (attributes.ApproachRate < arBonusBegin)
            {
                double s = 1.0;
                double preempt(double ar) => ar > 5.0 ? 150 * (13 - ar) : 120 * (15 - ar);
                double arBonus(double ar) => Math.Min(1, 1 + s * (preempt(arBonusBegin) / preempt(ar) - 1));
                aimDeviation *= arBonus(attributes.ApproachRate);
            }

            return aimDeviation;
        }

        /// <summary>
        /// Using <see cref="calculateEffectiveDeviation"/> estimates player's deviation on speed notes, assuming worst-case.
        /// Treats all speed notes as hit circles. This is not good way to do this, but fixing this is impossible under the limitation of current speed pp.
        /// If score was set with slideracc - tries to remove mistaps on sliders from total mistaps.
        /// </summary>
        /// <summary>
        /// Does the same as <see cref="calculateEffectiveDeviation"/>, but only for notes and inaccuracies that are relevant to speed difficulty.
        /// Treats all difficult speed notes as circles, so this method can sometimes return a lower deviation than <see cref="calculateEffectiveDeviation"/>.
        /// This is fine though, since this method is only used to scale speed pp.
        /// </summary>
        private double calculateSpeedDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return double.PositiveInfinity;

            // Create a new track to properly calculate the hit windows of 100s and 50s.
            double clockRate = getClockRate(score);

            double hitWindow300 = 80 - 6 * attributes.OverallDifficulty;
            double hitWindow100 = (140 - 8 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;
            double hitWindow50 = (200 - 10 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;

            // Calculate accuracy assuming the worst case scenario
            double speedNoteCount = attributes.SpeedNoteCount;
            double relevantTotalDiff = totalHits - attributes.SpeedNoteCount;
            double relevantCountGreat = Math.Max(0, countGreat - relevantTotalDiff);
            double relevantCountOk = Math.Max(0, countOk - Math.Max(0, relevantTotalDiff - countGreat));
            double relevantCountMeh = Math.Max(0, countMeh - Math.Max(0, relevantTotalDiff - countGreat - countOk));
            double relevantCountMiss = Math.Max(0, countMiss - Math.Max(0, relevantTotalDiff - countGreat - countOk - countMeh));

            // Assume 100s, 50s, and misses happen on circles. If there are less non-300s on circles than 300s,
            // compute the deviation on circles.
            if (relevantCountGreat > 0)
            {
                // The probability that a player hits a circle is unknown, but we can estimate it to be
                // the number of greats on circles divided by the number of circles, and then add one
                // to the number of circles as a bias correction.
                double greatProbabilityCircle = relevantCountGreat / (speedNoteCount - relevantCountMiss - relevantCountMeh + 1.0);

                // Compute the deviation assuming 300s and 100s are normally distributed, and 50s are uniformly distributed.
                // Begin with the normal distribution first.
                double deviationOnCircles = hitWindow300 / (Math.Sqrt(2) * SpecialFunctions.ErfInv(greatProbabilityCircle));

                // Then compute the variance for 50s.
                double mehVariance = (hitWindow50 * hitWindow50 + hitWindow100 * hitWindow50 + hitWindow100 * hitWindow100) / 3;

                // Find the total deviation.
                deviationOnCircles = Math.Sqrt(((relevantCountGreat + relevantCountOk) * Math.Pow(deviationOnCircles, 2) + relevantCountMeh * mehVariance) / (relevantCountGreat + relevantCountOk + relevantCountMeh));

                return deviationOnCircles;
            }

            return double.PositiveInfinity;
        }

        /// <summary>
        /// Estimates the player's tap deviation based on the OD, given number of 300s, 100s, 50s and misses,
        /// assuming the player's mean hit error is 0. The estimation is consistent in that two SS scores on the same map with the same settings
        /// will always return the same deviation. Misses are ignored because they are usually due to misaiming.
        /// 300s and 100s are assumed to follow a normal distribution, whereas 50s are assumed to follow a uniform distribution.
        /// </summary>
        private double calculateEffectiveDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return double.PositiveInfinity;

            int circleCount = attributes.HitCircleCount;
            int missCountCircles = Math.Min(countMiss, circleCount);
            int mehCountCircles = Math.Min(countMeh, circleCount - missCountCircles);
            int okCountCircles = Math.Min(countOk, circleCount - missCountCircles - mehCountCircles);
            int greatCountCircles = Math.Max(0, circleCount - missCountCircles - mehCountCircles - okCountCircles);

            if (greatCountCircles == 0)
            {
                return double.PositiveInfinity;
            }

            double n = circleCount - missCountCircles - mehCountCircles;

            // We can be 99% confident that p is at least this value.
            double pLowerBound(double i) => Beta.InvCDF(i, 1 + n - i, 0.01);

            // If all rhythm difficulties were the same, this is the would-be deviation divided by the would-be SS deviation.
            // Always greater than or equal to 1.
            double ratio = SpecialFunctions.ErfInv(pLowerBound(n)) / SpecialFunctions.ErfInv(pLowerBound(greatCountCircles));

            return attributes.EffectiveSSDeviation * ratio;
        }

        // Calculates multiplier for speed accounting for rake based on the deviation and speed difficulty
        // https://www.desmos.com/calculator/puc1mzdtfv
        private double calculateSpeedRakeNerf(OsuDifficultyAttributes attributes)
        {
            // Base speed value
            double speedValue = OsuStrainSkill.DifficultyToPerformance(attributes.SpeedDifficulty);

            // Starting from this pp amount - penalty will be applied
            double abusePoint = 100 + 260 * Math.Pow(22 / speedDeviation, 5.8);

            if (speedValue <= abusePoint)
                return 1.0;

            // Use log curve to make additional rise in difficulty unimpactful. Rescale values to make curve have correct steepness
            const double scale = 50;
            double adjustedSpeedValue = scale * (Math.Log((speedValue - abusePoint) / scale + 1) + abusePoint / scale);

            return adjustedSpeedValue / speedValue;
        }

        private static double getClockRate(ScoreInfo score)
        {
            var track = new TrackVirtual(1);
            score.Mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            return track.Rate;
        }
        private double getComboScalingFactor(OsuDifficultyAttributes attributes) => attributes.MaxCombo <= 0 ? 1.0 : Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(attributes.MaxCombo, 0.8), 1.0);

        private int totalHits => countGreat + countOk + countMeh + countMiss;

        private int totalSuccessfulHits => countGreat + countOk + countMeh;
    }
}
