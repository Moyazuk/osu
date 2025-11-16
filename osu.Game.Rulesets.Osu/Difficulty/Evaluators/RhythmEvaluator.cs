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

        /// <summary>
        /// Calculates a rhythm multiplier for the difficulty of the tap associated with historic data of the current <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, OsuDifficultyTuning tuning)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var currentOsuObject = (OsuDifficultyHitObject)current;

            var previousRatioMultipliers = createPreviousRatioTable(tuning);
            var nextRatioMultipliers = createNextRatioTable(tuning);

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

                // Use custom cap value to ensure that that at this point delta time is actually zero
                double currDelta = Math.Max(currObj.DeltaTime, 1e-7);
                double prevDelta = Math.Max(prevObj.DeltaTime, 1e-7);

                double deltaDifference = Math.Max(prevDelta, currDelta) / Math.Min(prevDelta, currDelta);

                bool isSpeedingUp = prevDelta > currDelta + deltaDifferenceEpsilon;

                double effectiveRatio = LerpFromArrays(previousRatioMultipliers, deltaDifference);

                if (prevObj.BaseObject is Slider)
                {
                    // if previous object is a slider it might be easier to tap since you dont have to do a whole tapping motion
                    // while a full deltatime might end up some weird ratio
                    // the "unpress->taps" motion might be simple, for example a slider-circle-circle pattern is being evaluated as a triple and not a single->double
                    double sliderEndDelta = currObj.MinimumJumpTime;
                    double sliderDeltaDifference = Math.Max(sliderEndDelta, currDelta) / Math.Min(sliderEndDelta, currDelta);
                    double sliderEffectiveRatio = LerpFromArrays(previousRatioMultipliers, sliderDeltaDifference);

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

                    // repeated island polarity (2 -> 4, 3 -> 5)
                    if (island.IsSimilarPolarity(previousIsland))
                        effectiveRatio *= 0.5;

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

                    rhythmComplexitySum += effectiveRatio * currHistoricalDecay;

                    startRatio = effectiveRatio;

                    previousIsland = island;

                    island = new Island((int)currDelta, deltaDifferenceEpsilon);
                }

                prevObj = currObj;
            }

            var next = current.Next(0) as OsuDifficultyHitObject;

            if (next != null)
            {
                double currDelta = Math.Max(currentOsuObject.DeltaTime, 1e-7);
                double nextDelta = Math.Max(next.DeltaTime, 1e-7);
                double nextDeltaDifference = Math.Max(currDelta, nextDelta) / Math.Min(currDelta, nextDelta);

                double nextRatio = LerpFromArrays(nextRatioMultipliers, nextDeltaDifference);

                double doubletapness = currentOsuObject.GetDoubletapness(next);

                rhythmComplexitySum *= nextRatio;
                rhythmComplexitySum *= 1 - doubletapness * 0.75;
            }

            double rhythmDifficulty = Math.Sqrt(4 + rhythmComplexitySum * tuning.RhythmOverallScale) / 2.0;
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
                // single delta islands shouldn't be compared
                if (DeltaCount <= 1 || other.DeltaCount <= 1)
                    return false;

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
