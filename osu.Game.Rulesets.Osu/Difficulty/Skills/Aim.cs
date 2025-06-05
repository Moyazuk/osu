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

        private bool isUsingClassicSliderAcc;

        public Aim(Mod[] mods, bool includeSliders, bool withCheesability)
            : base(mods)
        {
            IncludeSliders = includeSliders;
            WithCheesability = withCheesability;
            isUsingClassicSliderAcc = mods.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value);
        }

        private int countGreatWithCheesing = 0;

        private double currentStrain;

        private double skillMultiplier => 25.6;
        private double strainDecayBase => 0.15;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders, WithCheesability) * skillMultiplier;

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            countGreatWithCheesing += isGreatWhileCheesed(current, isUsingClassicSliderAcc);

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

        public double GetGreatsWithCheesing() => countGreatWithCheesing;

        // Check if cheesing the current object still results in a great.
        private static int isGreatWhileCheesed(DifficultyHitObject current, bool isUsingClassicSliderAcc)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;

            // Since the extra delta time is never above the 50 hit window (the hit window for sliders when slider acc is disabled),
            // we can always get a 300 on sliders when cheesing.
            if (osuCurrObj.BaseObject is Slider && isUsingClassicSliderAcc)
                return 1;

            return osuCurrObj.ExtraDeltaTime <= osuCurrObj.HitWindowGreat ? 1 : 0;
        }
    }
}
