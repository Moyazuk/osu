// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed
{
    public static class RhythmEvaluator
    {
        private const double tau_0 = 0.180; // 180ms base IIR constant
        private const double reset_start = 1.5; // Memory wipe starts at 1.5s
        private const double reset_end = 3.0; // Total wipe at 3.0s
        private const double sigmoid_x0_kn = 1.4; // Physical difficulty inflection point
        private const double sigmoid_k_kn = 2.5; // Physical sigmoid steepness
        private const double sigmoid_x0_h = 1.5; // Cognitive difficulty inflection point (UNUSED)
        private const double sigmoid_k_h = 4.0; // Cognitive sigmoid steepness (UNUSED)
        private const double gamma = 0.70; // Frequency-dependent fatigue factor

        /// <summary>
        /// Calculates a physical and cognitive multiplier for the rhythmic complexity of the tap associated with historic data of the current <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            // Spinners are assumed to have no rhythmic complexity
            if (current.BaseObject is Spinner)
                return 0;

            var currentOsu = (OsuDifficultyHitObject)current;
            var previousOsu = current.Index > 0 ? (OsuDifficultyHitObject)current.Previous(0) : null;

            // Context restoration: get previous rhythm state if it exists
            // Epsilon scales with the Great hit result - used for anti-mash and denoising
            var state = previousOsu?.RhythmState ?? new OsuDifficultyHitObject.RhythmScaleStates();
            double dt = current.DeltaTime / 1000.0;
            double epsilon = currentOsu.HitWindow(HitResult.Great) * 0.3 / 1000.0;

            // If enough time has passed since the last hit object, clear the rhythm state entirely (0.0)
            // Main method of avoiding complexity spikes due to breaks
            // Otherwise, decay the energy exponentially since last hit object, and accelerate the decay if between 1.5 and 3.0s (smoothing clearing of rhythm state)
            // This is the principle of an IIR filter / leaky integrator, and can be thought of as a series of "rhythm strains"
            if (dt > reset_end)
            {
                state.ClearRhythmState();
            }
            else
            {
                double alpha = Math.Clamp((dt - reset_start) / (reset_end - reset_start), 0, 1);
                state.ApplyDecay(dt, tau_0, 1 - alpha);
            }

            // For 6 different rhythm scales, ranging from finest to coarsest:
            for (int i = 0; i < 6; i++)
            {
                // Look at the note that was 2^i notes ago
                int lookback = 1 << i;

                // If no such note exists, skip processing
                if (current.Index <= lookback) continue;

                double s1 = currentOsu.Previous(0).StartTime / 1000.0;
                double s2 = currentOsu.Previous(lookback).StartTime / 1000.0;

                // Also skip processing across major gaps
                if (currentOsu.StartTime / 1000.0 - s2 > reset_end) continue;

                // Linearly predict the expected rhythmic interval, and extract the difference between expectation and reality
                double avgInterval = Math.Max(epsilon, (s1 - s2) / lookback);
                double predictedT = s1 + avgInterval;
                double rawDiff = currentOsu.StartTime / 1000.0 - predictedT;

                double normDiff = rawDiff / (avgInterval + 1e-6);

                // Anti-mash gate - avoid rewarding doubletaps or mashable patterns at the signal level
                double antiMashGate = 1.0 / (1.0 + Math.Exp(-100 * (dt - epsilon)));

                // Denoise gate - prevent minor redline or BPM changes from being overvalued
                double denoiseGate = (rawDiff * rawDiff) / (rawDiff * rawDiff + epsilon * epsilon);

                // Ratio gate - empirically buff weird rhythm energy and nerf energies of power-of-two rhythms
                // This is some "magic" that can't be eliminated yet, but *partially* mitigates start-stop streams being overvalued
                double ratio = dt / (avgInterval + 1e-6);
                double ratioWeight = getEffectiveRatio(ratio);
                if (ratio > 4.5) ratioWeight = 0.01; // This apparently nerfs buzzsliders into stamina streams
                double surprise = Math.Sqrt(Math.Abs(normDiff * denoiseGate * antiMashGate)) * ratioWeight;

                // Cap the max possible surprise to 1 and scale with the previously computed DeltaTime
                double tau = tau_0 * lookback;
                double energyAdd = surprise * (1.0 - Math.Exp(-dt / tau));

                // Preserved legacy nerfs
                if (currentOsu.BaseObject is Slider) energyAdd *= 0.5; // Into slider nerf
                if (previousOsu?.BaseObject is Slider) energyAdd *= 0.3; // From slider nerf

                // Doubletap nerf scaling at the post-processing level - does something different from the anti-mash gate
                double doubletapness = previousOsu?.GetDoubletapness(currentOsu) ?? 0;
                energyAdd *= 1.0 - doubletapness * 0.75;

                // Accumulate state
                state.AddEnergy(i, energyAdd);
            }

            // Final state storage and metric calculation
            currentOsu.RhythmState = state;
            return calculateComplexityMetrics(state);
        }

        private static double calculateComplexityMetrics(OsuDifficultyHitObject.RhythmScaleStates s)
        {
            // Scales from finest (note-to-note) to coarsest (1-note predictor to 32-note predictor)
            double[] energies = { s.E0, s.E1, s.E2, s.E3, s.E4, s.E5 };

            // Physical component - proxy for finger control
            // "Pseudo-entropy" that weighs finer scales more due to faster muscle twitches being more confusing - subject to debate
            double rawKn = 0;

            for (int i = 0; i < energies.Length; i++)
            {
                rawKn += energies[i] * Math.Pow(gamma, i);
            }

            // Cognitive component - proxy for rhythm reading
            // Straightforward Shannon entropy across scales derived from information theory
            // This is ignored because log summation gives me hilarious values if applied to tap/speed
            double totalEnergy = s.RhythmScaleTotalEnergy + 1e-9;
            double rawH = 0;

            for (int i = 0; i < energies.Length; i++)
            {
                double p = energies[i] / totalEnergy;
                if (p > 1e-4) rawH -= p * Math.Log(p, 2); // Shannon entropy log summation
            }

            // Final difficulty multiplier will simply be kn since this is being applied only to tap/speed currently
            // Ideally kn and h need to be separately handled
            // Returns a value [1.0, 2.0]
            return Math.Pow(rawKn, 3);
        }

        private static double getEffectiveRatio(double ratio)
        {
            // High baseline: everything is hard unless it's a known simple ratio
            const double base_complexity = 2.0;

            var nerfs = new[]
            {
                (1.0, 0.01, 0.04),
                (2.0, 0.05, 0.06),
                (1.0 / 2.0, 0.02, 0.04),
                (4.0, 0.00, 0.10),
                (1.0 / 4.0, 0.01, 0.04),
                (3.0, 0.25, 0.08),
                (1.0 / 3.0, 0.25, 0.08),
            };

            double minMultiplier = base_complexity;

            foreach ((double anchor, double target, double width) in nerfs)
            {
                // If we are near a simple multiple, pull the complexity down
                // An alternative to linearly interpolating from arrays is Gaussian windows
                double dist = ratio - anchor;
                double factor = Math.Exp(-(dist * dist) / (2 * width * width));
                minMultiplier = Math.Min(minMultiplier, Math.Max(target, base_complexity * (1 - factor)));
            }

            return minMultiplier;
        }
    }
}
