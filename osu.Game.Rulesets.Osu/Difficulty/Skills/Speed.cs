// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed;
using osu.Game.Rulesets.Osu.Mods;


namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : HarmonicSkill
    {
        private double totalMultiplier => 1;
        private double burstMultiplier => 2.43;
        private double streamMultiplier => 0.4;
        private double staminaMultiplier => 0.0175;
        private double meanExponent => 1.25;

        private double currentBurstStrain;
        private double currentRhythmStrain;
        private double currentStreamStrain;
        private double currentStaminaStrain;

        private readonly List<double> sliderStrains = new List<double>();

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        protected override double HarmonicScale => 15;
        protected override double DecayExponent => 0.9;

        private double strainDecayBurst(double ms) => Math.Pow(0.1, ms / 1000);
        private double strainDecayStream(double ms) => Math.Pow(0.01, Math.Pow(ms / 1000, 1.6));

        private double strainDecayStamina(double ms, double staminaValue)
        {
            double changeFactor = currentStaminaStrain > 0 ? 1 + Math.Pow(currentStaminaStrain / (staminaValue + currentStaminaStrain), 25.0) : 1.0;
            return Math.Pow(0.05, Math.Pow(ms * changeFactor / 1000, 3.5));
        }

        protected override double ObjectDifficultyOf(DifficultyHitObject current)
        {
            currentBurstStrain *= strainDecayBurst(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            currentRhythmStrain *= strainDecayBurst(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            currentRhythmStrain += RhythmEvaluator.CalculateStartOfFastPatternBonus(current) * 3;
            currentBurstStrain += SpeedEvaluator.EvaluateDifficultyOf(current) * 1.25;

            double totalBurstStrain = currentBurstStrain + currentRhythmStrain;

            double staminaValue = StaminaEvaluator.EvaluateDifficultyOf(current);

            currentStreamStrain *= strainDecayStream(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            currentStreamStrain += staminaValue * streamMultiplier;

            currentStaminaStrain *= strainDecayStamina(((OsuDifficultyHitObject)current).AdjustedDeltaTime, staminaValue * staminaMultiplier);
            currentStaminaStrain += staminaValue * staminaMultiplier;

            double totalValue = DiffUtils.Norm(meanExponent,
                totalBurstStrain,
                currentStreamStrain,
                currentStaminaStrain) * totalMultiplier;

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalValue);

            return totalValue;
        }

        private double calculateAdjustedDifficulty(DifficultyHitObject current)
        {
            double difficulty = SpeedEvaluator.EvaluateDifficultyOf(current);

            if (Mods.Any(m => m is OsuModAutopilot))
                difficulty *= 0.5;

            return difficulty;
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

            if (ObjectWeightSum == 0)
                return 0.0;

            double consistentTopNote = difficultyValue / ObjectWeightSum; // What would the top note be if all note values were identical

            if (consistentTopNote == 0)
                return 0;

            // Use a weighted sum of all notes. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DiffUtils.Logistic(s / consistentTopNote, 0.88, 10, 1.1));
        }
    }
}
