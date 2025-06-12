using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class RhythmEvaluator
    {
        private static double identicalStrainTolerance;

        private static List<double> noteHistory = new List<double>();
        private static List<double> noteHistoryVirtual = new List<double>();

        private static readonly double[] prevFractionX = { 1.0, 1.5, 2.0, 3.0, 4.0 };
        private static readonly double[] prevFractionY = { 0.5, 1.5, 0.9, 0.25, 0.0 };

        private static readonly double[] nextFractionX = { 1.0, 7.0 / 6.0, 1.5, 1.75, 2.0, 3.0, 4.0 };
        private static readonly double[] nextFractionY = { 0.05, 1.0, 0.75, 1.0, 0.5, 0.0, 0.0 };

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
            OsuDifficultyHitObject osuPrev = (OsuDifficultyHitObject)current.Previous(1);
            OsuDifficultyHitObject osuNext = (OsuDifficultyHitObject)current.Next(1);

            noteHistory.Clear();
            noteHistoryVirtual.Clear();

            double strainTime = osuCurrent.StrainTime / 1000;
            double prevStrainTime = osuPrev != null ? osuPrev.StrainTime / 1000 : 0;
            double prevVirtualStrainTime = osuPrev != null ? CalculateVirtualStrainTime(osuPrev) : 0;
            double virtualStrainTime = CalculateVirtualStrainTime(osuCurrent);
            identicalStrainTolerance = osuCurrent.HitWindowGreat / 2000;

            int rhythmStart = 0;

            int index = -1; // Start from current

            double runningTotal = 0;

            while (true)
            {
                DifficultyHitObject previousObj = current.Previous(index++);
                if (previousObj == null)
                    break;

                if (previousObj is not OsuDifficultyHitObject currObj)
                    continue;

                double strainT = currObj.StrainTime / 1000;
                double virtualStrainT = CalculateVirtualStrainTime(currObj);

                noteHistory.Add(strainT);
                noteHistoryVirtual.Add(virtualStrainT);
                runningTotal += strainT;

                if (runningTotal > 4 || noteHistory.Count > 32)
                    break;

                if (noteHistory.Count < noteHistoryVirtual.Count)
                    break;
            }

            noteHistory.Reverse();
            noteHistoryVirtual.Reverse();

            double repetitionVal = 0;
            double downtimeScale = 1;
            double appearanceScale = 1;
            double uniqueScale = 1;
            if (noteHistory.Count > 2)
            {
                double repetition = 1.0 - calculateExpectancy(noteHistory);
                double virtualRepetition = 1.0 - calculateExpectancy(noteHistoryVirtual);
                double repetitionExponent = Math.Min(2.0, 66.25 * Math.Min(strainTime, virtualStrainTime) - 1.65625);
                repetitionVal = Math.Pow(Math.Min(repetition, virtualRepetition), repetitionExponent);

                // When there is major downtime / not much actually happening
                downtimeScale = Math.Min(calculateDowntime(strainTime, noteHistory), calculateDowntime(virtualStrainTime, noteHistoryVirtual));

                // When there's a huge stream before a pack of doubles / triples
                appearanceScale = Math.Min(strainAppearance(strainTime, noteHistory), strainAppearance(virtualStrainTime, noteHistoryVirtual));

                // When there's a ton of unique strains that means that it's a wild BPM area
                (double uniqueVal, _) = checkAnomaly(noteHistory);
                (double virtualUniqueVal, _) = checkAnomaly(noteHistoryVirtual);
                uniqueScale = 1.0 + Math.Pow((Math.Min(uniqueVal, virtualUniqueVal) - 1.0) / 11.0, 4.0);
            }

            double multiplier = Math.Min(
                Math.Min(CompareStrains(strainTime, prevStrainTime, prevFractionX, prevFractionY), CompareStrains(strainTime, prevVirtualStrainTime, prevFractionX, prevFractionY)),
                Math.Min(CompareStrains(virtualStrainTime, prevStrainTime, prevFractionX, prevFractionY), CompareStrains(virtualStrainTime, prevVirtualStrainTime, prevFractionX, prevFractionY))
            );
            if (current is Slider)
            {
                multiplier /= 2;
            }

            // Console.WriteLine($"repetitionVal: {repetitionVal}, multiplier: {multiplier}, downtimeScale: {downtimeScale}, appearanceScale {appearanceScale}, uniqueScale, {uniqueScale}");
            double strain = repetitionVal * multiplier * downtimeScale * appearanceScale * uniqueScale / strainTime;

            if (osuNext != null)
            {
                double nextTime = osuNext.StrainTime / 1000.0;
                double nextVirtualStrainTime = 0;
                if (current.BaseObject is Slider currSlider)
                    nextVirtualStrainTime = Math.Max((nextTime - currSlider.EndTime / 1000.0), 0.025);

                double nextMultiplier = Math.Min(
                    Math.Min(CompareStrains(strainTime, nextTime, nextFractionX, nextFractionY), CompareStrains(strainTime, nextVirtualStrainTime, nextFractionX, nextFractionY)),
                    Math.Min(CompareStrains(virtualStrainTime, nextTime, nextFractionX, nextFractionY), CompareStrains(virtualStrainTime, nextVirtualStrainTime, nextFractionX, nextFractionY))
                );
                if (osuNext.BaseObject is Slider)
                    multiplier /= 2;

                strain *= nextMultiplier;
            }


            return strain;
        }

        private static double calculateDowntime(double strainTime, List<double> refNoteHistory)
        {
            int longNoteCount = 0;
            for (int i = 0; i < refNoteHistory.Count; i++)
            {
                if (refNoteHistory[i] > strainTime * 2 - identicalStrainTolerance)
                    longNoteCount++;
            }

            double longNoteFraction = Math.Max(0.5, (double)longNoteCount / (double)refNoteHistory.Count);

            double result = 1.0 - DifficultyCalculationUtils.Smoothstep(longNoteFraction, 0.5, 1.0);
            return result;
        }

        private static double strainAppearance(double strainTime, List<double> refNoteHistory)
        {
            int strainApperance = 0;
            for (int i = 0; i < refNoteHistory.Count; i++)
            {
                if (Math.Abs(refNoteHistory[i] - strainTime) < identicalStrainTolerance)
                    strainApperance++;
            }

            if (strainApperance == refNoteHistory.Count)
                return 0;

            double strainAppearanceFraction = Math.Max(0.5, (double)strainApperance / (double)refNoteHistory.Count);

            double result = 1.0 - DifficultyCalculationUtils.Smoothstep(strainAppearanceFraction, 0.5, 1.0);;
            return result;
        }

        private static double CalculateVirtualStrainTime(OsuDifficultyHitObject current)
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

            List<double> pattern = null;
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
            double fractionMultiplier = CompareStrains(strainTime, history[1], prevFractionX, prevFractionY);

            double repetitionVal = Math.Min(1.0, Math.Sqrt(maxRepetition) + patternLength);

            if (!exists)
            {
                double uniqueScale = Math.Pow(Math.Pow(-Math.Min(7.0, anomalyVal - 1.0) / 7.0, 5.0) + 1.0, 2.0);
                repetitionVal = Math.Max(Math.Min(1, repetitionVal + uniqueScale - fractionMultiplier), 0.0);
            }

            return repetitionVal;
        }


        private static (double, bool) checkAnomaly(List<double> refNoteHistory)
        {
            List<double> uniqueStrains = new List<double>();

            // Get all unique straintimes, ignore current object
            for (int i = 0; i < refNoteHistory.Count-1; i++)
            {
                bool exists = false;
                for (int j = 0; j < uniqueStrains.Count; j++)
                {
                    if (Math.Abs(uniqueStrains[j] - refNoteHistory[i]) < identicalStrainTolerance)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    uniqueStrains.Add(refNoteHistory[i]);
            }

            // Check if current strain exists previously, and find the ratio closest to 1
            bool unique = true;
            double strainTime = refNoteHistory[refNoteHistory.Count - 1];
            double strainRatio = 0;
            double closestStrain = 0;
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
                    closestStrain = uniqueStrains[j];
                }
            }
            return ((double)uniqueStrains.Count, !unique);
        }

        private static double CompareStrains(double strain1, double strain2, double[] x, double[] y)
        {
            if (strain1 == 0 || strain2 == 0)
                return 1;

            double fraction = Math.Max(strain1 / strain2, strain2 / strain1);

            return Math.Max(0.0, DifficultyCalculationUtils.InterpolateFromSortedTable(x, y, fraction));
        }
    }
}
