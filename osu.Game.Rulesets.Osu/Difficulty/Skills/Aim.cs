// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
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
using osu.Game.Rulesets.Osu.Difficulty;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuTimeSkill
    {
        public readonly bool IncludeSliders;
        public readonly bool WithCheesability;

        private readonly OsuDifficultyTuning tuning;

        public Aim(Mod[] mods, OsuDifficultyTuning tuning, bool includeSliders, bool withCheesability)
            : base(mods)
        {
            this.tuning = tuning;
            previousStrains = new List<(double, double)>();
            IncludeSliders = includeSliders;
            WithCheesability = withCheesability;
        }

        private double inaccuraciesWhileCheesing = 0;
        private double maxStrain = 0;

        private double currentStrain;

        private double currentAgilityStrain;

        private double currentflowStrain;

        private double strainDecayBase => tuning.AimStrainDecayBase;

        private double agilityStrainDecayBase => tuning.AimAgilityStrainDecayBase;

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
            currentAgilityStrain *= agilityStrainDecay(current.DeltaTime);

            var osuCurrent = (OsuDifficultyHitObject)current;
            double currentDifficulty = 0;
            double auxiliaryStrainValue = 0;
            double currentStrainDifficulty = 0;
            double transitionBonus = 0;
            double strainMultiplier = 0;
            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders, WithCheesability, tuning) * tuning.AimSkillMultiplier * tuning.AimSnapDifficultyScale;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders, tuning) * tuning.AimSkillMultiplier * tuning.AimFlowDifficultyScale;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current, WithCheesability, tuning) * tuning.AimSkillMultiplier * tuning.AimAgilityDifficultyScale;

            double snapTransitionBonus = tuning.AimSnapTransitionScale;
            double flowTransitionBonus = tuning.AimFlowTransitionScale;

            bool isFlow = flowDifficulty * flowTransitionBonus < (snapDifficulty + currentAgilityStrain + agilityDifficulty) * snapTransitionBonus;

            if (isFlow)

                //for flow aim, we want the strain contribution to be solely from the FlowStrainEvaluator, and we only want to update the value of
                // currentFlowStrain when the current note is flow-aimed
            {
                currentDifficulty = flowDifficulty;
                currentStrainDifficulty = currentDifficulty;
                auxiliaryStrainValue = 0;
                transitionBonus = flowTransitionBonus;
                strainMultiplier = tuning.AimFlowStrainMultiplier;

            }
                //for snap aim, the notes difficulty itself contributes to strain and we update the value of agilityStrain only when the note is snapped
            else
            {
                currentDifficulty = snapDifficulty;
                currentAgilityStrain += agilityDifficulty;
                auxiliaryStrainValue = currentAgilityStrain;
                currentStrainDifficulty = snapDifficulty;
                transitionBonus = snapTransitionBonus;
                strainMultiplier = tuning.AimSnapStrainMultiplier;
            }

            currentStrain = getCurrentStrainValue(osuCurrent.StartTime, previousStrains) * strainMultiplier;
            previousStrains.Add((osuCurrent.StartTime, currentStrainDifficulty));

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            inaccuraciesWhileCheesing += isInaccurateWhileCheesed(current) * currentStrain;
            if (currentStrain > maxStrain)
                maxStrain = currentStrain;

            return (currentDifficulty + currentStrain + auxiliaryStrainValue) * transitionBonus;
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
                double startTimeOffset = Math.Max(0, endTime - prevTime - tuning.AimBackwardsStrainInfluence);

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

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
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
