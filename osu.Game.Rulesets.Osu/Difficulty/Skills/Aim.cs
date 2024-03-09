﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuProbSkill
    {
        public Aim(Mod[] mods, bool withSliders)
            : base(mods)
        {
            this.withSliders = withSliders;
        }

        private readonly bool withSliders;

        private double currentStrain;

        private double skillMultiplier => 125;
        private double strainDecayBase => 0.15;

        protected override double HitProbability(double skill, double difficulty)
        {
            if (skill <= 0) return 0;
            if (difficulty <= 0) return 1;

            return SpecialFunctions.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += AimEvaluator.EvaluateDifficultyOf(current, withSliders) * skillMultiplier;

            return currentStrain;
        }
    }
}
