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
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
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
            previousStrains = new List<(double, double)>();
        }

        private double currentStrain;

        private double currentAgilityStrain;

        private double currentflowStrain;

        private bool? previousWasFlow = null;

        private double skillMultiplier => 128;
        private double strainDecayBase => 0.15;

        private double agilityStrainDecayBase => 0.15;

        private const double backwards_strain_influence = 1000;

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        private readonly List<(double, double)> previousStrains;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        private double agilityStrainDecay(double ms) => Math.Pow(agilityStrainDecayBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            //we decay both supplimental strain values irrespective of whether a given note is snapped or flowed
            currentAgilityStrain *= agilityStrainDecay(current.DeltaTime);

            var osuCurrent = (OsuDifficultyHitObject)current;
            double auxiliaryStrainValue = 0;
            double currentStrainDifficulty = 0;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * skillMultiplier;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current) * skillMultiplier;
            double snapBaseDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * skillMultiplier;
            double snapDifficulty = snapBaseDifficulty + (agilityDifficulty + currentAgilityStrain);

            double snapTransitionBonus = previousWasFlow.HasValue && previousWasFlow.Value ? 1.5 : 1.0;
            double flowTransitionBonus = previousWasFlow.HasValue && !previousWasFlow.Value ? 1.5 : 1.0;

            bool isFlow = flowDifficulty * flowTransitionBonus + currentStrain < snapDifficulty * snapTransitionBonus + currentStrain;

            double currentDifficulty = isFlow ? flowDifficulty * flowTransitionBonus : snapDifficulty * snapTransitionBonus;

            currentStrain = getCurrentStrainValue(osuCurrent.StartTime, previousStrains) * 4.10;


            if (!isFlow)
            {
                currentDifficulty = snapDifficulty;
                currentAgilityStrain += agilityDifficulty;
            }
            else
            {
                currentDifficulty = flowDifficulty;
                auxiliaryStrainValue = 0;
            }

            previousStrains.Add((osuCurrent.StartTime, currentDifficulty));
            previousWasFlow = isFlow;

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            return currentStrain + currentDifficulty;
        }

        private double getCurrentStrainValue(double endTime, List<(double Time, double Diff)> previousDifficulties)
        {
            if (previousDifficulties.Count < 2)
                return 0;

            double sum = 0;

            double highestNoteVal = 0;
            double prevDeltaTime = 0;

            int index = 1;

            while (index < previousDifficulties.Count)
            {
                double prevTime = previousDifficulties[index - 1].Time;
                double currTime = previousDifficulties[index].Time;

                double deltaTime = currTime - prevTime;
                double prevDifficulty = previousDifficulties[index - 1].Diff;

                // How much of the current deltaTime does not fall under the backwards strain influence value.
                double startTimeOffset = Math.Max(0, endTime - prevTime - backwards_strain_influence);

                // If the deltaTime doesn't fall into the backwards strain influence value at all, we can remove its corresponding difficulty.
                // We don't iterate index because the list moves backwards.
                if (startTimeOffset > deltaTime)
                {
                    previousDifficulties.RemoveAt(0);

                    continue;
                }

                highestNoteVal = Math.Max(prevDifficulty, strainDecay(prevDeltaTime));
                prevDeltaTime = deltaTime;

                sum += highestNoteVal * (strainDecayAntiderivative(startTimeOffset) - strainDecayAntiderivative(deltaTime));

                index++;
            }

            // CalculateInitialStrain stuff
            highestNoteVal = Math.Max(previousDifficulties.Last().Diff, highestNoteVal);
            double lastTime = previousDifficulties.Last().Time;
            sum += (strainDecayAntiderivative(0) - strainDecayAntiderivative(endTime - lastTime)) * highestNoteVal;

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
