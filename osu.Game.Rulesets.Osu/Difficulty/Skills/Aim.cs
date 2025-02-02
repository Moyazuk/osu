// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
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
            this.withSliders = withSliders;
        }

        private readonly bool withSliders;

        private readonly List<double> sliderStrains = new List<double>();

        private readonly List<double> previousStrains = new List<double>();

        private double currentStrain;
        private double currentAngleStrain;

        private double strainDecayBase => 0.15;
        private double strainDecayBaseAngle => 0.45;
        private double strainIncreaseRate => 10;
        private double strainDecreaseRate => 3;
        private double strainInfluence => 1 / 3.0;

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);
        private double strainDecayAngle(double ms) => Math.Pow(strainDecayBaseAngle, ms / 1000);
        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double currentDifficulty = AimEvaluator.EvaluateDifficultyOf(current, withSliders) * 19.1;
            currentAngleStrain *= strainDecayAngle(current.DeltaTime);
            currentAngleStrain += AngleEvaluator.EvaluateDifficultyOf(current);

            double priorDifficulty = highestPreviousStrain(current, current.DeltaTime);

            currentStrain = getStrainValueOf(currentDifficulty, priorDifficulty);
            previousStrains.Add(currentStrain);

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            return currentDifficulty + currentAngleStrain + currentStrain * strainInfluence;
        }

        private double getStrainValueOf(double currentDifficulty, double priorDifficulty) => currentDifficulty > priorDifficulty
            ? (priorDifficulty * strainIncreaseRate + currentDifficulty) / (strainIncreaseRate + 1)
            : (priorDifficulty * strainDecreaseRate + currentDifficulty) / (strainDecreaseRate + 1);

        private double highestPreviousStrain(DifficultyHitObject current, double time)
        {
            double hardestPreviousDifficulty = 0;
            double cumulativeDeltaTime = time;

            double timeDecay(double ms) => Math.Pow(strainDecayBase, Math.Pow(ms / 400, 7));

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

            double[] sortedStrains = sliderStrains.OrderDescending().ToArray();

            double maxSliderStrain = sortedStrains.Max();
            if (maxSliderStrain == 0)
                return 0;

            return sortedStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }
    }
}
