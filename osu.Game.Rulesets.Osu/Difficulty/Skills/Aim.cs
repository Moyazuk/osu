// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuStrainSkill
    {
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double currentAimStrain;
        private double currentAgilityStrain;
        private double lastAimStrain;

        private bool? previousWasFlow = null;

        private double skillMultiplierAim => 132.85;


        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecayAim(double ms) => Math.Pow(0.15, ms / 1000);
        private double strainDecaySpeed(double ms) => Math.Pow(0.3, ms / 1000);

        protected override double CalculateInitialStrain(double deltaTime) => lastAimStrain * strainDecayAim(deltaTime);

        protected override IEnumerable<ObjectStrain> StrainValuesAt(DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;
            lastAimStrain = currentAimStrain;

            var firstMovement = osuCurrent.Movements[0];
            double previousTime = firstMovement.StartTime;

            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOfMovement(current, firstMovement);
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOfMovement(current, firstMovement);
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOfMovement(current, firstMovement);

            if (Mods.Any(m => m is OsuModTouchDevice))
            {
                snapDifficulty = Math.Pow(snapDifficulty, 0.8);
                flowDifficulty = Math.Pow(flowDifficulty, 0.8);
            }

            double aimDecay = strainDecayAim(firstMovement.Time);
            double agilityDecay = strainDecayAim(firstMovement.Time);

            double scaledSnapDifficulty = snapDifficulty * (1 - aimDecay) * skillMultiplierAim;
            double scaledFlowDifficulty = flowDifficulty * (1 - aimDecay) * skillMultiplierAim;
            double scaledAgilityDifficulty = agilityDifficulty * skillMultiplierAim;

            bool isFlow = scaledFlowDifficulty < (scaledSnapDifficulty + currentAgilityStrain + scaledAgilityDifficulty);

            double transitionBonus = 1.0;

            if (previousWasFlow.HasValue)
                transitionBonus = isFlow ? (previousWasFlow.Value ? 1.0 : 1.0) : (!previousWasFlow.Value ? 1.0 : 1.0);

            currentAimStrain *= aimDecay;
            currentAgilityStrain *= agilityDecay;

            double auxiliaryStrainValue = 0;

            if (isFlow)
            {
                currentAimStrain += scaledFlowDifficulty;
            }
            else
            {
                currentAimStrain += scaledSnapDifficulty;
                currentAgilityStrain += scaledAgilityDifficulty;
                auxiliaryStrainValue = currentAgilityStrain;
            }

            previousWasFlow = isFlow;

            double totalStrain = (currentAimStrain + auxiliaryStrainValue) * transitionBonus;

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalStrain);

            yield return new ObjectStrain
            {
                Time = firstMovement.EndTime,
                PreviousTime = previousTime,
                Value = totalStrain,
            };

            previousTime = firstMovement.EndTime;

            for (int i = 1; i < osuCurrent.Movements.Count; i++)
            {
                var movement = osuCurrent.Movements[i];
                lastAimStrain = currentAimStrain;

                double decay = strainDecayAim(movement.Time);
                double agilityDecayInner = strainDecayAim(movement.Time);

                currentAimStrain *= decay;
                currentAgilityStrain *= agilityDecayInner;

                double innerAuxiliaryStrain = 0;

                if (IncludeSliders)
                {
                    double innerSnapDiff = SnapAimEvaluator.EvaluateDifficultyOfMovement(current, movement) * (1 - decay) * skillMultiplierAim;
                    double innerFlowDiff = FlowAimEvaluator.EvaluateDifficultyOfMovement(current, movement) * (1 - decay) * skillMultiplierAim;
                    double innerAgilityDiff = AgilityEvaluator.EvaluateDifficultyOfMovement(current, movement) * skillMultiplierAim;

                    bool innerIsFlow = innerFlowDiff < (innerSnapDiff + currentAgilityStrain + innerAgilityDiff);

                    if (innerIsFlow)
                    {
                        currentAimStrain += innerFlowDiff;
                    }
                    else
                    {
                        currentAimStrain += innerSnapDiff;
                        currentAgilityStrain += innerAgilityDiff;
                        innerAuxiliaryStrain = currentAgilityStrain;
                    }
                }

                totalStrain = (currentAimStrain + innerAuxiliaryStrain);

                if (current.BaseObject is Slider)
                    sliderStrains.Add(totalStrain);

                yield return new ObjectStrain
                {
                    Time = movement.EndTime,
                    PreviousTime = previousTime,
                    Value = totalStrain,
                };

                previousTime = movement.EndTime;
            }
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

        public double CountTopWeightedSliders(double difficultyValue)
            => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, difficultyValue);
    }
}
