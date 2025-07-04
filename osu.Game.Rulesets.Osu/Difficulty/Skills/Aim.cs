// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public abstract class Aim : OsuTimeSkill
    {
        public readonly bool IncludeSliders;
        public readonly bool WithCheesability;

        protected Aim(Mod[] mods, bool includeSliders, bool withCheesability)
            : base(mods)
        {
            IncludeSliders = includeSliders;
            previousStrains = new List<(OsuDifficultyHitObject, double)>();
            WithCheesability = withCheesability;
        }

        private double inaccuraciesWhileCheesing = 0;
        private double maxStrain = 0;
        private double currentStrain;

        private double skillMultiplier => 37.1;
        private double strainDecayBase => 0.15;

        private readonly List<double> sliderStrains = new List<double>();

        private readonly List<(OsuDifficultyHitObject, double)> previousStrains;

        // How far back notes should influence strain.
        private const double backwards_strain_influence = 1000;

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected abstract double StrainValueOf(DifficultyHitObject current);

        protected override double StrainValueAt(DifficultyHitObject current)
        {

            double currentDifficulty = StrainValueOf(current) * skillMultiplier;
            previousStrains.Add(((OsuDifficultyHitObject)current, currentDifficulty));
            currentStrain = getCurrentStrainValue((OsuDifficultyHitObject)current, previousStrains) * 2.5;

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            inaccuraciesWhileCheesing += isInaccurateWhileCheesed(current) * currentStrain;
            if (currentStrain > maxStrain)
                maxStrain = currentStrain;

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


        public double GetInaccuraciesWithCheesing() => maxStrain > 0 ? inaccuraciesWhileCheesing / maxStrain : 0;

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
