﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
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
    public class Tap : OsuTimeSkill
    {
        public Tap(Mod[] mods)
            : base(mods)
        {
        }

        private double currentStrain;

        private double overallMultiplier => 1.05;

        private double skillMultiplier => 20.85;
        private double strainDecayBase => 0.3;

        private double currentRhythm;

        private double currentHitWindow;

        private readonly List<double> sliderStrains = new List<double>();

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            currentStrain += SpeedEvaluator.EvaluateDifficultyOf(current) * skillMultiplier;

            currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            currentHitWindow = AccuracyEvaluator.EvaluateEffectiveHitWindow(current);

            double accDifficulty = 8000 / currentHitWindow;

            double totalStrain = (currentStrain * currentRhythm) / Math.Pow(currentHitWindow, 0.25);

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            return (totalStrain + accDifficulty) * overallMultiplier;
        }

        public double RelevantNoteCount()
        {
            if (Difficulties.Count == 0)
                return 0;

            double maxStrain = Difficulties.Max();
            if (maxStrain == 0)
                return 0;

            return Difficulties.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
