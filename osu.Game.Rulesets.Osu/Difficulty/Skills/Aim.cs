// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Aim : TimeSkill
    {
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double skillMultiplierSnap => 760.0;
        private double skillMultiplierAgility => 520;
        private double skillMultiplierFlow => 1200.0;

        private readonly List<double> sliderStrains = new List<double>();

        private readonly List<int> sliderIndices = new List<int>();

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        protected override double HitProbabilityFlow(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return Math.Pow(DifficultyCalculationUtils.Erf(skill / difficulty), 0.2);
        }

        protected override double StrainDecay(DifficultyHitObject current)
            => Math.Pow(0.15, ((OsuDifficultyHitObject)current).AdjustedDeltaTime / 1000);

        protected override double PFlow(double snapDifficulty, double flowDifficulty)
        {
            const double k = 7.27;
            double ratio = flowDifficulty / snapDifficulty;

            if (ratio == 0)
                return 0;

            if (double.IsNaN(ratio))
                return 1;

            return DifficultyCalculationUtils.Logistic(k * Math.Log(ratio));
        }

        protected override void StrainValueAt(DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;

            double decay = StrainDecay(current);

            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * (1 - decay) * skillMultiplierSnap;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current) * (1 - decay) * skillMultiplierAgility;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * (1 - decay) * skillMultiplierFlow;

            if (Mods.Any(m => m is OsuModTouchDevice))
            {
                snapDifficulty = Math.Pow(snapDifficulty, 0.89);
                flowDifficulty = Math.Pow(flowDifficulty, 1.1);
            }

            if (Mods.Any(m => m is OsuModRelax))
            {
                agilityDifficulty = 0;
                flowDifficulty *= 0.1;
            }

            SnapDifficulties.Add(snapDifficulty + agilityDifficulty);
            FlowDifficulties.Add(flowDifficulty);

            if (current.BaseObject is Slider)
                sliderIndices.Add(SnapDifficulties.Count - 1);
        }

        public double GetDifficultSliders()
        {
            if (sliderIndices.Count == 0 || CachedStrainSequence is null)
                return 0;

            double maxSliderStrain = sliderIndices.Max(i => CachedStrainSequence[i]);

            if (maxSliderStrain == 0)
                return 0;

            return sliderIndices.Sum(i =>
            {
                double strain = CachedStrainSequence[i];
                return 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0)));
            });
        }

        public double CountTopWeightedSliders(double difficultyValue)
        {
            if (sliderIndices.Count == 0 || CachedStrainSequence is null)
                return 0;

            double consistentTopStrain = difficultyValue / 10;

            if (consistentTopStrain == 0)
                return 0;

            return sliderIndices.Sum(i => DifficultyCalculationUtils.Logistic(CachedStrainSequence[i] / consistentTopStrain, 0.88, 10, 1.1));
        }
    }
}
