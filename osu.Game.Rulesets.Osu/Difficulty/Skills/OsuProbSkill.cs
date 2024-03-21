// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuProbSkill : Skill
    {
        protected OsuProbSkill(Mod[] mods)
            : base(mods)
        {
        }

        private const double fc_probability = 0.02;

        private const int bin_count = 32;

        private readonly List<double> difficulties = new List<double>();

        /// <summary>
        /// Returns the strain value at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double StrainValueAt(DifficultyHitObject current);

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(StrainValueAt(current));
        }

        protected abstract double HitProbability(double skill, double difficulty);

        private double fcProbabilityAtSkillBinned(double skill, IEnumerable<Bin> bins)
        {
            if (skill <= 0) return 0;

            double totalHitProbability(Bin bin) => Math.Pow(HitProbability(skill, bin.Difficulty), bin.Count);

            return bins.Aggregate(1.0, (current, bin) => current * totalHitProbability(bin));
        }

        private double fcProbabilityAtSkillExact(double skill)
        {
            if (skill <= 0) return 0;

            return difficulties.Aggregate<double, double>(1, (current, d) => current * HitProbability(skill, d));
        }

        private double difficultyValueBinned()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            var bins = Bin.CreateBins(difficulties, bin_count);

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = Chandrupatla.FindRootExpand(
                skill => fcProbabilityAtSkillBinned(skill, bins) - fc_probability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;
        }

        private double difficultyValueExact()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = Chandrupatla.FindRootExpand(
                skill => fcProbabilityAtSkillExact(skill) - fc_probability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;
        }

        public override double DifficultyValue()
        {
            if (difficulties.Count == 0)
                return 0;

            return difficulties.Count < 2 * bin_count ? difficultyValueExact() : difficultyValueBinned();
        }
    }
}
