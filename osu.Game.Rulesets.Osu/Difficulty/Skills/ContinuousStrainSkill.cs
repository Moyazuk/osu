// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using System.Linq;
// ReSharper disable once RedundantUsingDirective
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class ContinuousStrainSkill : Skill
    {

        /// <summary>
        /// The final multiplier to be applied to <see cref="DifficultyValue"/> after all other calculations.
        /// </summary>
        protected virtual double DifficultyMultiplier => 1;

        protected virtual double SectionLength => 400;
        protected virtual double DecayWeight => 0.85;
        protected abstract double StrainDecayBase { get; }

        private double currentStrain;

        protected struct StrainValue
        {
            public double Strain;
            public int StrainCountChange;
        }

        protected readonly List<StrainValue> Strains = new List<StrainValue>();

        protected ContinuousStrainSkill(Mod[] mods)
            : base(mods)
        {
        }

        protected double StrainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);

        public override double DifficultyValue()
        {
            double result = 0.0;
            double currentWeight = 1;
            double frequency = 0;
            var sortedStrains = Strains.OrderByDescending(x => (x.Strain, x.StrainCountChange)).ToList();

            double strainDecayRate = Math.Log(StrainDecayBase) / 1000;
            double sumDecayRate = Math.Log(DecayWeight) / SectionLength;

            for (int i = 0; i < sortedStrains.Count - 1; i++)
            {
                var current = sortedStrains[i];
                var next = sortedStrains[i + 1];
                frequency += current.StrainCountChange;

                if (frequency > 0 && current.Strain > 0)
                {
                    double time = Math.Log(next.Strain / current.Strain) * (frequency / strainDecayRate);

                    double nextWeight = currentWeight * Math.Exp(sumDecayRate * time);
                    double combinedDecay = SectionLength * (sumDecayRate + (strainDecayRate / frequency));
                    result += (next.Strain * nextWeight - current.Strain * currentWeight) / combinedDecay;
                    currentWeight = nextWeight;
                }
            }

            return result * DifficultyMultiplier;
        }

        protected abstract double StrainValueAt(DifficultyHitObject hitObject);

        public override void Process(DifficultyHitObject current)
        {
            Strains.Add(new StrainValue { Strain = currentStrain * Math.Pow(StrainDecayBase, current.DeltaTime / 1000), StrainCountChange = -1 });
            currentStrain = StrainValueAt(current);
            Strains.Add(new StrainValue { Strain = currentStrain, StrainCountChange = 1 });
        }

        /// <summary>
        /// Calculates the number of strains weighted against the top strain.
        /// The result is scaled by clock rate as it affects the total number of strains.
        /// </summary>
        /// <summary>
        /// Returns the number of relevant objects weighted against the top strain.
        /// </summary>
        public double CountRelevantObjects()
        {
            double consistentTopStrain = DifficultyValue() / 10; // What would the top strain be if all strain values were identical
            if (consistentTopStrain == 0)
                return 0.0;

            // Being consistently difficult for 1000 notes should be worth more than being consistently difficult for 100.
            double totalStrains = Strains.Count;
            double lengthFactor = 0.73 * Math.Pow(0.998759, totalStrains);
            // Use a weighted sum of all strains. Constants are arbitrary and give nice values
            return Strains.Sum(s => (1.0 - lengthFactor) / (1 + Math.Exp(-10 * (s.Strain / consistentTopStrain - 0.87 - lengthFactor / 4.0))));
        }

        public static double DifficultyToPerformance(double difficulty) => Math.Pow(5.0 * Math.Max(1.0, difficulty / 0.0675) - 4.0, 3.0) / 100000.0;
    }
}
