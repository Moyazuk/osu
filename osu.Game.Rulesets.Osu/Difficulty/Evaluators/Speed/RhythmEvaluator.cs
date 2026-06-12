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
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class RhythmEvaluator
    {
        private const int history_time_max = 5 * 1000; // 5 seconds
        private const int history_objects_max = 32;
        private const double rhythm_overall_multiplier = 1.0;
        private const double fcontrol_gallop_midpoint = 37.0;
        private const double fcontrol_exp_compression = 0.5;
        private const double fcontrol_multiplier = 0.13;
        /*
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
        */
        /// <summary>
        /// Calculates a rhythm multiplier for the difficulty of the tap associated with historic data of the current <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var currentOsuObject = (OsuDifficultyHitObject)current;

            double rhythmComplexitySum = 0;

            double deltaDifferenceEpsilon = ((OsuDifficultyHitObject)current).HitWindow(HitResult.Great) * 0.3;

            var island = new Island(deltaDifferenceEpsilon);
            var previousIsland = new Island(deltaDifferenceEpsilon);

            // we can't use dictionary here because we need to compare island with a tolerance
            // which is impossible to pass into the hash comparer
            var islandCounts = new List<(Island Island, int Count)>();

            double startRatio = 1; // store the ratio of the current start of an island to buff for tighter rhythms

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

                double effectiveRatio = getEffectiveRatio(deltaDifference);

                // if previous object is a slider it might be easier to tap since you don't have to do a whole tapping motion
                // while a full deltatime might end up some weird ratio the "unpress->tap" motion might be simple
                // for example a slider-circle-circle pattern should be evaluated as a regular triple and not as a single->double
                if (prevObj.BaseObject is Slider)
                {
                    double sliderLazyEndDelta = currObj.MinimumJumpTime;
                    double sliderLazyDeltaDifference = Math.Max(sliderLazyEndDelta, currDelta) / Math.Min(sliderLazyEndDelta, currDelta);

                    double sliderRealEndDelta = currObj.LastObjectEndDeltaTime;
                    double sliderRealDeltaDifference = Math.Max(sliderRealEndDelta, currDelta) / Math.Min(sliderRealEndDelta, currDelta);

                    double sliderEffectiveRatio = Math.Min(getEffectiveRatio(sliderLazyDeltaDifference), getEffectiveRatio(sliderRealDeltaDifference));
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
                        effectiveRatio *= 0.5;

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
                        if (island.DeltaCount > 0)
                        {
                            islandCounts.Add((island, 1));
                        }

                        // new island type - buff ratio by how many unique islands there are in the history, the more islands the more unpredictable the rhythm is
                        effectiveRatio *= Math.Max(0, Math.Pow(islandCounts.Count / 3.0, 1.75));
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

            return rhythmComplexitySum;
        }

        private static double getEffectiveRatio(double deltaDifference)
        {
            var ratioMultipliers = new[]
            {
                (1.0, 0.01), // same rhythm
                (4.0 / 3.0, 2.0), // 1/4 <-> 1/3
                (1.5, 1.5), // 1/3 <-> 1/2
                (5.0 / 3.0, 3.0), // 1/5 <-> 1/3
                (2.0, 0.05), // 1/4 <-> 1/2
                (2.5, 1.5), // 1/5 <-> 1/2
                (3.0, 0.25), // 1/3 <-> 1/1
                (4.0, 0.0) // 1/4 <-> 1/1
            };

            return LerpFromArrays(ratioMultipliers, deltaDifference);
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

        private static double getBaseFingerControlDifficulty(double currDelta, double prevDelta)
        {
            // Get basic taps per second associated with the two objects
            double prevVelocity = 1000.0 / prevDelta;
            double currVelocity = 1000.0 / currDelta;

            // Calculate the physical acceleration over the current time gap
            double dtInSeconds = currDelta / 1000.0;
            double rawKinematicAccel = (currVelocity - prevVelocity) / dtInSeconds;

            // Gallop factor to smoothly dampen patterns that can be mashed
            double gallopFactor = 1.0 / (1.0 + Math.Exp(0.5 * (currDelta - fcontrol_gallop_midpoint)));

            // Compress the raw acceleration to reflect nonlinear physical effort
            double compressedAccel = Math.Sign(rawKinematicAccel) * Math.Pow(Math.Abs(rawKinematicAccel), fcontrol_exp_compression);

            compressedAccel *= 1.0 - gallopFactor;

            return Math.Abs(compressedAccel);
        }

        public static double EvaluateBurstAccelerationOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index < 1)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double currDelta = Math.Max(osuCurrObj.DeltaTime, 1e-7);
            double prevDelta = Math.Max(osuPrevObj.DeltaTime, 1e-7);

            // keep the epsilon gate: timestamp jitter within the same nominal rhythm
            // produces small nonzero accel you don't want to count
            double epsilon = osuCurrObj.HitWindow(HitResult.Great) * 0.3;
            double gate = Math.Clamp((Math.Abs(prevDelta - currDelta) - epsilon) / epsilon, 0, 1);

            return gate * getBaseFingerControlDifficulty(currDelta, prevDelta);
        }
    }
}
