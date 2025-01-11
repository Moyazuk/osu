using System;
using System.Linq;
using System.Collections.Generic;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using MathNet.Numerics;
using MathNet.Numerics.Interpolation;
using MathNet.Numerics.LinearAlgebra;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class RhythmEvaluator
    {
        private const double rhythm_overall_multiplier = 0.95;
        private static double identicalStrainTolerance;

        private static List<double> noteHistory = new List<double>();
        private static List<double> noteHistoryVirtual = new List<double>();

        private static LinearSpline prevFractionSpline = LinearSpline.InterpolateSorted(
            new double[] { 1.0, 1.5 , 2.0, 3.0, 4.0 },
            new double[] { 0.5, 1.5, 0.9, 0.25, 0.0 } 
        );
        private static LinearSpline nextFractionSpline = LinearSpline.InterpolateSorted(
            new double[] { 1.0 , 7.0/6.0, 1.5 , 1.75, 2.0, 3.0, 4.0 },
            new double[] { 0.05, 1.0    , 0.75, 1.0 , 0.5, 0.0, 0.0 }
        );

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

            noteHistory.Clear();
            noteHistoryVirtual.Clear();

            double strainTime = osuCurrent.StrainTime / 1000;
            double prevStrainTime = osuPrev != null ? osuPrev.StrainTime / 1000 : 0;
            double prevVirtualStrainTime = osuPrev != null ? CalculateVirtualStrainTime(osuPrev) : 0;
            double virtualStrainTime = CalculateVirtualStrainTime(osuCurrent);
            identicalStrainTolerance = osuCurrent.HitWindowGreat / 2000;

            int rhythmStart = 0;

            int index = -1; // Start from current

            while (true)
            {
                // Get the previous object
                DifficultyHitObject previousObj = current.Previous(index++);
                if (previousObj == null)
                    break; // Exit if there are no more previous objects

                // Safely cast the previous object to OsuDifficultyHitObject
                if (previousObj is not OsuDifficultyHitObject currObj)
                    continue; // Skip if the object is not of the expected type

                // Add to note histories
                noteHistory.Add(currObj.StrainTime / 1000);
                noteHistoryVirtual.Add(CalculateVirtualStrainTime(currObj));

                // Break if the sum of noteHistory exceeds 4 seconds or the list grows too large
                if (noteHistory.Sum() > 4 || noteHistory.Count > 32)
                    break;

                // Break if the virtual history is mismatched
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
                uniqueScale = 1.0 + Math.Pow((Math.Min(uniqueVal, virtualUniqueVal) - 1.0) / 3.0, 3.0);
            }

            double multiplier = Math.Min(
                Math.Min(CompareStrains(strainTime, prevStrainTime, prevFractionSpline), CompareStrains(strainTime, prevVirtualStrainTime, prevFractionSpline)),
                Math.Min(CompareStrains(virtualStrainTime, prevStrainTime, prevFractionSpline), CompareStrains(virtualStrainTime, prevVirtualStrainTime, prevFractionSpline))
            );
            if (current is Slider)
            {
                multiplier /= 2;
            }

            // Console.WriteLine($"repetitionVal: {repetitionVal}, multiplier: {multiplier}, downtimeScale: {downtimeScale}, appearanceScale {appearanceScale}, uniqueScale, {uniqueScale}");
            double rhythmResult = repetitionVal * multiplier * downtimeScale * appearanceScale * uniqueScale / strainTime;

            //Console.WriteLine($"Final = {Math.Sqrt(4 + rhythmResult * 0.8) / 2.0}");

            return rhythmResult * 0.4;
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

            double result = Math.Pow(Math.Sin(Math.PI * (longNoteFraction - 1.0)), 2.0);
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

            double strainAppearanceFraction = Math.Max(0.5, (double)strainApperance / (double)refNoteHistory.Count);

            double result = Math.Pow(Math.Sin(Math.PI * (strainAppearanceFraction - 1.0)), 2.0);
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
            // See how many unique strains there are, and get a nerfed version of the straintime
            (double anomalyVal, bool exists) = checkAnomaly(refNoteHistory);

            refNoteHistory.Reverse();

            // Get reference pattern
            List<double> pattern = new List<double>();
            double strainTime = refNoteHistory[0];
            for (int i = 1; i < refNoteHistory.Count; i++)
            {
                if (Math.Abs(refNoteHistory[i] - strainTime) > identicalStrainTolerance)
                {
                    pattern = refNoteHistory.Take(i+1).ToList();
                    break;
                }
            }
                
            // If pattern length is 0, then that means that there are no changing straintimes
            if (pattern.Count == 0)
            {
                refNoteHistory.Reverse();
                return 1;
            }
            
            // If longer than half of the refNoteHistory length then just look at how often
            if (pattern.Count > refNoteHistory.Count / 2.0)
            {
                refNoteHistory.Reverse();
                return (double)pattern.Count / (double)refNoteHistory.Count;
            }
            
            int minSize = pattern.Count;
            int maxSize = pattern.Count;
            double maxRepetition = 0;
            for (int k = minSize; k < refNoteHistory.Count / 2; k++) // See how many times each pattern from reference size to half the main list repeats, get the maximum value
            {
                pattern = refNoteHistory.Take(k).ToList();
                
                int patternInstance = 0;
                int reversePatternInstance = 0;
                for (int i = pattern.Count; i < refNoteHistory.Count; i++)
                {
                    List<double> patternCompare = refNoteHistory.Skip(i).Take(pattern.Count).ToList();

                    if (patternCompare.Count != pattern.Count)
                        break;
                    
                    bool samePattern = true;
                    for (int j = 0; j < pattern.Count; j++)
                    {
                        if (Math.Abs(pattern[j] - patternCompare[j]) > identicalStrainTolerance)
                        {
                            samePattern = false;
                            break;
                        }
                    }
                    
                    if (samePattern)
                        patternInstance++;
                    else
                    {
                        patternCompare.Reverse();
                        bool reverseSamePattern = true;
                        for (int j = 0; j < pattern.Count; j++)
                        {
                            if (Math.Abs(pattern[j] - patternCompare[j]) > identicalStrainTolerance)
                            {
                                reverseSamePattern = false;
                                break;
                            }
                        }

                        if (reverseSamePattern)
                            reversePatternInstance++;
                    }
                }

                int possibleInstances = (int)Math.Ceiling((refNoteHistory.Count - pattern.Count - (pattern.Count - 1)) / 2.0);
                double ratio = Math.Min(1, (double)Math.Max(patternInstance, reversePatternInstance) / (double)possibleInstances);
                // There are cases where it's possible the counter makes this ratio more than 1 due to the checking method being if notes
                // fall within a range of 16 ms. As a result a max is required to cap at 1.

                if (ratio > maxRepetition)
                {
                    maxRepetition = ratio;
                    maxSize = pattern.Count;
                }

                // No need to loop anymore since 1 is the highest possible value
                if (maxRepetition == 1)
                    break;
            }

            // Punish patterns that are longer more, pattern size of 2 gets 0 value while pattern size 8+ get 1
            double patternLength = Math.Pow(Math.Sin(Math.PI * (Math.Min(maxSize, 8) - 2) / 12), 2.0);

            var fractionMultiplier = CompareStrains(strainTime, refNoteHistory[1], prevFractionSpline);

            refNoteHistory.Reverse();
            double repetitionVal = Math.Min(1.0, Math.Sqrt(maxRepetition) + patternLength);

            // Check if note even existed before, anomalyVal is high and repetitionVal is low
            if (!exists)
            {
                // A count of 1 gets 1, a count of 8+ gets 0
                double uniqueScale = Math.Pow(
                    Math.Pow(- Math.Min(7.0, anomalyVal - 1.0) / 7.0, 5.0) + 1.0,
                2.0);
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

        private static double CompareStrains(double strain1, double strain2, LinearSpline fractionSpline)
        {
            if (strain1 == 0 || strain2 == 0)
                return 1;

            double fraction = Math.Max(strain1 / strain2, strain2 / strain1);

            return Math.Max(0.0, fractionSpline.Interpolate(fraction)); // spline can sometimes dip below 0 which breaks everything 
        }
    }
}
