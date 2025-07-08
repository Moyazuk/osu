﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuStrainSkill
    {
        public Aim(Mod[] mods, bool withSliders)
            : base(mods)
        {
            this.IncludeSliders = withSliders;
            previousStrains = new List<(OsuDifficultyHitObject, double)>();
        }

        public readonly bool IncludeSliders;

        private readonly List<double> sliderStrains = new List<double>();

        private readonly List<(OsuDifficultyHitObject, double)> previousStrains;

        private double currentStrain;
        private double currentAngleStrain;

        private double strainDecayBase => 0.15;

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        private const double backwards_strain_influence = 1000;

        protected override double StrainValueAt(DifficultyHitObject current)
        {

            double currentDifficulty = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * 54;
            previousStrains.Add(((OsuDifficultyHitObject)current, currentDifficulty));
            currentStrain = getCurrentStrainValue((OsuDifficultyHitObject)current, previousStrains) * 2.5;

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            return currentStrain;
        }

        private double getCurrentStrainValue(OsuDifficultyHitObject current, List<(OsuDifficultyHitObject Note, double Diff)> previousDifficulties, double offset = 0)
        {
            if (previousDifficulties.Count < 2)
                return 0;

            double sum = 0;

            double highestNoteVal = 0;
            double prevDeltaTime = 0;

            int index = 1;

            while (index < previousDifficulties.Count)
            {
                OsuDifficultyHitObject note = previousDifficulties[index].Note;
                double prevDifficulty = previousDifficulties[index - 1].Diff;

                // How much of the current deltaTime does not fall under the backwards strain influence value.
                double startTimeOffset = Math.Max(0, note.DeltaTime + (current.StartTime - note.StartTime) - backwards_strain_influence);

                // If the deltaTime doesn't fall into the backwards strain influence value at all, we can remove its corresponding difficulty.
                // We don't iterate index because the list moves backwards.
                if (startTimeOffset > note.DeltaTime)
                {
                    previousDifficulties.RemoveAt(0);

                    continue;
                }

                highestNoteVal = Math.Max(prevDifficulty, strainDecay(prevDeltaTime));
                prevDeltaTime = note.DeltaTime;

                sum += highestNoteVal * (strainDecayAntiderivative(startTimeOffset) - strainDecayAntiderivative(note.DeltaTime));

                index++;
            }

            // CalculateInitialStrain stuff
            highestNoteVal = Math.Max(previousDifficulties.Last().Diff, highestNoteVal);
            sum += (strainDecayAntiderivative(0) - strainDecayAntiderivative(offset)) * highestNoteVal;

            return sum;

            double strainDecayAntiderivative(double t) => Math.Pow(strainDecayBase, t / 1000) / Math.Log(1.0 / strainDecayBase);
        }
        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double[] sortedStrains = sliderStrains.OrderDescending().ToArray();

            double maxSliderStrain = sortedStrains.Max();
            if (maxSliderStrain == 0)
                return 0;

            return sortedStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
