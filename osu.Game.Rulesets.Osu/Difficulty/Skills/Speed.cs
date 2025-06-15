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
    public class Speed : Skill
    {
        private double totalMultiplier => 0.675;
        private double burstMultiplier => 3.025;
        private double streamMultiplier => 0.085;
        private double staminaMultiplier => 0.05;
        private double meanFactor => 1.25;

        private readonly List<double> noteDifficulties = new List<double>();

        private readonly List<double> noteWeights = new List<double>();

        private readonly List<double> sliderStrains = new List<double>();

        private double currentBurstStrain;
        private double currentStreamStrain;
        private double currentStaminaStrain;
        private double currentRhythm;

        public readonly bool WithoutStamina;

        public Speed(Mod[] mods, bool withoutStamina)
            : base(mods)
        {
            WithoutStamina = withoutStamina;
        }

        private double strainDecayBurst(double ms) => Math.Pow(0.025, ms / 1000);
        private double strainDecayStream(double ms) => Math.Pow(0.01, Math.Pow(ms / 1000, 1.6));

        private double strainDecayStamina(double ms, double staminaValue)
        {
            double changeFactor = currentStaminaStrain > 0 ? 1 + Math.Pow(currentStaminaStrain / (staminaValue + currentStaminaStrain), 25) : 1;
            return Math.Pow(0.05, Math.Pow(ms * changeFactor / 1000, 3.5));
        }

        public override void Process(DifficultyHitObject current)
        {
            currentBurstStrain *= strainDecayBurst(((OsuDifficultyHitObject)current).StrainTime);
            currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);
            currentBurstStrain += SpeedEvaluator.EvaluateDifficultyOf(current, Mods) * burstMultiplier;

            if (WithoutStamina)
            {
                double totalStrain = currentBurstStrain * currentRhythm;

                if (current.BaseObject is Slider)
                    sliderStrains.Add(totalStrain);

                noteDifficulties.Add(totalStrain);
                return;
            }

            double staminaValue = StaminaEvaluator.EvaluateDifficultyOf(current);

            currentStreamStrain *= strainDecayStream(((OsuDifficultyHitObject)current).StrainTime);
            currentStreamStrain += staminaValue * streamMultiplier;

            currentStaminaStrain *= strainDecayStamina(((OsuDifficultyHitObject)current).StrainTime, staminaValue * staminaMultiplier);
            currentStaminaStrain += staminaValue * staminaMultiplier;

            double totalValue =
                Math.Pow(
                    Math.Pow(currentBurstStrain * currentRhythm, meanFactor) +
                    Math.Pow(currentStreamStrain, meanFactor) +
                    Math.Pow(currentStaminaStrain, meanFactor), 1.0 / meanFactor
                );

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalValue);

            noteDifficulties.Add(totalValue * totalMultiplier);
        }

        public override double DifficultyValue()
        {
            double difficulty = 0;

            // Notes with 0 difficulty are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These notes will not contribute to the difficulty.
            var peaks = noteDifficulties.Where(p => p > 0);

            List<double> notes = peaks.ToList();

            int index = 0;

            // Difficulty is the weighted sum of the highest notes.
            // We're sorting from highest to lowest note.
            foreach (double note in notes.OrderDescending())
            {
                // Use a harmonic sum for note which effectively buffs maps with more notes, especially if note difficulties are consistent.
                // Constants are arbitrary and give good values.
                // https://www.desmos.com/calculator/gquji01mlg
                double weight = (1.0 + (20.0 / (1 + index))) / (Math.Pow(index, 0.85) + 1.0 + (20.0 / (1.0 + index)));

                noteWeights.Add(weight);

                difficulty += note * weight;
                index += 1;
            }

            return difficulty;
        }

        /// <summary>
        /// Returns the number of relevant objects weighted against the top note.
        /// </summary>
        public double CountTopWeightedNotes()
        {
            if (noteDifficulties.Count == 0)
                return 0.0;

            double consistentTopNote = DifficultyValue() / noteWeights.Sum(); // What would the top note be if all note values were identical

            if (consistentTopNote == 0)
                return noteDifficulties.Count;

            // Use a weighted sum of all notes. Constants are arbitrary and give nice values
            return noteDifficulties.Sum(s => 1.1 / (1 + Math.Exp(-5 * (s / consistentTopNote - 2))));
        }

        public double RelevantNoteCount()
        {
            if (noteDifficulties.Count == 0)
                return 0;

            double consistentTopNote = DifficultyValue() / noteWeights.Sum(); // What would the top note be if all note values were identical

            if (noteWeights.Sum() == 0)
                return 0.0;

            if (consistentTopNote == 0)
                return noteDifficulties.Count;

            return noteDifficulties.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / consistentTopNote * 12.0 - 7.0))));
        }

        public double CountTopWeightedSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double consistentTopNote = DifficultyValue() / noteWeights.Sum(); // What would the top strain be if all strain values were identical

            if (consistentTopNote == 0)
                return 0;

            // Use a weighted sum of all strains. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DifficultyCalculationUtils.Logistic(s / consistentTopNote, 3, 5, 1.1));
        }
    }
}
