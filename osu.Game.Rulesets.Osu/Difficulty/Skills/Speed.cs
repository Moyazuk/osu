// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : HarmonicSkill
    {
        private double totalMultiplier => 1.0;
        private double burstMultiplier => Tuning.BurstMultiplier;
        private double streamMultiplier => Tuning.StreamMultiplier;
        private double staminaMultiplier => Tuning.StaminaMultiplier;
        private double meanExponent => Tuning.SpeedPowerMeanExponent;

        private double currentBurstStrain;
        private double currentStreamStrain;
        private double currentStaminaStrain;
        private double currentRhythm;

        private readonly List<double> sliderStrains = new List<double>();
        public readonly bool WithoutStamina;

        protected OsuDifficultyTuning Tuning { get; }

        public Speed(Mod[] mods, OsuDifficultyTuning tuning, bool withoutStamina)
            : base(mods)
        {
            Tuning = tuning;
            WithoutStamina = withoutStamina;
        }

        protected override double HarmonicScale => 5;
        protected override double DecayExponent => 0.9;

        private double strainDecayBurst(double ms) => Math.Pow(Tuning.StrainDecayBurst, ms / 1000);
        private double strainDecayStream(double ms) => Math.Pow(Tuning.StrainDecayStream, Math.Pow(ms / 1000, Tuning.StrainDecayStreamExponent));

        private double strainDecayStamina(double ms, double staminaValue)
        {
            double changeFactor = currentStaminaStrain > 0 ? 1 + Math.Pow(currentStaminaStrain / (staminaValue + currentStaminaStrain), Tuning.StrainDecayStaminaChange) : 1.0;
            return Math.Pow(Tuning.StrainDecayStamina, Math.Pow(ms * changeFactor / 1000, Tuning.StrainDecayStaminaExponent));
        }

        protected override double ObjectDifficultyOf(DifficultyHitObject current)
        {
            currentBurstStrain *= strainDecayBurst(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current, Tuning);
            currentBurstStrain += SpeedEvaluator.EvaluateDifficultyOf(current, Tuning, Mods) * burstMultiplier;

            if (WithoutStamina)
            {
                double totalStrain = currentBurstStrain * currentRhythm;

                if (current.BaseObject is Slider)
                    sliderStrains.Add(totalStrain);

                return totalStrain;
            }

            double staminaValue = StaminaEvaluator.EvaluateDifficultyOf(current);

            currentStreamStrain *= strainDecayStream(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            currentStreamStrain += staminaValue * streamMultiplier;

            currentStaminaStrain *= strainDecayStamina(((OsuDifficultyHitObject)current).AdjustedDeltaTime, staminaValue * staminaMultiplier);
            currentStaminaStrain += staminaValue * staminaMultiplier;

            double totalValue = DifficultyCalculationUtils.Norm(meanExponent,
                currentBurstStrain + currentRhythm,
                currentStreamStrain,
                currentStaminaStrain);

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalValue);

            return totalValue * totalMultiplier;
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
