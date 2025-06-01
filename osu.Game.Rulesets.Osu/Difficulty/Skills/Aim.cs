// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

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

        private double strainIncreaseRate => 10;
        private double strainDecreaseRate => 3;
        private double strainInfluence => 8.0 / 1;

        private double skillMultiplier => 25.6;
        private double strainDecayBase => 0.15;

        private readonly List<double> sliderStrains = new List<double>();
        private readonly List<double> previousStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double currentDifficulty = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders, WithCheesability) * 3.35;


            double priorDifficulty = highestPreviousStrain(current, current.DeltaTime);

            currentStrain = getStrainValueOf(currentDifficulty, priorDifficulty);
            previousStrains.Add(currentStrain);

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            return currentDifficulty + currentStrain * strainInfluence;
        }

        private double getStrainValueOf(double currentDifficulty, double priorDifficulty) => currentDifficulty > priorDifficulty
            ? (priorDifficulty * strainIncreaseRate + currentDifficulty) / (strainIncreaseRate + 1)
            : (priorDifficulty * strainDecreaseRate + currentDifficulty) / (strainDecreaseRate + 1);

        private double highestPreviousStrain(DifficultyHitObject current, double time)
        {
            double hardestPreviousDifficulty = 0;
            double cumulativeDeltaTime = time;

            double timeDecay(double ms) => Math.Pow(strainDecayBase, Math.Pow(ms / 900, 7));

            for (int i = 0; i < previousStrains.Count; i++)
            {
                if (cumulativeDeltaTime > 1200)
                {
                    previousStrains.RemoveRange(0, i);
                    break;
                }

                hardestPreviousDifficulty = Math.Max(hardestPreviousDifficulty, previousStrains[^(i + 1)] * timeDecay(cumulativeDeltaTime));

                cumulativeDeltaTime += current.Previous(i).DeltaTime;
            }

            return hardestPreviousDifficulty;
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
