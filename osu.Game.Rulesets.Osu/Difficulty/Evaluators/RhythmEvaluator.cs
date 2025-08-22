using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class RhythmEvaluator
    {
        private static double identicalStrainTolerance;

        private static readonly List<double> note_history = new List<double>();
        private static readonly List<double> note_history_virtual = new List<double>();

        private static readonly double[] prev_fraction_x = { 1.0, 1.5, 2.0, 3.0, 4.0 };
        private static readonly double[] prev_fraction_y = { 0.5, 1.5, 0.9, 0.25, 0.0 };

        private static readonly double[] next_fraction_x = { 1.0, 7.0 / 6.0, 1.5, 1.75, 2.0, 3.0, 4.0 };
        private static readonly double[] next_fraction_y = { 0.05, 1.0, 0.75, 1.0, 0.5, 0.0, 0.0 };

        /// <summary>
        /// Evaluates the difficulty of tapping the current object.
        /// </summary>
        /// <param name="current">The current difficulty hit object.</param>
        /// <returns>The difficulty value of the object.</returns>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            OsuDifficultyHitObject osuCurrent = (OsuDifficultyHitObject)current;
            OsuDifficultyHitObject osuPrev = (OsuDifficultyHitObject)current.Previous(0);
            OsuDifficultyHitObject osuNext = (OsuDifficultyHitObject)current.Next(0);

            note_history.Clear();
            note_history_virtual.Clear();

            double strainTime = osuCurrent.StrainTime / 1000;
            double prevStrainTime = osuPrev != null ? osuPrev.StrainTime / 1000 : 0;
            double prevVirtualStrainTime = osuPrev != null ? calculateVirtualStrainTime(osuPrev) : 0;
            double virtualStrainTime = calculateVirtualStrainTime(osuCurrent);
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

                double strainT = currObj.StrainTime / 1000;
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

            if (note_history.Count > 2)
            {
                double repetition = 1.0 - calculateExpectancy(note_history);

                // Added arbitrary buffer to virtualRepetition because slider endtimes are not a consistent rhythmic reference point due to leniency (also makes values better)

                double virtualRepetition = 1.0 - calculateExpectancy(note_history_virtual);
                double repetitionExponent = Math.Min(2.0, 66.25 * Math.Min(strainTime, virtualStrainTime) - 1.65625);
                repetitionVal = Math.Pow(Math.Min(repetition, virtualRepetition), repetitionExponent);

                // When there is major downtime / not much actually happening
                downtimeScale = Math.Min(calculateDowntime(strainTime, note_history), calculateDowntime(virtualStrainTime, note_history_virtual));

                // When there's a huge stream before a pack of doubles / triples
                appearanceScale = Math.Min(strainAppearance(strainTime, note_history), strainAppearance(virtualStrainTime, note_history_virtual));

                // When there's a ton of unique strains that means that it's a wild BPM area
                (double uniqueVal, _) = checkAnomaly(note_history);
                (double virtualUniqueVal, _) = checkAnomaly(note_history_virtual);
                uniqueScale = 1.0 + Math.Pow((Math.Min(uniqueVal, virtualUniqueVal) - 1.0) / 11.0, 4.0);
            }

            double multiplier = Math.Min(
                Math.Min(compareStrains(strainTime, prevStrainTime, prev_fraction_x, prev_fraction_y), compareStrains(strainTime, prevVirtualStrainTime, prev_fraction_x, prev_fraction_y)),
                Math.Min(compareStrains(virtualStrainTime, prevStrainTime, prev_fraction_x, prev_fraction_y), compareStrains(virtualStrainTime, prevVirtualStrainTime, prev_fraction_x, prev_fraction_y))
            );

            if (current.BaseObject is Slider)
            {
                multiplier /= 2;
            }

            double strain = repetitionVal * multiplier * downtimeScale * appearanceScale * uniqueScale / strainTime;

            if (osuNext == null) return strain;

            double nextTime = osuNext.StrainTime / 1000.0;
            double nextVirtualStrainTime = 0;
            if (current.BaseObject is Slider currSlider)
                nextVirtualStrainTime = Math.Max(nextTime - currSlider.EndTime / 1000.0, 0.025);

            double nextMultiplier = Math.Min(
                Math.Min(compareStrains(strainTime, nextTime, next_fraction_x, next_fraction_y), compareStrains(strainTime, nextVirtualStrainTime, next_fraction_x, next_fraction_y)),
                Math.Min(compareStrains(virtualStrainTime, nextTime, next_fraction_x, next_fraction_y), compareStrains(virtualStrainTime, nextVirtualStrainTime, next_fraction_x, next_fraction_y))
            );
            if (osuNext.BaseObject is Slider)
                nextMultiplier /= 2;

            strain *= nextMultiplier;

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

            return current.StrainTime / 1000;
        }

        private static double calculateExpectancy(List<double> refNoteHistory)
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
            double fractionMultiplier = compareStrains(strainTime, history[1], prev_fraction_x, prev_fraction_y);

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

        private static double compareStrains(double strain1, double strain2, double[] x, double[] y)
        {
            if (strain1 == 0 || strain2 == 0)
                return 1;

            double fraction = Math.Max(strain1 / strain2, strain2 / strain1);

            return Math.Max(0.0, DifficultyCalculationUtils.InterpolateFromSortedTable(x, y, fraction));
        }
    }
}
