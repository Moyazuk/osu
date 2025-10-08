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

        private static (double ratio, double multiplier)[] ratioMultipliers = new[]
        {
            (1.0, 0.01), // same rhythm
            (4.0 / 3.0, 2.0), // 1/4 <-> 1/3
            (1.5, 1.0), // 1/3 <-> 1/2
            (5.0 / 3.0, 4.0), // 1/5 <-> 1/3
            (2.0, 0.1), // 1/4 <-> 1/2
            (2.5, 1.2), // 1/5 <-> 1/2
            (3.0, 0.25), // 1/3 <-> 1/1
            (4.0, 0.0) // 1/4 <-> 1/1
        };

        /// <summary>
        /// Calculates a rhythm multiplier for the difficulty of the tap associated with historic data of the current <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            ratioMultipliers = new[]
            {
                (1.0, 0.01), // same rhythm
                (4.0 / 3.0, 3.0), // 1/4 <-> 1/3
                (1.5, 1.25), // 1/3 <-> 1/2
                (5.0 / 3.0, 3.0), // 1/5 <-> 1/3
                (2.0, 0.2), // 1/4 <-> 1/2
                (2.5, 1.2), // 1/5 <-> 1/2
                (3.0, 0.25), // 1/3 <-> 1/1
                (4.0, 0.0) // 1/4 <-> 1/1
            };

            var currentOsuObject = (OsuDifficultyHitObject)current;

            double rhythmComplexitySum = 0;

            double deltaDifferenceEpsilon = ((OsuDifficultyHitObject)current).HitWindowGreat * 0.3;

            var island = new Island(deltaDifferenceEpsilon);
            var previousIsland = new Island(deltaDifferenceEpsilon);

            // we can't use dictionary here because we need to compare island with a tolerance
            // which is impossible to pass into the hash comparer
            var islandCounts = new List<(Island Island, int Count)>();

            double startRatio = 0; // store the ratio of the current start of an island to buff for tighter rhythms

            int historicalNoteCount = Math.Min(current.Index, history_objects_max);

            int rhythmStart = 0;

            while (rhythmStart < historicalNoteCount - 2 && current.StartTime - current.Previous(rhythmStart).StartTime < history_time_max)
                rhythmStart++;

            OsuDifficultyHitObject prevObj = (OsuDifficultyHitObject)current.Previous(rhythmStart);

            // we go from the furthest object back to the current one
            for (int i = rhythmStart; i > 0; i--)
            {
                OsuDifficultyHitObject currObj = (OsuDifficultyHitObject)current.Previous(i - 1);

                // scales note 0 to 1 from history to now
                double timeDecay = (history_time_max - (current.StartTime - currObj.StartTime)) / history_time_max;
                double noteDecay = (double)(historicalNoteCount - i) / historicalNoteCount;

                double currHistoricalDecay = Math.Min(noteDecay, timeDecay); // either we're limited by time or limited by object count.

                double currDelta = currObj.StrainTime;
                double prevDelta = prevObj.StrainTime;

                double deltaDifference = Math.Max(prevDelta, currDelta) / Math.Min(prevDelta, currDelta);

                bool isSpeedingUp = prevDelta > currDelta + deltaDifferenceEpsilon;

                double effectiveRatio = LerpFromArrays(ratioMultipliers, deltaDifference);

                if (prevObj.BaseObject is Slider)
                {
                    // if previous object is a slider it might be easier to tap since you dont have to do a whole tapping motion
                    // while a full deltatime might end up some weird ratio
                    // the "unpress->taps" motion might be simple, for example a slider-circle-circle pattern is being evaluated as a triple and not a single->double
                    double sliderEndDelta = currObj.MinimumJumpTime;
                    double sliderDeltaDifference = Math.Max(sliderEndDelta, currDelta) / Math.Min(sliderEndDelta, currDelta);
                    double sliderEffectiveRatio = LerpFromArrays(ratioMultipliers, sliderDeltaDifference);

                    effectiveRatio = Math.Min(sliderEffectiveRatio, effectiveRatio);
                }

                if (isSpeedingUp)
                    effectiveRatio *= 0.5;

                if (Math.Abs(prevDelta - currDelta) < deltaDifferenceEpsilon)
                {
                    // island is still progressing
                    island.AddDelta((int)currDelta);
                }
                else
                {
                    // bpm change is into slider, this is easy acc window
                    // TODO: `if (mods.classic)`
                    if (currObj.BaseObject is Slider)
                        effectiveRatio *= 0.35;

                    var islandCount = islandCounts.FirstOrDefault(x => x.Island.Equals(island));

                    if (islandCount != default)
                    {
                        int countIndex = islandCounts.IndexOf(islandCount);

                        // only add island to island counts if they're going one after another
                        if (previousIsland.Equals(island))
                            islandCount.Count++;

                        // repeated island (ex: triplet -> triplet)
                        double power = DifficultyCalculationUtils.Logistic(island.Delta, maxValue: 0.75, multiplier: 0.24, midpointOffset: 58.33);
                        effectiveRatio *= Math.Min(5.0 / islandCount.Count, Math.Pow(1.0 / islandCount.Count, power));

                        islandCounts[countIndex] = (islandCount.Island, islandCount.Count);
                    }
                    else
                    {
                        islandCounts.Add((island, 1));
                    }

                    // scale down the difficulty if the object is doubletappable
                    double doubletapness = prevObj.GetDoubletapness(currObj);
                    effectiveRatio *= 1 - doubletapness * 0.75;

                    rhythmComplexitySum += effectiveRatio * Math.Sqrt(startRatio) * currHistoricalDecay;

                    startRatio = effectiveRatio;

                    previousIsland = island;

                    island = new Island((int)currDelta, deltaDifferenceEpsilon);
                }

                prevObj = currObj;
            }

            double rhythmDifficulty = Math.Sqrt(4 + rhythmComplexitySum * 3.2) / 2.0; // produces multiplier that can be applied to strain. range [1, infinity) (not really though)

            rhythmDifficulty *= 1 - currentOsuObject.GetDoubletapness((OsuDifficultyHitObject)current.Next(0));
            return rhythmDifficulty;
        }

        private class Island : IEquatable<Island>
        {
            private readonly double deltaDifferenceEpsilon;

            public Island(double epsilon)
            {
                deltaDifferenceEpsilon = epsilon;
            }

            public Island(int delta, double epsilon)
            {
                deltaDifferenceEpsilon = epsilon;
                Delta = Math.Max(delta, OsuDifficultyHitObject.MIN_DELTA_TIME);
                DeltaCount++;
            }

            public int Delta { get; private set; } = int.MaxValue;
            public int DeltaCount { get; private set; }

            public void AddDelta(int delta)
            {
                if (Delta == int.MaxValue)
                    Delta = Math.Max(delta, OsuDifficultyHitObject.MIN_DELTA_TIME);

                DeltaCount++;
            }

            public bool IsSimilarPolarity(Island other)
            {
                // TODO: consider islands to be of similar polarity only if they're having the same average delta (we don't want to consider 3 singletaps similar to a triple)
                //       naively adding delta check here breaks _a lot_ of maps because of the flawed ratio calculation
                return Math.Abs(Delta - other.Delta) < deltaDifferenceEpsilon &&
                       DeltaCount % 2 == other.DeltaCount % 2;
            }

            public bool Equals(Island? other)
            {
                if (other == null)
                    return false;

                return Math.Abs(Delta - other.Delta) < deltaDifferenceEpsilon &&
                       DeltaCount == other.DeltaCount;
            }

            public override string ToString()
            {
                return $"{Delta}x{DeltaCount}";
            }
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
