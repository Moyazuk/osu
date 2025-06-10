// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuStrainSkill
    {
        public readonly bool IncludeSliders;
        public readonly bool WithCheesability;

        public Aim(Mod[] mods, bool includeSliders, bool withCheesability)
            : base(mods)
        {
            IncludeSliders = includeSliders;
            WithCheesability = withCheesability;
        }


        private double currentStrain;

        private double skillMultiplier => 25.45;
        private double strainDecayBase => 0.15;

        private readonly List<double> cheeseResults = new List<double>();
        private readonly List<double> sliderStrains = new List<double>();
        private readonly List<double> strains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders, WithCheesability) * skillMultiplier;

            strains.Add(currentStrain);

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            cheeseResults.Add(isInaccurateWhileCheesed(current));

            return currentStrain;
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());

        public double GetInaccuraciesWithCheesing()
        {
            double sum = 0;
            for (int i = 0; i < cheeseResults.Count; i++)
            {
                double w = strains[i] / strains.Max();
                sum += cheeseResults[i] * (1 - w); // Limit the amount of inaccuracies on easier parts
            }

            return sum;
        }

        // Check if cheesing the current object still results in a great.
        private static int isInaccurateWhileCheesed(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;

            // Assume even on Lazer that cheesing does not happen on sliders
            if (osuCurrObj.BaseObject is Slider)
                return 0;

            return osuCurrObj.ExtraDeltaTime > osuCurrObj.HitWindowGreat ? 1 : 0;
        }
    }
}
