// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Aggregation
{
    public abstract class OsuTimeSkill : Skill
    {
        protected OsuTimeSkill(Mod[] mods)
            : base(mods)
        {
        }

        // Assume players spend 12 minutes retrying a map before they FC
        private const double time_threshold = 12;

        // The width of each dimension of the bins. Since the array of bins is 2 dimensional, the number of bins is equal to these values multiplied together.
        private const int difficulty_bin_count = 8;
        private const int time_bin_count = 16;

        private readonly List<double> difficulties = new List<double>();
        private readonly List<double> times = new List<double>();

        /// <summary>
        /// Returns the strain value at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double StrainValueAt(DifficultyHitObject current);

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(StrainValueAt(current));

            // Cap the delta time of a given note at 5 seconds to not reward absurdly long breaks
            times.Add(times.LastOrDefault() + Math.Min(current.DeltaTime, 5000));
        }

        protected abstract double HitProbability(double skill, double difficulty);

        public double DifficultyValueExact()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            const double lower_bound_estimate = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcTime(skill) - time_threshold * 60000,
                lower_bound_estimate,
                upperBoundEstimate);

            return skill;

            double fcTime(double s)
            {
                if (s <= 0) return double.PositiveInfinity;

                double t = 0;
                double prodOfHitProbabilities = 1;

                for (int n = difficulties.Count - 1; n >= 0; n--)
                {
                    double deltaTime = n > 0 ? times[n] - times[n - 1] : times[n];

                    prodOfHitProbabilities *= HitProbability(s, difficulties[n]);
                    t += deltaTime / prodOfHitProbabilities - deltaTime;
                }

                return t;
            }
        }

        public double DifficultyValueBinned()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            var bins = Bin.CreateBins(difficulties, times, difficulty_bin_count, time_bin_count);

            const double lower_bound_estimate = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcTime(skill) - time_threshold * 60000,
                lower_bound_estimate,
                upperBoundEstimate);

            return skill;

            double fcTime(double s)
            {
                if (s <= 0) return double.PositiveInfinity;

                double t = 0;
                double prodOfHitProbabilities = 1;

                for (int timeIndex = time_bin_count - 1; timeIndex >= 0; timeIndex--)
                {
                    double deltaTime = times.LastOrDefault() / time_bin_count;

                    for (int difficultyIndex = 0; difficultyIndex < difficulty_bin_count; difficultyIndex++)
                    {
                        Bin bin = bins[difficulty_bin_count * timeIndex + difficultyIndex];

                        prodOfHitProbabilities *= Math.Pow(HitProbability(s, bin.Difficulty), bin.Count);
                    }

                    t += deltaTime / prodOfHitProbabilities - deltaTime;
                }

                return t;
            }
        }

        public override double DifficultyValue()
        {
            if (difficulties.Count == 0) return 0;

            return difficulties.Count > time_bin_count * difficulty_bin_count ? DifficultyValueBinned() : DifficultyValueExact();
        }

        /// <summary>
        /// The coefficients of a quartic fitted to the miss counts at each skill level.
        /// </summary>
        /// <returns>The coefficients for ax^4+bx^3+cx^2. The 4th coefficient for dx^1 can be deduced from the first 3 in the performance calculator.</returns>
        public Polynomial GetMissPenaltyCurve()
        {
            double[] missCounts = new double[7];
            double[] penalties = { 1, 0.95, 0.9, 0.8, 0.6, 0.3, 0 };

            Polynomial missPenaltyCurve = new Polynomial();

            // If there are no notes, we just return the polynomial with all coefficients 0.
            if (difficulties.Count == 0 || difficulties.Max() == 0)
                return missPenaltyCurve;

            double fcSkill = DifficultyValue();

            var bins = Bin.CreateBins(difficulties, times, difficulty_bin_count, time_bin_count);

            for (int i = 0; i < penalties.Length; i++)
            {
                if (i == 0)
                {
                    missCounts[i] = 0;
                    continue;
                }

                double penalizedSkill = fcSkill * penalties[i];

                missCounts[i] = Math.Log(getMissCountAtSkill(penalizedSkill, bins) + 1);
            }

            missPenaltyCurve.Fit(missCounts);

            return missPenaltyCurve;
        }

        /// <summary>
        /// Find the lowest misscount that a player with the provided <paramref name="skill"/> would likely achieve within 12 minutes of retrying.
        /// </summary>
        private double getMissCountAtSkill(double skill, List<Bin> bins)
        {
            double maxDiff = difficulties.Max();
            double endTime = times.Max();

            if (maxDiff == 0)
                return 0;
            if (skill <= 0)
                return difficulties.Count;

            IterativePoissonBinomial poiBin = new IterativePoissonBinomial();

            double timeAtMissCountAtSkill(double missCount)
            {
                poiBin.Reset();

                if (difficulties.Count > time_bin_count * difficulty_bin_count)
                {
                    double binTimeSteps = endTime / time_bin_count;

                    double totalTime = 0;

                    for (int timeIndex = 0; timeIndex < time_bin_count; timeIndex++)
                    {
                        for (int difficultyIndex = 0; difficultyIndex < difficulty_bin_count; difficultyIndex++)
                        {
                            Bin bin = bins[timeIndex * difficulty_bin_count + difficultyIndex];

                            double missProb = 1 - HitProbability(skill, bin.Difficulty);
                            poiBin.AddBinnedProbabilities(missProb, bin.Count);
                        }

                        totalTime += binTimeSteps * poiBin.CDF(missCount);
                    }

                    if (poiBin.CDF(missCount) < 1e-10)
                        return double.PositiveInfinity;

                    return (totalTime / poiBin.CDF(missCount) - endTime) / 60000;
                }
                else
                {
                    double totalTime = 0;

                    for (int i = 0; i < difficulties.Count; i++)
                    {
                        double deltaTime = i > 0 ? times[i] - times[i - 1] : times[i];

                        double missProb = 1 - HitProbability(skill, difficulties[i]);
                        poiBin.AddProbability(missProb);

                        totalTime += deltaTime * poiBin.CDF(missCount);
                    }

                    if (poiBin.CDF(missCount) < 1e-10)
                        return double.PositiveInfinity;

                    return (totalTime / poiBin.CDF(missCount) - endTime) / 60000;
                }
            }

            return Math.Max(0, RootFinding.FindRootExpand(x => timeAtMissCountAtSkill(x) - time_threshold, -50, 1000, accuracy: 0.01));
        }

        public double CountTopWeightedStrains() => OsuStrainUtils.CountTopWeightedSliders(difficulties, DifficultyValue());
    }
}
