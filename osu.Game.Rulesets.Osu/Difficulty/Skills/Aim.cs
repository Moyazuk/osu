﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : ContinuousStrainSkill
    {
        public Aim(Mod[] mods, bool withSliders)
            : base(mods)
        {
            this.withSliders = withSliders;
        }

        private readonly bool withSliders;

        private double currentStrain;

        private double skillMultiplier => 80.25;
        protected override double StrainDecayBase => 0.25;

        private readonly List<double> objectStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);

        // protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double hardestPreviousDifficulty = hardestNoteInPrevSecond(current, objectStrains);

            currentStrain = (hardestPreviousDifficulty * 3 + AimEvaluator.EvaluateDifficultyOf(current, false)) / 4;

            objectStrains.Add(currentStrain);

            return currentStrain * skillMultiplier;
        }

        private static double hardestNoteInPrevSecond(DifficultyHitObject current, List<double> previousDifficulties)
        {
            List<double> reversedDifficulties = new List<double>(previousDifficulties);

            reversedDifficulties.Reverse();

            double hardestPreviousDifficulty = 0;
            double cumulativeDeltatime = current.DeltaTime;

            for (int i = 0; i < previousDifficulties.Count; i++)
            {
                cumulativeDeltatime += current.Previous(i).DeltaTime;

                if (cumulativeDeltatime > 1000)
                    break;

                hardestPreviousDifficulty = Math.Max(hardestPreviousDifficulty, reversedDifficulties[i] * sigmoidDeltaTimeNerf(cumulativeDeltatime));
            }

            return hardestPreviousDifficulty;
        }

        private static double sigmoidDeltaTimeNerf(double deltaTime) => Math.Max(0, (1 / (1 + Math.Exp((deltaTime - 600) / 70)) - 0.05) / 0.95);
    }
}