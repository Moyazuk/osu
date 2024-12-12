// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Aggregation
{
    public abstract class OsuProbabilitySkill : Skill
    {
        protected OsuProbabilitySkill(Mod[] mods)
            : base(mods)
        {
        }

        // We assume players have a 2% chance to hit every note in the map.
        // A higher value of fc_probability increases the influence of difficulty spikes,
        // while a lower value increases the influence of length and consistent difficulty.
        private const double fc_probability = 0.02;

        private const int bin_count = 32;

        // The number of difficulties there must be before we can be sure that binning difficulties would not change the output significantly.
        private double binThreshold => 2 * bin_count;

        private readonly List<double> difficulties = new List<double>();

        /// <summary>
        /// Returns the strain value at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double StrainValueAt(DifficultyHitObject current);

        // used to send timestamps to osu!tools
        private double totalElapsedTime = 0;
        private readonly List<double> timestamps = new List<double>();

        public override void Process(DifficultyHitObject current)
        {
            //used to send timestamps to osu!tools
            timestamps.Add(totalElapsedTime);
            difficulties.Add(StrainValueAt(current));
            totalElapsedTime += current.DeltaTime;
        }

        protected abstract double HitProbability(double skill, double difficulty);

        // used to send timestamps and difficulty information to osu!tools
        public IEnumerable<(double Timestamp, double Value)> GetCurrentStrainPeaks()
        {
            return timestamps.Zip(difficulties, (timestamp, value) => (timestamp, value));
        }

        private double difficultyValueExact()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcProbability(skill) - fc_probability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;

            double fcProbability(double s)
            {
                if (s <= 0) return 0;

                return difficulties.Aggregate<double, double>(1, (current, d) => current * HitProbability(s, d));
            }
        }

        private double difficultyValueBinned()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            var bins = Bin.CreateBins(difficulties, bin_count);

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcProbability(skill) - fc_probability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;

            double fcProbability(double s)
            {
                if (s <= 0) return 0;

                return bins.Aggregate(1.0, (current, bin) => current * Math.Pow(HitProbability(s, bin.Difficulty), bin.Count));
            }
        }

        public override double DifficultyValue()
        {
            if (difficulties.Count == 0) return 0;

            return difficulties.Count > binThreshold ? difficultyValueBinned() : difficultyValueExact();
        }

        /// <returns>
        /// A polynomial fitted to the miss counts at each skill level.
        /// </returns>
        public ExpPolynomial GetMissPenaltyCurve()
        {
            double[] missCounts = new double[7];
            double[] penalties = { 1, 0.95, 0.9, 0.8, 0.6, 0.3, 0 };

            ExpPolynomial missPenaltyCurve = new ExpPolynomial();

            // If there are no notes, we just return the curve with all coefficients set to zero.
            if (difficulties.Count == 0 || difficulties.Max() == 0)
                return missPenaltyCurve;

            double fcSkill = DifficultyValue();

            var bins = Bin.CreateBins(difficulties, bin_count);

            for (int i = 0; i < penalties.Length; i++)
            {
                if (i == 0)
                {
                    missCounts[i] = 0;
                    continue;
                }

                double penalizedSkill = fcSkill * penalties[i];

                missCounts[i] = getMissCountAtSkill(penalizedSkill, bins);
            }

            missPenaltyCurve.Fit(missCounts);

            return missPenaltyCurve;
        }

        /// <summary>
        /// Find the lowest miss count that a player with the provided <paramref name="skill"/> would have a 2% chance of achieving or better.
        /// </summary>
        private double getMissCountAtSkill(double skill, List<Bin> bins)
        {
            double maxDiff = difficulties.Max();

            if (maxDiff == 0)
                return 0;
            if (skill <= 0)
                return difficulties.Count;

            var poiBin = difficulties.Count > binThreshold ? new PoissonBinomial(bins, skill, HitProbability) : new PoissonBinomial(difficulties, skill, HitProbability);

            return Math.Max(0, RootFinding.FindRootExpand(x => poiBin.CDF(x) - fc_probability, -50, 1000, accuracy: 1e-4));
        }
    }
}
