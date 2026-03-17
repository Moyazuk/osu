// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Difficulty.Utils
{
    public struct Bin
    {
        public double Difficulty;
        public double Time;
        public double NoteCount;
        public double FlowFraction;

        /// <summary>
        /// Creates bins using 2D quantile-based binning.
        /// First splits notes into time quantiles (equal note counts), then splits each time quantile into difficulty quantiles.
        /// </summary>
        public static List<Bin> CreateBins(List<double> difficulties, List<double> times, List<double> flowFractions, int difficultyDimensionLength, int timeDimensionLength)
        {
            if (difficulties.Count == 0 || times.Count == 0 || difficultyDimensionLength <= 0 || timeDimensionLength <= 0)
                return new List<Bin>();

            int n = difficulties.Count;
            var bins = new List<Bin>();

            int notesPerTimeQuantile = (int)Math.Ceiling((double)n / timeDimensionLength);

            for (int timeQuantile = 0; timeQuantile < timeDimensionLength; timeQuantile++)
            {
                int startIdx = timeQuantile * notesPerTimeQuantile;
                int endIdx = Math.Min(startIdx + notesPerTimeQuantile, n);

                if (startIdx >= n) break;

                int quantileSize = endIdx - startIdx;

                var quantileDifficulties = new List<double>(quantileSize);
                var quantileTimes = new List<double>(quantileSize);
                var quantileFlowFractions = new List<double>(quantileSize);

                for (int i = startIdx; i < endIdx; i++)
                {
                    quantileDifficulties.Add(difficulties[i]);
                    quantileTimes.Add(times[i]);
                    quantileFlowFractions.Add(flowFractions[i]);
                }

                int[] sortedIndices = Enumerable.Range(0, quantileSize)
                                                .OrderBy(i => quantileDifficulties[i])
                                                .ToArray();

                int notesPerDiffQuantile = (int)Math.Ceiling((double)quantileSize / difficultyDimensionLength);

                for (int diffQuantile = 0; diffQuantile < difficultyDimensionLength; diffQuantile++)
                {
                    int diffStartIdx = diffQuantile * notesPerDiffQuantile;
                    int diffEndIdx = Math.Min(diffStartIdx + notesPerDiffQuantile, quantileSize);

                    if (diffStartIdx >= quantileSize)
                        break;

                    double diffSum = 0;
                    double timeSum = 0;
                    double flowSum = 0;
                    int count = diffEndIdx - diffStartIdx;

                    for (int i = diffStartIdx; i < diffEndIdx; i++)
                    {
                        int originalIdx = sortedIndices[i];
                        diffSum += quantileDifficulties[originalIdx];
                        timeSum += quantileTimes[originalIdx];
                        flowSum += quantileFlowFractions[originalIdx];
                    }

                    bins.Add(new Bin
                    {
                        Difficulty = diffSum / count,
                        Time = timeSum / count,
                        NoteCount = count,
                        FlowFraction = flowSum / count
                    });
                }
            }

            return bins.OrderBy(b => b.Time).ToList();
        }
    }
}
