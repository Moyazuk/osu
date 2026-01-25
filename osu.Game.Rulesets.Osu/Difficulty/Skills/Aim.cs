﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuFcTimeSkill
    {
        public readonly bool IncludeSliders;
        public readonly bool WithCheesability;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            previousStrains = new List<(double, double)>();
            IncludeSliders = includeSliders;
        }

        private double currentStrain;

        private double currentAgilityStrain;

        private double currentflowStrain;

        private bool? previousWasFlow = null;

        private double skillMultiplier => 685;
        private double strainDecayBase => 0.15;

        private double agilityStrainDecayBase => 0.85;

        private const double backwards_strain_influence = 1000;

        private readonly List<(double, double)> previousStrains;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        private double agilityStrainDecay(double ms) => Math.Pow(agilityStrainDecayBase, ms / 1000);

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double decay = strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            double auxiliaryStrainValue = 0;
            double transitionBonus = 0;
            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * (1 - decay) * skillMultiplier;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * (1 - decay) * skillMultiplier;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current, WithCheesability) * skillMultiplier;

            double snapTransitionBonus = previousWasFlow.HasValue && previousWasFlow.Value ? 1.2 : 1.0;
            double flowTransitionBonus = previousWasFlow.HasValue && !previousWasFlow.Value ? 1.05 : 1.0;

            bool isFlow = (flowDifficulty) < (snapDifficulty + currentAgilityStrain + agilityDifficulty);

            currentStrain *= decay;
            currentAgilityStrain *= agilityStrainDecay(current.DeltaTime);

            if (isFlow)

                //for flow aim, we want the strain contribution to be solely from the FlowStrainEvaluator, and we only want to update the value of
                // currentFlowStrain when the current note is flow-aimed
            {
                currentStrain += flowDifficulty;
                auxiliaryStrainValue = 0;
                transitionBonus = flowTransitionBonus;

            }
                //for snap aim, the notes difficulty itself contributes to strain and we update the value of agilityStrain only when the note is snapped
            else
            {
                currentStrain += snapDifficulty;
                currentAgilityStrain += agilityDifficulty;
                auxiliaryStrainValue = currentAgilityStrain;
                transitionBonus = snapTransitionBonus;
            }

            previousWasFlow = isFlow;

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            return (currentStrain + auxiliaryStrainValue) * transitionBonus;
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
    }
}
