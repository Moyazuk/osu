// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public const double PERFORMANCE_BASE_MULTIPLIER = 1.12; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things.
        public const double PERFORMANCE_NORM_EXPONENT = 1.1;

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
        private double drainRate;

        private double? deviation;
        private double? speedDeviation;

        private SkillJudgements aimJudgements;
        private SkillJudgements speedJudgements;
        private SkillJudgements readingJudgements;
        private SkillJudgements accuracyJudgements;

        private double aimEstimatedSliderBreaks;
        private double speedEstimatedSliderBreaks;

        public static double DifficultyToPerformance(double difficulty) => 4.0 * DiffUtils.Pow(difficulty, 3);

        public OsuPerformanceCalculator()
            : base(new OsuRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var osuAttributes = (OsuDifficultyAttributes)attributes;

            usingClassicSliderAccuracy = score.Mods.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value);
            usingScoreV2 = score.Mods.Any(m => m is ModScoreV2);

            accuracy = Math.Clamp(score.Accuracy, 0, 1);
            scoreMaxCombo = Math.Clamp(score.MaxCombo, 0, osuAttributes.MaxCombo);
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

            approachRate = calculateRateAdjustedApproachRate(difficulty.ApproachRate, clockRate);
            overallDifficulty = (79.5 - greatHitWindow) / 6;
            drainRate = difficulty.DrainRate;

            double comboBasedEstimatedMissCount = calculateComboBasedEstimatedMissCount(osuAttributes);
            double? scoreBasedEstimatedMissCount = null;

            if (usingClassicSliderAccuracy && !usingScoreV2 && score.LegacyTotalScore > 0)
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
            effectiveMissCount = Math.Max(0, effectiveMissCount);

            if (effectiveMissCount > 0)
            {
                aimEstimatedSliderBreaks = calculateEstimatedSliderBreaks(osuAttributes.AimTopWeightedSliderFactor, osuAttributes);
                speedEstimatedSliderBreaks = calculateEstimatedSliderBreaks(osuAttributes.SpeedTopWeightedSliderFactor, osuAttributes);
            }

            double multiplier = PERFORMANCE_BASE_MULTIPLIER;

            if (score.Mods.Any(m => m is OsuModNoFail))
                multiplier *= Math.Max(0.90, 1.0 - 0.02 * effectiveMissCount);

            if (score.Mods.Any(m => m is OsuModSpunOut) && totalHits > 0)
                multiplier *= 1.0 - DiffUtils.Pow((double)osuAttributes.SpinnerCount / totalHits, 0.85);

            if (score.Mods.Any(h => h is OsuModRelax))
            {
                // https://www.desmos.com/calculator/vspzsop6td
                // we use OD13.3 as maximum since it's the value at which great hitwidow becomes 0
                // this is well beyond currently maximum achievable OD which is 12.17 (DTx2 + DA with OD11)
                double okMultiplier = 0.75 * Math.Max(0.0, overallDifficulty > 0.0 ? 1 - overallDifficulty / 13.33 : 1.0);
                double mehMultiplier = Math.Max(0.0, overallDifficulty > 0.0 ? 1 - DiffUtils.Pow(overallDifficulty / 13.33, 5) : 1.0);

                // As we're adding Oks and Mehs to an approximated number of combo breaks the result can be higher than total hits in specific scenarios (which breaks some calculations) so we need to clamp it.
                effectiveMissCount = Math.Min(effectiveMissCount + countOk * okMultiplier + countMeh * mehMultiplier, totalHits);
            }

            double flashlightValue = computeFlashlightValue(score, osuAttributes);

            assignJudgements(score, osuAttributes, flashlightValue);

            speedDeviation = calculateDeviation(speedJudgements);
            deviation = calculateAccuracyDeviation(accuracyJudgements);

            double aimValue = computeAimValue(score, osuAttributes, aimJudgements);
            double speedValue = computeSpeedValue(score, osuAttributes, speedJudgements);
            double accuracyValue = computeAccuracyValue(score, osuAttributes, accuracyJudgements);
            double readingValue = computeReadingValue(osuAttributes, readingJudgements);
            double cognitionValue = OsuDifficultyCalculator.SumCognitionDifficulty(readingValue, flashlightValue);

            double totalValue = DiffUtils.Norm(PERFORMANCE_NORM_EXPONENT, aimValue, speedValue, accuracyValue, cognitionValue) * multiplier;

            return new OsuPerformanceAttributes
            {
                Aim = aimValue,
                Speed = speedValue,
                Accuracy = accuracyValue,
                Flashlight = flashlightValue,
                Reading = readingValue,
                EffectiveMissCount = effectiveMissCount,
                ComboBasedEstimatedMissCount = comboBasedEstimatedMissCount,
                ScoreBasedEstimatedMissCount = scoreBasedEstimatedMissCount,
                AimEstimatedSliderBreaks = aimEstimatedSliderBreaks,
                SpeedEstimatedSliderBreaks = speedEstimatedSliderBreaks,
                SpeedDeviation = speedDeviation,
                AimJudgements = aimJudgements,
                SpeedJudgements = speedJudgements,
                ReadingJudgements = readingJudgements,
                AccuracyJudgements = accuracyJudgements,
                Total = totalValue
            };
        }

        private const int judgement_ok = 0;
        private const int judgement_meh = 1;
        private const int judgement_miss = 2;
        private const int judgement_type_count = 3;

        private const int skill_aim = 0;
        private const int skill_speed = 1;
        private const int skill_reading = 2;
        private const int skill_accuracy = 3;
        private const int skill_count = 4;

        /// <summary>
        /// Maximum number of chunks each judgement type is split into during the greedy assignment.
        /// </summary>
        private const int max_assignment_chunks = 20;

        /// <summary>
        /// Number of times the step size is halved during local refinement.
        /// </summary>
        private const int refinement_levels = 4;

        private const int max_refinement_iterations = 50;

        private const double epsilon = 1e-9;

        /// <summary>
        /// Distributes the score's oks, mehs and (effective) misses between aim, speed, reading and accuracy
        /// such that the resulting total performance is minimised. Each judgement is attributed to exactly one skill (fractionally),
        /// and a skill cannot receive more judgements than its relevant note count. Remaining capacity of each skill is filled with greats.
        /// </summary>
        /// <remarks>
        /// The objective is not convex, so this is a heuristic: a greedy pass (misses, then mehs, then oks) which assigns each chunk
        /// to the skill where it reduces total performance the most, followed by a local search which moves judgements between skills
        /// with progressively smaller step sizes while doing so keeps reducing total performance.
        /// </remarks>
        private void assignJudgements(ScoreInfo score, OsuDifficultyAttributes attributes, double flashlightValue)
        {
            double[] capacities = new double[skill_count];
            capacities[skill_aim] = paddedRelevantNoteCount(attributes.AimNoteCount);
            capacities[skill_speed] = paddedRelevantNoteCount(attributes.SpeedNoteCount);
            capacities[skill_reading] = paddedRelevantNoteCount(attributes.ReadingNoteCount);
            capacities[skill_accuracy] = accuracyObjectCount(attributes);

            double[] budgets = new double[judgement_type_count];
            budgets[judgement_ok] = countOk;
            budgets[judgement_meh] = countMeh;
            budgets[judgement_miss] = effectiveMissCount;

            double[,] allocation = new double[skill_count, judgement_type_count];
            double[] chunkSizes = new double[judgement_type_count];

            // Greedy pass, most impactful judgements first so that they get first pick of skill capacity.
            for (int type = judgement_type_count - 1; type >= 0; type--)
            {
                double remaining = budgets[type];

                if (remaining <= epsilon)
                    continue;

                int chunks = (int)Math.Ceiling(Math.Min(remaining, max_assignment_chunks));
                double chunkSize = remaining / chunks;
                chunkSizes[type] = chunkSize;

                while (remaining > epsilon)
                {
                    double amount = Math.Min(chunkSize, remaining);

                    int bestSkill = -1;
                    double bestPlaced = 0;
                    double bestTotal = double.PositiveInfinity;

                    for (int skill = 0; skill < skill_count; skill++)
                    {
                        double placed = Math.Min(amount, freeCapacity(skill));

                        if (placed <= epsilon)
                            continue;

                        allocation[skill, type] += placed;
                        double total = evaluate();
                        allocation[skill, type] -= placed;

                        if (total < bestTotal)
                        {
                            bestTotal = total;
                            bestSkill = skill;
                            bestPlaced = placed;
                        }
                    }

                    // No skill has any capacity left; the remaining judgements cannot be attributed anywhere.
                    if (bestSkill < 0)
                        break;

                    allocation[bestSkill, type] += bestPlaced;
                    remaining -= bestPlaced;
                }
            }

            // Local refinement: move judgements between skills while it lowers total performance.
            double currentTotal = evaluate();

            for (int level = 0; level < refinement_levels; level++)
            {
                bool improved = true;

                for (int iteration = 0; improved && iteration < max_refinement_iterations; iteration++)
                {
                    improved = false;

                    for (int type = 0; type < judgement_type_count; type++)
                    {
                        double delta = chunkSizes[type] / (1 << level);

                        if (delta <= epsilon)
                            continue;

                        for (int from = 0; from < skill_count; from++)
                        {
                            for (int to = 0; to < skill_count; to++)
                            {
                                if (from == to)
                                    continue;

                                double moved = Math.Min(delta, Math.Min(allocation[from, type], freeCapacity(to)));

                                if (moved <= epsilon)
                                    continue;

                                allocation[from, type] -= moved;
                                allocation[to, type] += moved;

                                double total = evaluate();

                                if (total < currentTotal - epsilon)
                                {
                                    currentTotal = total;
                                    improved = true;
                                }
                                else
                                {
                                    allocation[from, type] += moved;
                                    allocation[to, type] -= moved;
                                }
                            }
                        }
                    }
                }
            }

            aimJudgements = judgementsFor(skill_aim);
            speedJudgements = judgementsFor(skill_speed);
            readingJudgements = judgementsFor(skill_reading);
            accuracyJudgements = judgementsFor(skill_accuracy);

            double assigned(int skill) => allocation[skill, judgement_ok] + allocation[skill, judgement_meh] + allocation[skill, judgement_miss];

            double freeCapacity(int skill) => capacities[skill] - assigned(skill);

            SkillJudgements judgementsFor(int skill) => new SkillJudgements(
                Math.Max(0, capacities[skill] - assigned(skill)),
                allocation[skill, judgement_ok],
                allocation[skill, judgement_meh],
                allocation[skill, judgement_miss]);

            // The global multiplier is a positive constant, so it does not affect which assignment is minimal.
            double evaluate()
            {
                double aimValue = computeAimValue(score, attributes, judgementsFor(skill_aim));
                double speedValue = computeSpeedValue(score, attributes, judgementsFor(skill_speed));
                double accuracyValue = computeAccuracyValue(score, attributes, judgementsFor(skill_accuracy));
                double readingValue = computeReadingValue(attributes, judgementsFor(skill_reading));
                double cognitionValue = OsuDifficultyCalculator.SumCognitionDifficulty(readingValue, flashlightValue);

                return DiffUtils.Norm(PERFORMANCE_NORM_EXPONENT, aimValue, speedValue, accuracyValue, cognitionValue);
            }
        }

        /// <summary>
        /// The amount of objects judgements can be attributed to for a skill. Objects which aren't relevant to the skill
        /// still count for a tenth, so that skills with very few relevant notes can't be fully escaped.
        /// </summary>
        private double paddedRelevantNoteCount(double relevantNoteCount) => Math.Min(totalHits, relevantNoteCount);

        /// <summary>
        /// The amount of objects which are judged on timing.
        /// </summary>
        private int accuracyObjectCount(OsuDifficultyAttributes attributes)
        {
            int count = attributes.HitCircleCount;

            if (!usingClassicSliderAccuracy || usingScoreV2)
                count += attributes.SliderCount;

            return count;
        }

        private double computeAimValue(ScoreInfo score, OsuDifficultyAttributes attributes, SkillJudgements judgements)
        {
            if (score.Mods.Any(h => h is OsuModAutopilot))
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

                double sliderNerfFactor = (1 - attributes.SliderFactor) * DiffUtils.Pow(1 - estimateImproperlyFollowedDifficultSliders / attributes.AimDifficultSliderCount, 3) + attributes.SliderFactor;
                aimDifficulty *= sliderNerfFactor;
            }

            double aimValue = DifficultyToPerformance(aimDifficulty);

            double lengthBonus = 0.95 + 0.35 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            aimValue *= lengthBonus;

            double aimRelevantMissCount = Math.Min(judgements.Miss + aimEstimatedSliderBreaks, totalImperfectHits + countSliderTickMiss);

            if (aimRelevantMissCount > 0)
                aimValue *= calculateMissPenalty(aimRelevantMissCount, attributes.AimDifficultStrainCount);

            // TC bonuses are excluded when blinds is present as the increased visual difficulty is unimportant when notes cannot be seen.
            if (score.Mods.Any(m => m is OsuModBlinds))
                aimValue *= 1.3 + (totalHits * (0.0016 / (1 + 2 * effectiveMissCount)) * DiffUtils.Pow(accuracy, 16)) * (1 - 0.003 * drainRate * drainRate);
            else if (score.Mods.Any(m => m is OsuModTraceable))
            {
                aimValue *= 1.0 + calculateTraceableBonus(attributes.SliderFactor);
            }

            // Scale aim value by the deviation of the judgements attributed to aim.
            aimValue *= calculateDeviationScaling(calculateDeviation(judgements), 3);

            return aimValue;
        }

        private double computeSpeedValue(ScoreInfo score, OsuDifficultyAttributes attributes, SkillJudgements judgements)
        {
            if (score.Mods.Any(h => h is OsuModRelax))
                return 0.0;

            double? deviationOnSpeedNotes = calculateDeviation(judgements);

            if (deviationOnSpeedNotes == null)
                return 0.0;

            double speedDifficulty = attributes.SpeedDifficulty * calculateSpeedHighDeviationNerf(attributes, deviationOnSpeedNotes.Value);

            double speedValue = DifficultyToPerformance(speedDifficulty);

            double speedRelevantMissCount = Math.Min(judgements.Miss + speedEstimatedSliderBreaks, totalImperfectHits + countSliderTickMiss);

            if (speedRelevantMissCount > 0)
                speedValue *= calculateMissPenalty(speedRelevantMissCount, attributes.SpeedDifficultStrainCount);

            if (score.Mods.Any(m => m is OsuModBlinds))
            {
                // Increasing the speed value by object count for Blinds isn't ideal, so the minimum buff is given.
                speedValue *= 1.12;
            }

            // Scale speed value by normalized accuracy.
            speedValue *= calculateDeviationScaling(calculateDeviation(judgements), 3);

            return speedValue;
        }

        private double computeAccuracyValue(ScoreInfo score, OsuDifficultyAttributes attributes, SkillJudgements judgements)
        {
            if (score.Mods.Any(h => h is OsuModRelax))
                return 0.0;

            double? accuracyDeviation = calculateAccuracyDeviation(judgements);

            if (accuracyDeviation == null)
                return 0.0;

            // Only objects judged on timing are considered - in this part of the calculation we focus on hitting the timing hit window.
            int amountHitObjectsWithAccuracy = accuracyObjectCount(attributes);

            double accuracyValue = 170 * Math.Pow(7.5 / accuracyDeviation.Value, 1.4);

            // reduce acc pp for short maps
            accuracyValue *= Math.Min(1.0, Math.Pow(amountHitObjectsWithAccuracy / 1000.0, 0.3));

            // Increasing the accuracy value by object count for Blinds isn't ideal, so the minimum buff is given.
            if (score.Mods.Any(m => m is OsuModBlinds))
                accuracyValue *= 1.14;
            else if (score.Mods.Any(m => m is OsuModTraceable))
            {
                // Decrease bonus for AR > 10
                accuracyValue *= 1 + 0.08 * DiffUtils.ReverseLerp(approachRate, 11.5, 10);
            }

            if (score.Mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            return accuracyValue;
        }

        private double computeFlashlightValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (!score.Mods.Any(h => h is OsuModFlashlight))
                return 0.0;

            Debug.Assert(attributes.FlashlightDifficulty != null);

            double flashlightValue = Flashlight.DifficultyToPerformance(attributes.FlashlightDifficulty.Value);

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                flashlightValue *= 0.97 * DiffUtils.Pow(1 - DiffUtils.Pow(effectiveMissCount / totalHits, 0.775), DiffUtils.Pow(effectiveMissCount, .875));

            flashlightValue *= getComboScalingFactor(attributes);

            // Scale the flashlight value with accuracy _slightly_.
            flashlightValue *= 0.5 + accuracy / 2.0;

            return flashlightValue;
        }

        private double computeReadingValue(OsuDifficultyAttributes attributes, SkillJudgements judgements)
        {
            double readingValue = DifficultyToPerformance(attributes.ReadingDifficulty);

            double readingRelevantMissCount = judgements.Miss + aimEstimatedSliderBreaks;

            if (readingRelevantMissCount > 0)
                readingValue *= calculateMissPenalty(readingRelevantMissCount, attributes.ReadingDifficultNoteCount);

            // Scale the reading value with the deviation of the judgements attributed to reading _harshly_.
            readingValue *= calculateDeviationScaling(calculateDeviation(judgements), 8);

            return readingValue;
        }

        /// <summary>
        /// The hit window deviation scaling is measured against, in milliseconds. This is the great hit window at OD10.
        /// </summary>
        /// <remarks>
        /// This intentionally does not depend on the beatmap's OD. Deviation is estimated from the map's own great hit window,
        /// so measuring against that same window would only give back the proportion of greats and OD would not be accounted for.
        /// </remarks>
        private const double deviation_scaling_hit_window = 20;

        /// <summary>
        /// Scales a skill's value by the proportion of greats a player with the given deviation would hit on a fixed hit window,
        /// assuming normally distributed hit errors with a mean of zero.
        /// </summary>
        /// <param name="deviation">The deviation on the skill's notes, or null if none of its notes were hit.</param>
        /// <param name="exponent">How harshly the value is scaled.</param>
        private static double calculateDeviationScaling(double? deviation, double exponent)
        {
            if (deviation == null)
                return 0;

            double proportionOfGreats = DiffUtils.Erf(deviation_scaling_hit_window / (Math.Sqrt(2) * deviation.Value));

            return DiffUtils.Pow(proportionOfGreats, exponent);
        }

        private double calculateComboBasedEstimatedMissCount(OsuDifficultyAttributes attributes)
        {
            if (attributes.SliderCount <= 0)
                return countMiss;

            double missCount = countMiss;

            if (usingClassicSliderAccuracy)
            {
                // If sliders in the map are hard - it's likely for player to drop sliderends
                // If map has easy sliders - it's more likely for player to sliderbreak
                double likelyMissedSliderendPortion = 0.04 + 0.06 * DiffUtils.Pow(Math.Min(attributes.AimTopWeightedSliderFactor, 1), 2);

                // Consider that full combo is maximum combo minus dropped slider tails since they don't contribute to combo but also don't break it
                // In classic scores we can't know the amount of dropped sliders so we estimate it
                double fullComboThreshold = attributes.MaxCombo - Math.Min(4 + likelyMissedSliderendPortion * attributes.SliderCount, attributes.SliderCount);

                if (scoreMaxCombo < fullComboThreshold)
                    missCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);

                // In classic scores there can't be more misses than a sum of all non-perfect judgements
                missCount = Math.Min(missCount, totalImperfectHits);

                // Every slider has *at least* 2 combo attributed in classic mechanics.
                // If they broke on a slider with a tick, then this still works since they would have lost at least 2 combo (the tick and the end)
                // Using this as a max means a score that loses 1 combo on a map can't possibly have been a slider break.
                // It must have been a slider end.
                int maxPossibleSliderBreaks = Math.Min(attributes.SliderCount, (attributes.MaxCombo - scoreMaxCombo) / 2);

                double sliderBreaks = missCount - countMiss;

                if (sliderBreaks > maxPossibleSliderBreaks)
                    missCount = countMiss + maxPossibleSliderBreaks;
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
            int nonMissMistakes = countOk + countMeh;

            if (!usingClassicSliderAccuracy || nonMissMistakes == 0)
                return 0;

            double missedComboPercent = 1.0 - (double)scoreMaxCombo / attributes.MaxCombo;
            double estimatedSliderBreaks = Math.Min(nonMissMistakes, effectiveMissCount * topWeightedSliderFactor);

            // Scores with more Oks and Mehs are more likely to have slider breaks.
            // We add an arbitrary value to both sides of the division to make it more stable on extreme ends.
            double nonMissMistakeAdjustment = (nonMissMistakes - estimatedSliderBreaks + 4.5) / (nonMissMistakes + 4);

            // There is a low probability of extra slider breaks on effective miss counts close to 1, as score based calculations are good at indicating if only a single break occurred.
            estimatedSliderBreaks *= DiffUtils.Smoothstep(effectiveMissCount, 1, 2);

            return estimatedSliderBreaks * nonMissMistakeAdjustment * DiffUtils.Logistic(missedComboPercent, 0.33, 15);
        }

        /// <summary>
        /// Estimates the player's tap deviation based on the OD, given number of greats, oks, mehs and misses,
        /// assuming the player's mean hit error is 0. The estimation is consistent in that two SS scores on the same map with the same settings
        /// will always return the same deviation. Misses are ignored because they are usually due to misaiming.
        /// Greats and oks are assumed to follow a normal distribution, whereas mehs are assumed to follow a uniform distribution.
        /// </summary>
        private double? calculateDeviation(SkillJudgements judgements) => calculateDeviation(judgements.Great, judgements.Ok, judgements.Meh);

        private double? calculateDeviation(double relevantCountGreat, double relevantCountOk, double relevantCountMeh)
        {
            if (relevantCountGreat + relevantCountOk + relevantCountMeh <= 0)
                return null;

            // The sample proportion of successful hits.
            double n = Math.Max(1, relevantCountGreat + relevantCountOk);
            double p = relevantCountGreat / n;

            // 99% critical value for the normal distribution (one-tailed).
            const double z = 2.32634787404;

            // We can be 99% confident that the population proportion is at least this value.
            double pLowerBound = Math.Min(p, (n * p + z * z / 2) / (n + z * z) - z / (n + z * z) * Math.Sqrt(n * p * (1 - p) + z * z / 4));

            double deviation;

            // Tested max precision for the deviation calculation.
            if (pLowerBound > 0.01)
            {
                // Compute deviation assuming greats and oks are normally distributed.
                deviation = greatHitWindow / (Math.Sqrt(2) * DiffUtils.ErfInv(pLowerBound));

                // Subtract the deviation provided by tails that land outside the ok hit window from the deviation computed above.
                // This is equivalent to calculating the deviation of a normal distribution truncated at +-okHitWindow.
                double okHitWindowTailAmount = Math.Sqrt(2 / Math.PI) * okHitWindow * Math.Exp(-0.5 * Math.Pow(okHitWindow / deviation, 2))
                                               / (deviation * DiffUtils.Erf(okHitWindow / (Math.Sqrt(2) * deviation)));

                deviation *= Math.Sqrt(1 - okHitWindowTailAmount);
            }
            else
            {
                // A tested limit value for the case of a score only containing oks.
                deviation = okHitWindow / Math.Sqrt(3);
            }

            // Compute and add the variance for mehs, assuming that they are uniformly distributed.
            double mehVariance = (mehHitWindow * mehHitWindow + okHitWindow * mehHitWindow + okHitWindow * okHitWindow) / 3;

            deviation = Math.Sqrt(((relevantCountGreat + relevantCountOk) * Math.Pow(deviation, 2) + relevantCountMeh * mehVariance) / (relevantCountGreat + relevantCountOk + relevantCountMeh));

            return deviation;
        }

        // Calculates multiplier for speed to account for improper tapping based on the deviation and speed difficulty
        // https://www.desmos.com/calculator/dmogdhzofn
        private double calculateSpeedHighDeviationNerf(OsuDifficultyAttributes attributes, double speedDeviation)
        {
            // Decides a point where the difficulty played compared to the speed deviation is assumed to be tapped improperly.
            // Any difficulty above this point is considered "excess" speed difficulty.
            // This is used to cause difficulty above the cutoff to scale logarithmically towards the original speed value thus nerfing the value.
            double excessSpeedDifficultyCutoff = 2.9 + 1.45 * DiffUtils.Pow(22 / speedDeviation, 5);

            if (attributes.SpeedDifficulty <= excessSpeedDifficultyCutoff)
                return 1.0;

            const double scale = 0.45;
            double adjustedSpeedDifficulty = scale * (Math.Log((attributes.SpeedDifficulty - excessSpeedDifficultyCutoff) / scale + 1) + excessSpeedDifficultyCutoff / scale);

            // 220 UR and less are considered tapped correctly to ensure that normal scores will be punished as little as possible
            double lerp = 1 - DiffUtils.ReverseLerp(speedDeviation, 22.0, 27.0);
            adjustedSpeedDifficulty = double.Lerp(adjustedSpeedDifficulty, attributes.SpeedDifficulty, lerp);

            return adjustedSpeedDifficulty / attributes.SpeedDifficulty;
        }

        /// <summary>
        /// Calculates a visibility bonus that is applicable to Traceable.
        /// </summary>
        private double calculateTraceableBonus(double sliderFactor = 1)
        {
            // We want to reward slider aim less, more so at lower AR
            double highApproachRateSliderVisibilityFactor = 0.5 + (DiffUtils.Pow(sliderFactor, 6) / 2);
            double lowApproachRateSliderVisibilityFactor = DiffUtils.Pow(sliderFactor, 6);

            // Start from normal curve, rewarding lower AR up to AR7
            double traceableBonus = 0.0275;
            traceableBonus += 0.025 * (12.0 - Math.Max(approachRate, 7)) * highApproachRateSliderVisibilityFactor;

            // For AR up to 0 - reduce reward for very low ARs when object is visible
            if (approachRate < 7)
                traceableBonus += 0.025 * (7.0 - Math.Max(approachRate, 0)) * lowApproachRateSliderVisibilityFactor;

            // Starting from AR0 - cap values so they won't grow to infinity
            if (approachRate < 0)
                traceableBonus += 0.025 * (1 - DiffUtils.Pow(1.5, approachRate)) * lowApproachRateSliderVisibilityFactor;

            return traceableBonus;
        }

        // Miss penalty assumes that a player will miss on the hardest parts of a map,
        // so we use the amount of relatively difficult sections to adjust miss penalty
        // to make it more punishing on maps with lower amount of hard sections.
        private double calculateMissPenalty(double missCount, double difficultStrainCount) => 0.93 / (missCount / (3 * Math.Log(Math.Max(1, difficultStrainCount))) + 1);
        private double getComboScalingFactor(OsuDifficultyAttributes attributes) => attributes.MaxCombo <= 0 ? 1.0 : Math.Min(DiffUtils.Pow(scoreMaxCombo, 0.8) / DiffUtils.Pow(attributes.MaxCombo, 0.8), 1.0);

        private double calculateRateAdjustedApproachRate(double approachRate, double clockRate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(approachRate, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN) / clockRate;
            return IBeatmapDifficultyInfo.InverseDifficultyRange(preempt, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN);
        }

        private int totalHits => countGreat + countOk + countMeh + countMiss;
        private int totalSuccessfulHits => countGreat + countOk + countMeh;
        private int totalImperfectHits => countOk + countMeh + countMiss;

        /// <summary>
        /// Estimates the player's deviation on the objects attributed to accuracy.
        /// Greats and oks are assumed to follow a normal distribution, whereas mehs are assumed to follow a uniform distribution.
        /// </summary>
        private double? calculateAccuracyDeviation(SkillJudgements judgements, double z = 2.32634787404)
        {
            double n = judgements.Great + judgements.Ok;

            if (n <= 0)
                return null;

            if (judgements.Great == n && z == 0)
                return 0;

            // Proportion of greats, ignoring misses and 50s.
            double p = judgements.Great / n;

            // We can be 99% confident that p is at least this value.
            double pLowerBound = (n * p + z * z / 2) / (n + z * z) - z / (n + z * z) * Math.Sqrt(n * p * (1 - p) + z * z / 4);

            // Compute the deviation assuming 300s and 100s are normally distributed, and 50s are uniformly distributed.
            // Begin with 300s and 100s first. Ignoring 50s, we can be 99% confident that the deviation is not higher than:
            double result = greatHitWindow / (Math.Sqrt(2) * DiffUtils.ErfInv(pLowerBound));
            double adjustFor100 = Math.Sqrt(2 / Math.PI) * okHitWindow * Math.Exp(-0.5 * Math.Pow(okHitWindow / result, 2))
                                  / (result * DiffUtils.Erf(okHitWindow / (Math.Sqrt(2) * result)));

            result *= Math.Sqrt(1 - adjustFor100);

            // Value deviation approach as greatCount approaches 0
            double limitValue = okHitWindow / Math.Sqrt(3);

            // If precision is not enough to compute true deviation - use limit value
            if (pLowerBound <= 0 || adjustFor100 >= 1 || double.IsNaN(result) || result > limitValue)
                result = limitValue;

            // Then compute the variance for 50s.
            double mehVariance = (mehHitWindow * mehHitWindow + okHitWindow * mehHitWindow + okHitWindow * okHitWindow) / 3;

            // Find the total deviation.
            return Math.Sqrt((n * Math.Pow(result, 2) + judgements.Meh * mehVariance) / (n + judgements.Meh));
        }
    }
}
