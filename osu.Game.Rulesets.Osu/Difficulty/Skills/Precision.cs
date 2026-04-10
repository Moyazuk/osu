// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Precision : HarmonicSkill
    {
        private double skillMultiplier => 4.2;

        private readonly List<double> sliderStrains = new List<double>();

        private double currentDifficulty;

        private double strainDecayBase => 0.10;

        protected override double HarmonicScale => 20;
        protected override double DecayExponent => 0.7;

        public readonly bool IncludeSliders;

        public Precision(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double ObjectDifficultyOf(DifficultyHitObject current)
        {
            double decay = strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * Aim.SkillMultiplierSnap;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current) * Aim.SkillMultiplierAgility;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * Aim.SkillMultiplierFlow;

            double combinedSnapDifficulty = Norm(Aim.MeanExponent, snapDifficulty, agilityDifficulty);

            double pSnap = Aim.CalculateSnapFlowProbability(flowDifficulty / combinedSnapDifficulty);

            double flowNerf = Interpolation.Lerp(0.0, 1.0, Smootherstep(pSnap, 0, 0.5));

            double precisionDifficulty = PrecisionEvaluator.EvaluateDifficultyOf(current) * (1 - decay) * skillMultiplier * flowNerf;

            currentDifficulty *= decay;
            currentDifficulty += precisionDifficulty;

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentDifficulty);

            if (Mods.Any(m => m is OsuModTouchDevice))
            {
                currentDifficulty *= 0.5;
            }

            currentDifficulty *= 1;


            return currentDifficulty;
        }

        public double RelevantNoteCount()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            double maxStrain = ObjectDifficulties.Max();

            if (maxStrain == 0)
                return 0;

            return ObjectDifficulties.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders(double difficultyValue)
        {
            if (sliderStrains.Count == 0)
                return 0;

            if (NoteWeightSum == 0)
                return 0.0;

            double consistentTopNote = difficultyValue / NoteWeightSum; // What would the top note be if all note values were identical

            if (consistentTopNote == 0)
                return 0;

            // Use a weighted sum of all notes. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DifficultyCalculationUtils.Logistic(s / consistentTopNote, 0.88, 10, 1.1));
        }
    }
}
