// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed
{
    public static class RhythmEvaluator
    {
        private const int history_time_max = 5 * 1000; // 5 seconds
        private const int history_islands_max = 8;
        private const double rhythm_overall_multiplier = 0.95;

        private static double single_note_island_difficulty => 0.7;
        private static double speedup_multiplier => 0.65;
        private static double consecutive_speedup_multiplier => 0.125;
        private static double repeated_length_multiplier => 0.5;
        private static double repeated_polarity_multiplier => 0.5;
        private static double slider_boundary_multiplier => 0.6;

        private static double gap_speed_multiplier => 40.0; // tune this to bring the divided bonus back to a comparable scale

        /// <summary>
        /// Calculates a rhythm multiplier for the difficulty of the tap associated with historic data of the current <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var currObj = (OsuDifficultyHitObject)current;
            var currentIsland = currObj.Island;

            // Notes without an island (e.g. directly spinner-adjacent) get a neutral multiplier.
            if (currentIsland == null)
                return 1.0;

            double deltaDifferenceEpsilon = currObj.HitWindowGreat * 0.3;

            double rhythmComplexitySum = 0;
            double? transitionOut = null;

            var island = currentIsland;

            for (int depth = 0; depth < history_islands_max; depth++)
            {
                var previous = island.Previous(0);

                var next = island.Next(0);

                // No previous island, or rhythm continuity was severed (spinner) - stop walking.
                if (previous == null || island.StartDeltaTime == null)
                    break;

                double elapsed = current.StartTime - island.FirstObject.StartTime;

                if (elapsed > history_time_max)
                    break;

                double decay = (history_time_max - elapsed) / history_time_max;

                double transitionIn = transitionDifficulty(previous, island, deltaDifferenceEpsilon);

                double contribution;

                if (island.Length == 2 && previous.Length > 2 && next != null && next.Length > 2)
                    contribution = single_note_island_difficulty; // constant difficulty for single-note islands
                else
                    contribution = transitionOut == null ? transitionIn : Math.Sqrt(transitionIn * transitionOut.Value);

                rhythmComplexitySum += contribution * decay;

                transitionOut = transitionIn;
                island = previous;
            }

            // Long uninterrupted rhythm goes stale - scale the sum down as the current island drags on.
            // Uses notes elapsed *so far* rather than the island's full Length, so the nerf builds up
            // over the island instead of applying retroactively from its first note (which would leak
            // future information into early notes' strain).
            double notesElapsed = (current.StartTime - currentIsland.FirstObject.StartTime) / Math.Max(currentIsland.DeltaTime, OsuDifficultyHitObject.MIN_DELTA_TIME);
            rhythmComplexitySum *= DiffUtils.ReverseLerp(notesElapsed, 22, 3);

            return rhythmComplexitySum;
        }

        /// <summary>
        /// The rhythm difficulty of transitioning from <paramref name="previous"/> into <paramref name="island"/>,
        /// considering both the change in internal deltaTime and the gap between the two islands.
        /// </summary>
        private static double transitionDifficulty(OsuDifficultyHitIsland previous, OsuDifficultyHitIsland island, double deltaDifferenceEpsilon)
        {
            double prevDelta = Math.Max(previous.DeltaTime, OsuDifficultyHitObject.MIN_DELTA_TIME);
            double currDelta = Math.Max(island.DeltaTime, OsuDifficultyHitObject.MIN_DELTA_TIME);
            double gap = Math.Max(island.StartDeltaTime!.Value, OsuDifficultyHitObject.MIN_DELTA_TIME);

            // Signal 1: the change in internal deltaTime from one island to the next.
            double islandRatio = Math.Max(prevDelta, currDelta) / Math.Min(prevDelta, currDelta);
            double ratioDifficulty = getEffectiveDifficulty(islandRatio);

            // Signal 2: the gap between the islands as a rhythm event of its own,
            // measured against the internal spacing of both neighbours.
            double gapRatioPrev = Math.Max(gap, prevDelta) / Math.Min(gap, prevDelta);
            double gapRatioCurr = Math.Max(gap, currDelta) / Math.Min(gap, currDelta);
            double gapDifficulty = Math.Max(getEffectiveDifficulty(gapRatioPrev), getEffectiveDifficulty(gapRatioCurr));

            double ratioBonus = ratioDifficulty - 1.0;
            double gapBonus = gapDifficulty - 1.0;

            // Scale the gap's contribution inversely with the gap itself - a fast (small) gap
            // produces a larger divisor result, i.e. more difficulty; a slow (large) gap shrinks it.
            gapBonus *= gap_speed_multiplier / Math.Pow(gap, 1);

            double difficulty = 1.0 + ratioBonus * 1 + gapBonus;

            // reduce bonus for deltas that are large multiples of each other (1/1 -> 1/8 is different, not hard)
            difficulty *= Math.Clamp(2.0 - islandRatio / 8.0, 0.0, 1.0);

            // smooth the difficulty in near-threshold transitions that only just exceeded island tolerance
            double deltaDifference = Math.Abs(prevDelta - currDelta);
            difficulty *= Math.Clamp((deltaDifference - deltaDifferenceEpsilon) / deltaDifferenceEpsilon, 0, 1);

            bool speedingUp = currDelta < prevDelta;

            if (speedingUp)
                difficulty *= speedup_multiplier;

            // consecutive speedups (1/1 -> 1/2 -> 1/4): the intermediate step already got credit, don't double-buff
            if (speedingUp && previous.StartDeltaTime != null
                           && previous.Previous(0) is OsuDifficultyHitIsland prevPrev
                           && prevDelta + deltaDifferenceEpsilon < Math.Max(prevPrev.DeltaTime, OsuDifficultyHitObject.MIN_DELTA_TIME))
                difficulty *= consecutive_speedup_multiplier;

            // repeated island size (ex: triplet -> triplet)
            if (island.Length == previous.Length)
                difficulty *= repeated_length_multiplier;

            // repeated island polarity (2 -> 4, 3 -> 5) at similar speed
            if (island.Length > 1 && previous.Length > 1
                                  && island.Length % 2 == previous.Length % 2
                                  && Math.Abs(currDelta - prevDelta) < deltaDifferenceEpsilon)
                difficulty *= repeated_polarity_multiplier;

            // transitions into or out of sliders have relaxed tap timing
            if (previous.LastObject.BaseObject is Slider)
                difficulty *= slider_boundary_multiplier;

            if (island.FirstObject.BaseObject is Slider)
                difficulty *= slider_boundary_multiplier;

            // scale down if the transition is double-tappable
            difficulty *= 1 - previous.LastObject.CalculateDoubleTapFeasibility(island.FirstObject) * 0.75;

            // repeated identical islands in recent history get progressively less credit
            int occurrences = 1;
            var scan = previous;

            for (int j = 0; j < history_islands_max && scan != null; j++)
            {
                if (Math.Abs(scan.DeltaTime - currDelta) < deltaDifferenceEpsilon && scan.Length == island.Length)
                    occurrences++;

                scan = scan.Previous(0);
            }

            if (occurrences > 1)
            {
                double power = DiffUtils.Logistic(currDelta, maxValue: 2.75, multiplier: 0.24, midpointOffset: 58.33);
                difficulty *= Math.Min(3.0 / occurrences, DiffUtils.Pow(1.0 / occurrences, power));
            }

            return difficulty;
        }

        public static double CalculateStartOfFastPatternBonus(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);

            if (osuLastObj == null)
                return 0.0;

            // how much faster is this note than the previous one
            double speedupRatio = osuLastObj.AdjustedDeltaTime / osuCurrObj.AdjustedDeltaTime;

            const double speedupThreshold = 1.15;
            const double bonusStrength = 140000;

            if (speedupRatio <= speedupThreshold)
                return 0;

            double result = ((bonusStrength * (speedupRatio - speedupThreshold) / speedupThreshold) / osuLastObj.AdjustedDeltaTime) / Math.Max(osuCurrObj.AdjustedDeltaTime, 35);

            double doubleTapFeasibility = 1.0 - osuCurrObj.CalculateDoubleTapFeasibility((OsuDifficultyHitObject?)osuCurrObj.Next(0));

            return result * doubleTapFeasibility;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double getEffectiveDifficulty(double deltaDifferenceRatio)
        {
            const double rhythm_ratio_difficulty_multiplier = 26.0;

            // Take only the fractional part of the value since we're only interested in punishing multiples
            double deltaDifferenceFraction = deltaDifferenceRatio - Math.Truncate(deltaDifferenceRatio);

            return 1.0 + rhythm_ratio_difficulty_multiplier * Math.Min(0.5, DiffUtils.SmoothstepBellCurve(deltaDifferenceFraction));
        }
    }
}
