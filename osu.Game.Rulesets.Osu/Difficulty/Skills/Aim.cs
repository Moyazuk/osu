// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuTimeSkill
    {
        public readonly bool IncludeSliders;
        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private readonly List<double> sliderStrains = new List<double>();

        private double currentStrain;
        private double agilityStrain;

        private double strainDecayBase => 0.15;
        private double strainDecayAgiBase => 0.15;

        private double strainInfluence => 1 / 2.0;

        private double flowStrainInfluence => 1 / 3.0;

        private double agiStrainInfluence => 16 / 1.0;

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);
        private double agilityStrainDecay(double ms) => Math.Pow(strainDecayAgiBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        private bool wasFlow = false; // Tracks last aim type

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            agilityStrain *= agilityStrainDecay(current.DeltaTime);
            currentStrain *= strainDecay(current.DeltaTime);

            double agilityDifficulty = SnapAimEvaluator.EvaluateAgilityBonus(current);
            double snapBaseDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);
            double snapDifficulty = snapBaseDifficulty + (agilityDifficulty + agilityStrain * agiStrainInfluence);
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);
            double adjStrainInfluence = 0;

            bool isFlow = flowDifficulty < snapDifficulty;
            double currentDifficulty = Math.Min(snapDifficulty, flowDifficulty);

            // If switching from flow to snap, or snap to flow, apply a small bonus.
            if (isFlow != wasFlow)
            {
                //double switchBonus = 1.25;
                //currentDifficulty *= switchBonus;
            }

            if (!isFlow)
            {
                currentStrain += snapBaseDifficulty / 8.0;
                agilityStrain += agilityDifficulty * 16;
                adjStrainInfluence = strainInfluence;
            }
            else
            {
                double currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);
                currentStrain += currentDifficulty / 3.0;
                adjStrainInfluence = flowStrainInfluence;
            }

            wasFlow = isFlow;

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            return currentDifficulty + currentStrain * adjStrainInfluence;
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
    }
}
