﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Accuracy : ProbabilitySkill
    {
        public Accuracy(Mod[] mods)
            : base(mods)
        {
        }

        protected override double TimeThresholdMinutes => 48;

        private double overallMultiplier => 1.05;

        private double skillMultiplier => 20.85;
        private double strainDecayBase => 0.3;

        private double currentHitWindow;

        private readonly List<double> sliderStrains = new List<double>();

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        protected override double ObjectDifficultyOf(DifficultyHitObject current)
        {
            currentHitWindow = AccuracyEvaluator.EvaluateEffectiveHitWindow(current);

            double currentRhythm = Math.Sqrt(RhythmEvaluator.EvaluateDifficultyOf(current));

            double rhythmWeight = 1.0 + Math.Pow(currentRhythm - 1.0, 3);

            double accDifficulty = rhythmWeight / currentHitWindow;

            if (current.BaseObject is Slider)
            {
                accDifficulty = 0;
            }

            return accDifficulty * 21000;
        }


        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
