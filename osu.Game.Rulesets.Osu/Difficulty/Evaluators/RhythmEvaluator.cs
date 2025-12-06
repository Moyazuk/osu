// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class RhythmEvaluator
    {
        private const int history_time_max = 5 * 1000; // 5 seconds
        private const int history_objects_max = 32;
        private const double rhythm_overall_multiplier = 1.0;

        private static double identicalStrainTolerance;

        private static readonly List<double> note_history = new List<double>();
        private static readonly List<double> note_history_virtual = new List<double>();

        private static (double ratio, double multiplier)[] createPreviousRatioTable(OsuDifficultyTuning tuning) => new[]
        {
            (1.0,         tuning.RhythmPrevSame),
            (4.0 / 3.0,   tuning.RhythmPrev_4over3),
            (1.5,         tuning.RhythmPrev_3over2),
            (5.0 / 3.0,   tuning.RhythmPrev_5over3),
            (2.0,         tuning.RhythmPrev_2over1),
            (2.5,         tuning.RhythmPrev_5over2),
            (3.0,         tuning.RhythmPrev_3over1),
            (4.0,         tuning.RhythmPrev_4over1),
        };

        private static (double ratio, double multiplier)[] createNextRatioTable(OsuDifficultyTuning tuning) => new[]
        {
            (1.0,         tuning.RhythmNextSame),
            (4.0 / 3.0,   tuning.RhythmNext_4over3),
            (1.5,         tuning.RhythmNext_3over2),
            (5.0 / 3.0,   tuning.RhythmNext_5over3),
            (2.0,         tuning.RhythmNext_2over1),
            (2.5,         tuning.RhythmNext_5over2),
            (3.0,         tuning.RhythmNext_3over1),
            (4.0,         tuning.RhythmNext_4over1),
        };

        private static (double ratio, double multiplier)[] createPrevious2RatioTable(OsuDifficultyTuning tuning) => new[]
        {
            (1.0,         tuning.RhythmPrev2Same),
            (4.0 / 3.0,   tuning.RhythmPrev2_4over3),
            (1.5,         tuning.RhythmPrev2_3over2),
            (5.0 / 3.0,   tuning.RhythmPrev2_5over3),
            (2.0,         tuning.RhythmPrev2_2over1),
            (2.5,         tuning.RhythmPrev2_5over2),
            (3.0,         tuning.RhythmPrev2_3over1),
            (4.0,         tuning.RhythmPrev2_4over1),
        };

        private static (double ratio, double multiplier)[] createNext2RatioTable(OsuDifficultyTuning tuning) => new[]
        {
            (1.0,         tuning.RhythmNext2Same),
            (4.0 / 3.0,   tuning.RhythmNext2_4over3),
            (1.5,         tuning.RhythmNext2_3over2),
            (5.0 / 3.0,   tuning.RhythmNext2_5over3),
            (2.0,         tuning.RhythmNext2_2over1),
            (2.5,         tuning.RhythmNext2_5over2),
            (3.0,         tuning.RhythmNext2_3over1),
            (4.0,         tuning.RhythmNext2_4over1),
        };

        /// <summary>
        /// Calculates a rhythm multiplier for the difficulty of the tap associated with historic data of the current <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, OsuDifficultyTuning tuning)
        {
            if (current.BaseObject is Spinner)
                return 0;

            OsuDifficultyHitObject osuCurrent = (OsuDifficultyHitObject)current;
            OsuDifficultyHitObject osuPrev = (OsuDifficultyHitObject)current.Previous(0);
            OsuDifficultyHitObject osuPrev2 = (OsuDifficultyHitObject)current.Previous(1);
            OsuDifficultyHitObject osuNext = (OsuDifficultyHitObject)current.Next(0);
            OsuDifficultyHitObject osuNext2 = (OsuDifficultyHitObject)current.Next(1);

            double[] prev_fraction_x = { 1.0, 1.5, 2.0, 3.0, 4.0 };
            double[] prev_fraction_y = { 0.5, 0.05, 1, 0.5, 0.0 };

            double[] next_fraction_x = { 1.0, 7.0 / 6.0, 1.5, 1.75, 2.0, 3.0, 4.0 };
            double[] next_fraction_y = { 0.05, 4, 1, 2, 0.5, 0.05, 0.0 };

            double[] prev2_fraction_x = { 1.0, 1.5, 2.0, 3.0, 4.0 };
            double[] prev2_fraction_y = { 0.25, 2.5, 0.75, 0.05, 1.5 };

            double[] next2_fraction_x = { 1.0, 7.0 / 6.0, 1.5, 1.75, 2.0, 3.0, 4.0 };
            double[] next2_fraction_y = { 1.0, 0.05, 0.05, 0.05, 0.5, 2, 0.5 };

            note_history.Clear();
            note_history_virtual.Clear();

            double strainTime = osuCurrent.AdjustedDeltaTime / 1000;
            double virtualStrainTime = calculateVirtualStrainTime(osuCurrent);
            double prevStrainTime = osuPrev != null ? osuPrev.AdjustedDeltaTime / 1000 : 0;
            double prev2StrainTime = osuPrev2 != null ? osuPrev2.AdjustedDeltaTime / 1000 : 0;
            double prevVirtualStrainTime = osuPrev != null ? calculateVirtualStrainTime(osuPrev) : 0;
            double prev2VirtualStrainTime = osuPrev2 != null ? calculateVirtualStrainTime(osuPrev2) : 0;
            identicalStrainTolerance = osuCurrent.HitWindowGreat / 2000;

            int index = -1; // Start from current

            double timeElapsed = 0; // Time elapsed in seconds

            while (true)
            {
                DifficultyHitObject previousObj = current.Previous(index++);
                if (previousObj == null)
                    break;

                if (previousObj is not OsuDifficultyHitObject currObj)
                    continue;

                double strainT = currObj.AdjustedDeltaTime / 1000;
                double virtualStrainT = calculateVirtualStrainTime(currObj);

                note_history.Add(strainT);
                note_history_virtual.Add(virtualStrainT);
                timeElapsed += strainT;

                if (timeElapsed > 2 || note_history.Count > 16)
                    break;

                if (note_history.Count < note_history_virtual.Count)
                    break;
            }

            note_history.Reverse();
            note_history_virtual.Reverse();

            double repetitionVal = 0;
            double downtimeScale = 1;
            double appearanceScale = 1;
            double uniqueScale = 1;

            var prevTable = createPreviousRatioTable(tuning);
            var nextTable = createNextRatioTable(tuning);
            var prev2Table = createPrevious2RatioTable(tuning);
            var next2Table = createNext2RatioTable(tuning);

            if (note_history.Count > 2)
            {
                double repetition = 1.0 - calculateExpectancy(note_history, prevTable);

                double virtualRepetition = 1.25 - calculateExpectancy(note_history_virtual, prevTable);
                double repetitionExponent = Math.Min(2.0, 66.25 * Math.Min(strainTime, virtualStrainTime) - 1.65625);
                repetitionVal = Math.Pow(Math.Min(repetition, virtualRepetition), repetitionExponent);

                // When there is major downtime / not much actually happening
                downtimeScale = Math.Min(calculateDowntime(strainTime, note_history), calculateDowntime(virtualStrainTime, note_history_virtual));

                // When there's a huge stream before a pack of doubles / triples
                appearanceScale = Math.Min(strainAppearance(strainTime, note_history), strainAppearance(virtualStrainTime, note_history_virtual));

                // When there's a ton of unique strains that means that it's a wild BPM area
                (double uniqueVal, _) = checkAnomaly(note_history);
                (double virtualUniqueVal, _) = checkAnomaly(note_history_virtual);
                uniqueScale = 1.0 + Math.Pow((Math.Min(uniqueVal, virtualUniqueVal) - 1.0) / tuning.Uniqueness_Divisor, tuning.Uniqueness_Exponent);
            }


            double currMultiplier = Math.Min(
                Math.Min(compareStrains(strainTime, prevStrainTime, prevTable),
                    compareStrains(strainTime, prevVirtualStrainTime, prevTable)),
                Math.Min(compareStrains(virtualStrainTime, prevStrainTime, prevTable),
                    compareStrains(virtualStrainTime, prevVirtualStrainTime, prevTable))
            );

            double prevMultiplier = Math.Min(
                Math.Min(compareStrains(prevStrainTime, prev2StrainTime, prev2Table), compareStrains(prevStrainTime, prev2VirtualStrainTime, prev2Table)),
                Math.Min(compareStrains(prevVirtualStrainTime, prev2StrainTime, prev2Table), compareStrains(prevVirtualStrainTime, prev2VirtualStrainTime, prev2Table))
            );

            if (current.BaseObject is Slider)
            {
                currMultiplier /= 2;
            }

            double strain = repetitionVal * currMultiplier * prevMultiplier * downtimeScale * appearanceScale * uniqueScale / strainTime;

            if (osuNext == null || osuNext2 == null) return strain;

            double nextTime = osuNext.AdjustedDeltaTime / 1000.0;
            double next2Time = osuNext2.AdjustedDeltaTime / 1000.0;
            double nextVirtualStrainTime = calculateVirtualStrainTime(osuNext);
            double next2VirtualStrainTime = calculateVirtualStrainTime(osuNext2);

            double nextMultiplier = Math.Min(
                Math.Min(compareStrains(strainTime, nextTime, nextTable), compareStrains(strainTime, nextVirtualStrainTime, nextTable)),
                Math.Min(compareStrains(virtualStrainTime, nextTime, nextTable), compareStrains(virtualStrainTime, nextVirtualStrainTime, nextTable))
            );

            double next2Multiplier = Math.Min(
                Math.Min(compareStrains(nextTime, next2Time, next2Table), compareStrains(nextTime, next2VirtualStrainTime, next2Table)),
                Math.Min(compareStrains(nextVirtualStrainTime, next2Time, next2Table), compareStrains(nextVirtualStrainTime, next2VirtualStrainTime, next2Table))
            );

            if (osuNext.BaseObject is Slider)
                nextMultiplier /= 2;

            strain *= nextMultiplier * next2Multiplier;

            double doubletapness = 1.0 - osuCurrent.GetDoubletapness((OsuDifficultyHitObject?)osuCurrent.Next(0));

            // Console.WriteLine($"strain: {strain}, repetitionVal: {repetitionVal}, multiplier: {multiplier}, nextMult: {nextMultiplier}, downtimeScale: {downtimeScale}, appearanceScale {appearanceScale}, uniqueScale, {uniqueScale}");
            return strain;
        }

        private static double calculateDowntime(double strainTime, List<double> refNoteHistory)
        {
            int longNoteCount = refNoteHistory.Count(t => t > strainTime * 2 - identicalStrainTolerance);

            double longNoteFraction = Math.Max(0.5, longNoteCount / (double)refNoteHistory.Count);

            double result = 1.0 - DifficultyCalculationUtils.Smoothstep(longNoteFraction, 0.5, 1.0);
            return result;
        }

        private static double strainAppearance(double strainTime, List<double> refNoteHistory)
        {
            int strainAppearance = refNoteHistory.Count(t => Math.Abs(t - strainTime) < identicalStrainTolerance);

            if (strainAppearance == refNoteHistory.Count)
                return 0;

            double strainAppearanceFraction = Math.Max(0.5, strainAppearance / (double)refNoteHistory.Count);

            double result = 1.0 - DifficultyCalculationUtils.Smoothstep(strainAppearanceFraction, 0.5, 1.0);
            return result;
        }

        private static double calculateVirtualStrainTime(OsuDifficultyHitObject current)
        {
            if (current.LastObject is Slider prevSlider)

                return Math.Max((current.StartTime - prevSlider.EndTime) / 1000, 0.025);

            return current.AdjustedDeltaTime / 1000;
        }

        private static double calculateExpectancy(List<double> refNoteHistory, (double ratio, double multiplier)[] prevTable)
        {
            (double anomalyVal, bool exists) = checkAnomaly(refNoteHistory);

            int n = refNoteHistory.Count;
            List<double> history = new List<double>(n);
            for (int i = n - 1; i >= 0; i--)
                history.Add(refNoteHistory[i]);

            double strainTime = history[0];

            List<double>? pattern = null;

            for (int i = 1; i < history.Count; i++)
            {
                if (Math.Abs(history[i] - strainTime) > identicalStrainTolerance)
                {
                    pattern = history.GetRange(0, i + 1);
                    break;
                }
            }

            if (pattern == null)
                return 1;

            if (pattern.Count > history.Count / 2)
                return (double)pattern.Count / history.Count;

            int maxSize = pattern.Count;
            double maxRepetition = 0;

            for (int k = pattern.Count; k < history.Count / 2; k++)
            {
                var candidate = history.GetRange(0, k);

                int patternInstance = 0;
                int reversePatternInstance = 0;

                for (int i = k; i <= history.Count - k; i++)
                {
                    bool same = true, reverseSame = true;

                    for (int j = 0; j < k; j++)
                    {
                        double a = candidate[j];
                        double b = history[i + j];
                        double rb = history[i + k - 1 - j];

                        if (Math.Abs(a - b) > identicalStrainTolerance)
                            same = false;

                        if (Math.Abs(a - rb) > identicalStrainTolerance)
                            reverseSame = false;

                        if (!same && !reverseSame)
                            break;
                    }

                    if (same) patternInstance++;
                    else if (reverseSame) reversePatternInstance++;
                }

                int possibleInstances = Math.Max(1, (int)Math.Ceiling((history.Count - 2 * k + 1) / 2.0));
                double ratio = Math.Min(1, (double)Math.Max(patternInstance, reversePatternInstance) / possibleInstances);

                if (ratio > maxRepetition)
                {
                    maxRepetition = ratio;
                    maxSize = k;
                }

                if (maxRepetition == 1)
                    break;
            }

            double patternLength = DifficultyCalculationUtils.Smoothstep(maxSize, 2, 8);
            double fractionMultiplier = compareStrains(strainTime, history[1], prevTable);

            double repetitionVal = Math.Min(1.0, Math.Sqrt(maxRepetition) + patternLength);

            if (exists) return repetitionVal;

            double uniqueScale = Math.Pow(Math.Pow(-Math.Min(7.0, anomalyVal - 1.0) / 7.0, 5.0) + 1.0, 2.0);
            repetitionVal = Math.Max(Math.Min(1, repetitionVal + uniqueScale - fractionMultiplier), 0.0);

            return repetitionVal;
        }

        private static (double, bool) checkAnomaly(List<double> refNoteHistory)
        {
            List<double> uniqueStrains = new List<double>();

            // Get all unique straintimes, ignore current object
            for (int i = 0; i < refNoteHistory.Count - 1; i++)
            {
                bool exists = uniqueStrains.Any(t => Math.Abs(t - refNoteHistory[i]) < identicalStrainTolerance);

                if (!exists)
                    uniqueStrains.Add(refNoteHistory[i]);
            }

            // Check if current strain exists previously, and find the ratio closest to 1
            bool unique = true;
            double strainTime = refNoteHistory[^1];
            double strainRatio = 0;

            for (int j = 0; j < uniqueStrains.Count; j++)
            {
                if (
                    Math.Abs(strainTime - uniqueStrains[j]) < identicalStrainTolerance ||
                    Math.Abs(strainTime * 2 - uniqueStrains[j]) < identicalStrainTolerance ||
                    Math.Abs(strainTime / 2 - uniqueStrains[j]) < identicalStrainTolerance
                )
                {
                    unique = false;
                    break;
                }

                double strainRatioTest = Math.Max(strainTime, uniqueStrains[j]) / Math.Min(strainTime, uniqueStrains[j]);

                if (strainRatioTest - 1 < strainRatio - 1)
                {
                    strainRatio = strainRatioTest;
                }
            }

            return (uniqueStrains.Count, !unique);
        }

        private static double compareStrains(
            double strain1,
            double strain2,
            (double ratio, double multiplier)[] ratioMultipliers)
        {
            if (strain1 == 0 || strain2 == 0)
                return 1;

            double fraction = Math.Max(strain1 / strain2, strain2 / strain1);

            double result = LerpFromArrays(ratioMultipliers, fraction);

            return Math.Max(0.0, result);
        }

        public static double LerpFromArrays((double ratio, double multiplier)[] ratioMultipliers, double t)
        {
            if (t <= ratioMultipliers[0].ratio)
                return ratioMultipliers[0].multiplier;

            if (t >= ratioMultipliers[^1].ratio)
                return ratioMultipliers[^1].multiplier;

            for (int i = 0; i < ratioMultipliers.Length - 1; i++)
            {
                if (t >= ratioMultipliers[i].ratio && t <= ratioMultipliers[i + 1].ratio)
                {
                    double distance = (t - ratioMultipliers[i].ratio) / (ratioMultipliers[i + 1].ratio - ratioMultipliers[i].ratio);
                    return Interpolation.Lerp(ratioMultipliers[i].multiplier, ratioMultipliers[i + 1].multiplier, distance);
                }
            }

            return 0;
        }
    }
}
