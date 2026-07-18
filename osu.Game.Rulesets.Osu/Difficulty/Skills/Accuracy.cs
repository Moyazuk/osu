// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Accuracy : Skill
    {
        private readonly List<double> effectiveHitWindows = new List<double>();

        public Accuracy(Mod[] mods)
            : base(mods)
        {
        }

        private double ssProbability(double deviation)
        {
            if (deviation == 0)
                return 1;

            double p = 1.0;
            foreach (double effectiveHitWindow in effectiveHitWindows)
                p *= DiffUtils.Erf(effectiveHitWindow / (Math.Sqrt(2) * deviation));

            return p;
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            double effectiveHitWindow = AccuracyEvaluator.EvaluateEffectiveHitWindow(current);

            if (!double.IsFinite(effectiveHitWindow) || effectiveHitWindow <= 0)
                return 0;

            effectiveHitWindows.Add(effectiveHitWindow);


            return 1000.0 / effectiveHitWindow;
        }

        public override double DifficultyValue()
        {
            double threshold = 0.01;

            double sigma = RootFinding.FindRootExpand(d => ssProbability(d) - threshold, 0, 20, accuracy: 1e-4);

            return sigma;
        }
    }
}
