// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        private bool usingClassicSliderAccuracy;
        private bool usingScoreV2;

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        /// <summary>
        /// Missed slider ticks that includes missed reverse arrows. Will only be correct on non-classic scores
        /// </summary>
        private int countSliderTickMiss;

        /// <summary>
        /// Amount of missed slider tails that don't break combo. Will only be correct on non-classic scores
        /// </summary>
        private int countSliderEndsDropped;

        /// <summary>
        /// Estimated total amount of combo breaks
        /// </summary>
        private double effectiveMissCount;

        private double clockRate;
        private double greatHitWindow;
        private double okHitWindow;
        private double mehHitWindow;
        private double overallDifficulty;
        private double approachRate;

        private double? deviation;
        private double? speedDeviation;
        private double? fingerControlDeviation;

        private double aimEstimatedSliderBreaks;
        private double speedEstimatedSliderBreaks;

        public OsuPerformanceCalculator()
            : base(new OsuRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var osuAttributes = (OsuDifficultyAttributes)attributes;

            usingClassicSliderAccuracy = score.Mods.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value);
            usingScoreV2 = score.Mods.Any(m => m is ModScoreV2);

            accuracy = score.Accuracy;
            scoreMaxCombo = score.MaxCombo;
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            countSliderEndsDropped = osuAttributes.SliderCount - score.Statistics.GetValueOrDefault(HitResult.SliderTailHit);
            countSliderTickMiss = score.Statistics.GetValueOrDefault(HitResult.LargeTickMiss);
            effectiveMissCount = countMiss;

            var difficulty = score.BeatmapInfo!.Difficulty.Clone();

            score.Mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            clockRate = ModUtils.CalculateRateWithMods(score.Mods);

            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(difficulty.OverallDifficulty);

            greatHitWindow = hitWindows.WindowFor(HitResult.Great) / clockRate;
            okHitWindow = hitWindows.WindowFor(HitResult.Ok) / clockRate;
            mehHitWindow = hitWindows.WindowFor(HitResult.Meh) / clockRate;

            double preempt = IBeatmapDifficultyInfo.DifficultyRange(difficulty.ApproachRate, 1800, 1200, 450) / clockRate;

            overallDifficulty = (80 - greatHitWindow) / 6;
            approachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5;

            double comboBasedEstimatedMissCount = calculateComboBasedEstimatedMissCount(osuAttributes);
            double? scoreBasedEstimatedMissCount = null;

            if (usingClassicSliderAccuracy && score.LegacyTotalScore != null)
            {
                var legacyScoreMissCalculator = new OsuLegacyScoreMissCalculator(score, osuAttributes);
                scoreBasedEstimatedMissCount = legacyScoreMissCalculator.Calculate();

                effectiveMissCount = scoreBasedEstimatedMissCount.Value;
            }
            else
            {
                // Use combo-based miss count if this isn't a legacy score
                effectiveMissCount = comboBasedEstimatedMissCount;
            }

            effectiveMissCount = Math.Max(countMiss, effectiveMissCount);
            effectiveMissCount = Math.Min(totalHits, effectiveMissCount);

            double multiplier = OsuDifficultyCalculator.CalculateDifficultyMultiplier(score.Mods, totalHits, osuAttributes.SpinnerCount);

            if (score.Mods.Any(m => m is OsuModNoFail))
                multiplier *= Math.Max(0.90, 1.0 - 0.02 * effectiveMissCount);

            if (score.Mods.Any(h => h is OsuModRelax))
            {
                // https://www.desmos.com/calculator/vspzsop6td
                // we use OD13.3 as maximum since it's the value at which great hitwidow becomes 0
                // this is well beyond currently maximum achievable OD which is 12.17 (DTx2 + DA with OD11)
                double okMultiplier = 0.75 * Math.Max(0.0, overallDifficulty > 0.0 ? 1 - overallDifficulty / 13.33 : 1.0);
                double mehMultiplier = Math.Max(0.0, overallDifficulty > 0.0 ? 1 - Math.Pow(overallDifficulty / 13.33, 5) : 1.0);

                // As we're adding Oks and Mehs to an approximated number of combo breaks the result can be higher than total hits in specific scenarios (which breaks some calculations) so we need to clamp it.
                effectiveMissCount = Math.Min(effectiveMissCount + countOk * okMultiplier + countMeh * mehMultiplier, totalHits);
            }


            speedDeviation = calculateSpeedDeviation(osuAttributes);
            deviation = calculateDeviation(score, osuAttributes);

            double aimValue = computeAimValue(score, osuAttributes);
            double speedValue = computeSpeedValue(score, osuAttributes);
            double accuracyValue = computeAccuracyValue(score, osuAttributes);
            double flashlightValue = computeFlashlightValue(score, osuAttributes);
            double fingerControlValue = computeFingerControlValue(score, osuAttributes);

            double totalValue =
                Math.Pow(
                    Math.Pow(aimValue, 1.1) +
                    Math.Pow(speedValue, 1.1) +
                    Math.Pow(accuracyValue, 1.1) +
                    Math.Pow(fingerControlValue, 1.1) +
                    Math.Pow(flashlightValue, 1.1), 1.0 / 1.1
                ) * multiplier;

            return new OsuPerformanceAttributes
            {
                Aim = aimValue,
                Speed = speedValue,
                Accuracy = accuracyValue,
                FingerControl = fingerControlValue,
                Flashlight = flashlightValue,
                EffectiveMissCount = effectiveMissCount,
                ComboBasedEstimatedMissCount = comboBasedEstimatedMissCount,
                ScoreBasedEstimatedMissCount = scoreBasedEstimatedMissCount,
                AimEstimatedSliderBreaks = aimEstimatedSliderBreaks,
                SpeedEstimatedSliderBreaks = speedEstimatedSliderBreaks,
                SpeedDeviation = speedDeviation,
                Total = totalValue
            };
        }

        private double computeAimValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModAutopilot) || deviation == null)
                return 0.0;

            double aimDifficulty = attributes.AimDifficulty;

            if (attributes.SliderCount > 0 && attributes.AimDifficultSliderCount > 0)
            {
                double estimateImproperlyFollowedDifficultSliders;

                if (usingClassicSliderAccuracy)
                {
                    // When the score is considered classic (regardless if it was made on old client or not) we consider all missing combo to be dropped difficult sliders
                    int maximumPossibleDroppedSliders = totalImperfectHits;
                    estimateImproperlyFollowedDifficultSliders = Math.Clamp(Math.Min(maximumPossibleDroppedSliders, attributes.MaxCombo - scoreMaxCombo), 0, attributes.AimDifficultSliderCount);
                }
                else
                {
                    // We add tick misses here since they too mean that the player didn't follow the slider properly
                    // We however aren't adding misses here because missing slider heads has a harsh penalty by itself and doesn't mean that the rest of the slider wasn't followed properly
                    estimateImproperlyFollowedDifficultSliders = Math.Clamp(countSliderEndsDropped + countSliderTickMiss, 0, attributes.AimDifficultSliderCount);
                }

                double sliderNerfFactor = (1 - attributes.SliderFactor) * Math.Pow(1 - estimateImproperlyFollowedDifficultSliders / attributes.AimDifficultSliderCount, 3) + attributes.SliderFactor;
                aimDifficulty *= sliderNerfFactor;
            }

            double aimValue = OsuStrainSkill.DifficultyToPerformance(aimDifficulty);

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            aimValue *= lengthBonus;

            if (effectiveMissCount > 0)
            {
                aimEstimatedSliderBreaks = calculateEstimatedSliderBreaks(attributes.AimTopWeightedSliderFactor, attributes);
                aimValue *= calculateMissPenalty(effectiveMissCount + aimEstimatedSliderBreaks, attributes.AimDifficultStrainCount);
            }

            // TC bonuses are excluded when blinds is present as the increased visual difficulty is unimportant when notes cannot be seen.
            if (score.Mods.Any(m => m is OsuModBlinds))
                aimValue *= 1.3 + (totalHits * (0.0016 / (1 + 2 * effectiveMissCount)) * Math.Pow(accuracy, 16)) * (1 - 0.003 * attributes.DrainRate * attributes.DrainRate);
            else if (score.Mods.Any(m => m is OsuModTraceable))
            {
                aimValue *= 1.0 + OsuDifficultyCalculator.CalculateVisibilityBonus(score.Mods, approachRate);
            }

            // Scale the aim value by how cheesable it is.
            double cheesedAimValue = aimValue * Math.Pow(attributes.CheeseFactor, 3);
            aimValue = double.Lerp(cheesedAimValue, aimValue, DifficultyCalculationUtils.Erf(20 / (Math.Sqrt(2) * (double)deviation)));

            return aimValue;
        }

        private double computeSpeedValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || speedDeviation == null)
                return 0.0;

            double speedValue = OsuStrainSkill.DifficultyToPerformance(attributes.SpeedDifficulty);

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            speedValue *= lengthBonus;

            if (effectiveMissCount > 0)
            {
                speedEstimatedSliderBreaks = calculateEstimatedSliderBreaks(attributes.SpeedTopWeightedSliderFactor, attributes);
                speedValue *= calculateMissPenalty(effectiveMissCount + speedEstimatedSliderBreaks, attributes.SpeedDifficultStrainCount);
            }

            // TC bonuses are excluded when blinds is present as the increased visual difficulty is unimportant when notes cannot be seen.
            if (score.Mods.Any(m => m is OsuModBlinds))
            {
                // Increasing the speed value by object count for Blinds isn't ideal, so the minimum buff is given.
                speedValue *= 1.12;
            }
            else if (score.Mods.Any(m => m is OsuModTraceable))
            {
                speedValue *= 1.0 + OsuDifficultyCalculator.CalculateVisibilityBonus(score.Mods, approachRate);
            }

            double speedHighDeviationMultiplier = calculateSpeedHighDeviationNerf(attributes);
            speedValue *= speedHighDeviationMultiplier;

            // Calculate accuracy assuming the worst case scenario
            double relevantTotalDiff = Math.Max(0, totalHits - attributes.SpeedNoteCount);
            double relevantCountGreat = Math.Max(0, countGreat - relevantTotalDiff);
            double relevantCountOk = Math.Max(0, countOk - Math.Max(0, relevantTotalDiff - countGreat));
            double relevantCountMeh = Math.Max(0, countMeh - Math.Max(0, relevantTotalDiff - countGreat - countOk));
            double relevantAccuracy = attributes.SpeedNoteCount == 0 ? 0 : (relevantCountGreat * 6.0 + relevantCountOk * 2.0 + relevantCountMeh) / (attributes.SpeedNoteCount * 6.0);

            // An effective hit window is created based on the speed SR. The higher the speed difficulty, the shorter the hit window.
            // For example, a speed SR of 3.0 leads to an effective hit window of 20ms, which is OD 10.
            double effectiveHitWindow = Math.Sqrt(20 * 60 / attributes.SpeedDifficulty);

            // Find the proportion of 300s on speed notes assuming the hit window was the effective hit window.
            double effectiveAccuracy = DifficultyCalculationUtils.Erf(effectiveHitWindow / (double)speedDeviation);

            // Scale speed value by normalized accuracy.
            speedValue *= Math.Pow(effectiveAccuracy, 2);

            return speedValue;
        }

        private double computeFingerControlValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || speedDeviation == null)
                return 0.0;

            double fingerControlValue = OsuStrainSkill.DifficultyToPerformance(attributes.FingerControlDifficulty);

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            fingerControlValue *= lengthBonus;

            if (effectiveMissCount > 0)
            {
                fingerControlValue *= calculateMissPenalty(effectiveMissCount + speedEstimatedSliderBreaks, attributes.FingerControlDifficultStrainCount);
            }

            // TC bonuses are excluded when blinds is present as the increased visual difficulty is unimportant when notes cannot be seen.
            if (score.Mods.Any(m => m is OsuModBlinds))
            {
                // Increasing the speed value by object count for Blinds isn't ideal, so the minimum buff is given.
                fingerControlValue *= 1.12;
            }
            else if (score.Mods.Any(m => m is OsuModTraceable))
            {
                fingerControlValue *= 1.0 + OsuDifficultyCalculator.CalculateVisibilityBonus(score.Mods, approachRate);
            }

            // Calculate accuracy assuming the worst case scenario
            double relevantTotalDiff = Math.Max(0, totalHits - attributes.FingerControlNoteCount);
            double relevantCountGreat = Math.Max(0, countGreat - relevantTotalDiff);
            double relevantCountOk = Math.Max(0, countOk - Math.Max(0, relevantTotalDiff - countGreat));
            double relevantCountMeh = Math.Max(0, countMeh - Math.Max(0, relevantTotalDiff - countGreat - countOk));
            double relevantAccuracy = attributes.FingerControlNoteCount == 0 ? 0 : (relevantCountGreat * 6.0 + relevantCountOk * 2.0 + relevantCountMeh) / (attributes.SpeedNoteCount * 6.0);

            // An effective hit window is created based on the speed SR. The higher the speed difficulty, the shorter the hit window.
            // For example, a speed SR of 3.0 leads to an effective hit window of 20ms, which is OD 10.
            double effectiveHitWindow = Math.Sqrt(20 * 60 / attributes.SpeedDifficulty);

            // Find the proportion of 300s on speed notes assuming the hit window was the effective hit window.
            double effectiveAccuracy = DifficultyCalculationUtils.Erf(effectiveHitWindow / (double)speedDeviation);

            // Scale speed value by normalized accuracy.
            fingerControlValue *= Math.Pow(effectiveAccuracy, 2);

            return fingerControlValue;
        }

        private double computeAccuracyValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || deviation == null)
                return 0.0;

            double fingerControlDiff = attributes.FingerControlDifficulty;

            double accuracyValue = 120 * Math.Pow(7.5 / (double)deviation, 2);

            double fingerControlNerf = 0.05 + 0.95 * Math.Clamp(fingerControlDiff / 1.1, 0, 1);
            accuracyValue *= fingerControlNerf;

            // Increasing the accuracy value by object count for Blinds isn't ideal, so the minimum buff is given.
            if (score.Mods.Any(m => m is OsuModBlinds))
                accuracyValue *= 1.14;
            else if (score.Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
            {
                // Decrease bonus for AR > 10
                accuracyValue *= 1 + 0.08 * Math.Clamp((11.5 - approachRate) / (11.5 - 10), 0, 1);
            }

            if (score.Mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            return accuracyValue;
        }

        private double computeFlashlightValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (!score.Mods.Any(h => h is OsuModFlashlight))
                return 0.0;

            double flashlightValue = Flashlight.DifficultyToPerformance(attributes.FlashlightDifficulty);

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                flashlightValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            flashlightValue *= getComboScalingFactor(attributes);

            // Scale the flashlight value with accuracy _slightly_.
            flashlightValue *= 0.5 + accuracy / 2.0;

            return flashlightValue;
        }

        private double calculateComboBasedEstimatedMissCount(OsuDifficultyAttributes attributes)
        {
            if (attributes.SliderCount <= 0)
                return countMiss;

            double missCount = countMiss;

            if (usingClassicSliderAccuracy)
            {
                // Consider that full combo is maximum combo minus dropped slider tails since they don't contribute to combo but also don't break it
                // In classic scores we can't know the amount of dropped sliders so we estimate to 10% of all sliders on the map
                double fullComboThreshold = attributes.MaxCombo - 0.1 * attributes.SliderCount;

                if (scoreMaxCombo < fullComboThreshold)
                    missCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);

                // In classic scores there can't be more misses than a sum of all non-perfect judgements
                missCount = Math.Min(missCount, totalImperfectHits);
            }
            else
            {
                double fullComboThreshold = attributes.MaxCombo - countSliderEndsDropped;

                if (scoreMaxCombo < fullComboThreshold)
                    missCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);

                // Combine regular misses with tick misses since tick misses break combo as well
                missCount = Math.Min(missCount, countSliderTickMiss + countMiss);
            }

            return missCount;
        }

        private double calculateEstimatedSliderBreaks(double topWeightedSliderFactor, OsuDifficultyAttributes attributes)
        {
            if (!usingClassicSliderAccuracy || countOk == 0)
                return 0;

            double missedComboPercent = 1.0 - (double)scoreMaxCombo / attributes.MaxCombo;
            double estimatedSliderBreaks = Math.Min(countOk, effectiveMissCount * topWeightedSliderFactor);

            // scores with more oks are more likely to have slider breaks
            double okAdjustment = ((countOk - estimatedSliderBreaks) + 0.5) / countOk;

            // There is a low probability of extra slider breaks on effective miss counts close to 1, as score based calculations are good at indicating if only a single break occurred.
            estimatedSliderBreaks *= DifficultyCalculationUtils.Smoothstep(effectiveMissCount, 1, 2);

            return estimatedSliderBreaks * okAdjustment * DifficultyCalculationUtils.Logistic(missedComboPercent, 0.33, 15);
        }

        /// <summary>
        /// Estimates player's deviation on speed notes.
        /// Treats all speed notes as hit circles.
        /// </summary>
        private double? calculateSpeedDeviation(OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return null;

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
                double deviationOnCircles = greatHitWindow / (Math.Sqrt(2) * DifficultyCalculationUtils.ErfInv(greatProbabilityCircle));

                // Then compute the variance for 50s.
                double mehVariance = (mehHitWindow * mehHitWindow + okHitWindow * mehHitWindow + okHitWindow * okHitWindow) / 3;

                // Find the total deviation.
                deviationOnCircles = Math.Sqrt(((relevantCountGreat + relevantCountOk) * Math.Pow(deviationOnCircles, 2) + relevantCountMeh * mehVariance) / (relevantCountGreat + relevantCountOk + relevantCountMeh));

                return deviationOnCircles;
            }

            return null;
        }

        /// <summary>
        /// Estimates the player's tap deviation based on the OD, given number of greats, oks, mehs and misses,
        /// assuming the player's mean hit error is 0. The estimation is consistent in that two SS scores on the same map with the same settings
        /// will always return the same deviation. Misses are ignored because they are usually due to misaiming.
        /// This method actually gives an upper bound for deviation; i.e. we can be 99% confident that deviation is below this value.
        /// This is so long maps can be less harshly nerfed.
        /// Greats and oks are assumed to follow a normal distribution, whereas mehs are assumed to follow a uniform distribution.
        /// </summary>
        private double? calculateDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return null;

            const double z = 2.32634787404; // 99% critical value for the normal distribution (one-tailed).

            if (score.Mods.Any(m => m is OsuModClassic))
            {
                int circleCount = attributes.HitCircleCount;
                int missCountCircles = Math.Min(countMiss, circleCount);
                int mehCountCircles = Math.Min(countMeh, circleCount - missCountCircles);
                int okCountCircles = Math.Min(countOk, circleCount - missCountCircles - mehCountCircles);
                int greatCountCircles = Math.Max(0, circleCount - missCountCircles - mehCountCircles - okCountCircles);

                // Assume 100s, 50s, and misses happen on circles. If there are less non-300s on circles than 300s,
                // compute the deviation on circles.
                if (greatCountCircles > 0)
                {
                    double n = circleCount - missCountCircles - mehCountCircles;

                    // Proportion of greats hit on circles, ignoring misses and 50s.
                    double p = greatCountCircles / n;

                    // We can be 99% confident that p is at least this value.
                    double pLowerBound = (n * p + z * z / 2) / (n + z * z) - z / (n + z * z) * Math.Sqrt(n * p * (1 - p) + z * z / 4);

                    // Compute the deviation assuming 300s and 100s are normally distributed, and 50s are uniformly distributed.
                    // Begin with 300s and 100s first. Ignoring 50s, we can be 99% confident that the deviation is not higher than:
                    double deviationOnCircles = greatHitWindow / (Math.Sqrt(2) * DifficultyCalculationUtils.ErfInv(pLowerBound));
                    double adjustFor100 = Math.Sqrt(2 / Math.PI) * okHitWindow * Math.Exp(-0.5 * Math.Pow(okHitWindow / deviationOnCircles, 2)) / (deviationOnCircles * DifficultyCalculationUtils.Erf(okHitWindow / (Math.Sqrt(2) * deviationOnCircles)));

                    deviationOnCircles *= Math.Sqrt(1 - adjustFor100);

                    // Value deviation approach as greatCount approaches 0
                    double limitValue = okHitWindow / Math.Sqrt(3);

                    // If precision is not enough to compute true deviation - use limit value
                    if (pLowerBound == 0 || adjustFor100 >= 1 || deviation > limitValue)
                        deviationOnCircles = limitValue;

                    // Then compute the variance for 50s.
                    double mehVariance = (mehHitWindow * mehHitWindow + okHitWindow * mehHitWindow + okHitWindow * okHitWindow) / 3;

                    // Find the total deviation.
                    deviationOnCircles = Math.Sqrt(((greatCountCircles + okCountCircles) * Math.Pow(deviationOnCircles, 2) + mehCountCircles * mehVariance) / (greatCountCircles + okCountCircles + mehCountCircles));

                    return deviationOnCircles;
                }

                // If there are more non-300s than there are circles, compute the deviation on sliders instead.
                // Here, all that matters is whether or not the slider was missed, since it is impossible
                // to get a 100 or 50 on a slider by mis-tapping it.
                int sliderCount = attributes.SliderCount;
                int missCountSliders = Math.Min(sliderCount, countMiss - missCountCircles);
                int greatCountSliders = sliderCount - missCountSliders;

                // We only get here if nothing was hit. In this case, there is no estimate for deviation.
                // Note that this is never negative, so checking if this is only equal to 0 makes sense.
                if (greatCountSliders == 0)
                {
                    return null;
                }

                double greatProbabilitySlider = greatCountSliders / (sliderCount + 1.0);
                double deviationOnSliders = mehHitWindow / (Math.Sqrt(2) * DifficultyCalculationUtils.ErfInv(greatProbabilitySlider));

                return deviationOnSliders;
            }
            else
            {
                double n = countGreat + countOk;

                // Proportion of greats hit on circles, ignoring misses and 50s.
                double p = countGreat / n;

                // We can be 99% confident that p is at least this value.
                double pLowerBound = (n * p + z * z / 2) / (n + z * z) - z / (n + z * z) * Math.Sqrt(n * p * (1 - p) + z * z / 4);

                // Compute the deviation assuming 300s and 100s are normally distributed, and 50s are uniformly distributed.
                // Begin with 300s and 100s first. Ignoring 50s, we can be 99% confident that the deviation is not higher than:
                double deviation = greatHitWindow / (Math.Sqrt(2) * DifficultyCalculationUtils.ErfInv(pLowerBound));
                double adjustFor100 = Math.Sqrt(2 / Math.PI) * okHitWindow * Math.Exp(-0.5 * Math.Pow(okHitWindow / deviation, 2)) / (deviation * DifficultyCalculationUtils.Erf(okHitWindow / (Math.Sqrt(2) * deviation)));

                deviation *= Math.Sqrt(1 - adjustFor100);

                // Value deviation approach as greatCount approaches 0
                double limitValue = okHitWindow / Math.Sqrt(3);

                // If precision is not enough to compute true deviation - use limit value
                if (pLowerBound == 0 || adjustFor100 >= 1 || deviation > limitValue)
                    deviation = limitValue;

                // Then compute the variance for 50s.
                double mehVariance = (mehHitWindow * mehHitWindow + okHitWindow * mehHitWindow + okHitWindow * okHitWindow) / 3;

                // Find the total deviation.
                deviation = Math.Sqrt(((countGreat + countOk) * Math.Pow(deviation, 2) + countMeh * mehVariance) / (countGreat + countOk + countMeh));

                return deviation;
            }
        }

        // Calculates multiplier for speed to account for improper tapping based on the deviation and speed difficulty
        // https://www.desmos.com/calculator/dmogdhzofn
        private double calculateSpeedHighDeviationNerf(OsuDifficultyAttributes attributes)
        {
            if (speedDeviation == null)
                return 0;

            double speedValue = OsuStrainSkill.DifficultyToPerformance(attributes.SpeedDifficulty);

            // Decides a point where the PP value achieved compared to the speed deviation is assumed to be tapped improperly. Any PP above this point is considered "excess" speed difficulty.
            // This is used to cause PP above the cutoff to scale logarithmically towards the original speed value thus nerfing the value.
            double excessSpeedDifficultyCutoff = 100 + 220 * Math.Pow(22 / speedDeviation.Value, 6.5);

            if (speedValue <= excessSpeedDifficultyCutoff)
                return 1.0;

            const double scale = 50;
            double adjustedSpeedValue = scale * (Math.Log((speedValue - excessSpeedDifficultyCutoff) / scale + 1) + excessSpeedDifficultyCutoff / scale);

            // 220 UR and less are considered tapped correctly to ensure that normal scores will be punished as little as possible
            double lerp = 1 - DifficultyCalculationUtils.ReverseLerp(speedDeviation.Value, 22.0, 27.0);
            adjustedSpeedValue = double.Lerp(adjustedSpeedValue, speedValue, lerp);

            return adjustedSpeedValue / speedValue;
        }

        // Miss penalty assumes that a player will miss on the hardest parts of a map,
        // so we use the amount of relatively difficult sections to adjust miss penalty
        // to make it more punishing on maps with lower amount of hard sections.
        private double calculateMissPenalty(double missCount, double difficultStrainCount) => 0.96 / ((missCount / (4 * Math.Pow(Math.Log(difficultStrainCount), 0.94))) + 1);
        private double getComboScalingFactor(OsuDifficultyAttributes attributes) => attributes.MaxCombo <= 0 ? 1.0 : Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(attributes.MaxCombo, 0.8), 1.0);

        private int totalHits => countGreat + countOk + countMeh + countMiss;
        private int totalSuccessfulHits => countGreat + countOk + countMeh;
        private int totalImperfectHits => countOk + countMeh + countMiss;
    }
}
