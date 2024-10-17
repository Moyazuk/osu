﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using AlsaSharp;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class ContinuousStrainSkill : Skill
    {

        /// <summary>
        /// The final multiplier to be applied to <see cref="DifficultyValue"/> after all other calculations.
        /// </summary>
        protected virtual double DifficultyMultiplier => 1.00;

        protected virtual double SectionLength => 400;
        protected virtual double DecayWeight => 0.9;
        protected abstract double StrainDecayBase { get; }

        private double currentStrain;

        protected struct StrainValue
        {
            public double Strain;
            public int StrainCountChange;
        }

        protected List<StrainValue> strains = new List<StrainValue>();

        protected ContinuousStrainSkill(Mod[] mods)
            : base(mods)
        {
        }

        public override double DifficultyValue()
        {
            double result = 0.0;
            double currentWeight = 1;
            double frequency = 0;
            var sortedStrains = strains.OrderByDescending(x => (x.Strain, x.StrainCountChange)).ToList();

            double strainDecayRate = Math.Log(StrainDecayBase) / 1000;
            double sumDecayRate = Math.Log(DecayWeight) / SectionLength;
            double strainIntegral = StrainIntegral();

            for (int i = 0; i < sortedStrains.Count - 1; ++i)
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

        public double StrainIntegral()
        {
            var sortedStrains = strains.OrderByDescending(x => (x.Strain, x.StrainCountChange)).ToList();

            Console.WriteLine($"Total number of strains: {sortedStrains.Count}");

            double strainDecayRate = Math.Log(StrainDecayBase) / 1000;
            double sumDecayRate = Math.Log(DecayWeight) / SectionLength;

            double strainIntegral = 0.0;
            double rectangleArea = 0.0;
            double strainRatio = 0.0;

            if (sortedStrains.Count == 0)
                return 0.0;

            double sumOfSquares = 0.0;

            for (int i = 0; i < sortedStrains.Count - 1; ++i)
            {
                double strain = sortedStrains[i].Strain;
                double squaredStrain = Math.Pow(strain, 2);
                sumOfSquares += squaredStrain;
            }
            double strainRms = Math.Sqrt(sumOfSquares / sortedStrains.Count);
            Console.WriteLine($"strainRms: {strainRms}");
            double rawConsistencyScore = 0.0;
            for (int i = 0; i < sortedStrains.Count; ++i)
            {
                var strainValue = sortedStrains[i];
                strainValue.Strain = strainValue.Strain - strainRms;
                rawConsistencyScore += strainValue.Strain;
                sortedStrains[i] = strainValue;
            }
            Console.WriteLine($"Raw Consistency Score: {rawConsistencyScore}");
            // take top 20% strains
            int topPercentageCount = (int)Math.Ceiling(sortedStrains.Count * 0.20);
            var topStrains = sortedStrains.Take(topPercentageCount).Select(x => x.Strain).ToList();

            // average 'em
            double averageTopStrain = topStrains.Average();

            if (averageTopStrain == 0)
                return 0.0;

            // normalize them to 10 (arbitrary) to keep behavior consistent across different difficulties
            double normalizationFactor = 10 / averageTopStrain;

            for (int i = 0; i < sortedStrains.Count - 1; ++i)
            {
                var current = sortedStrains[i];
                var next = sortedStrains[i + 1];

                double normalizedCurrentStrain = current.Strain * normalizationFactor;
                double normalizedNextStrain = next.Strain * normalizationFactor;

                double averageStrain = (normalizedCurrentStrain + normalizedNextStrain) / 2;
                double deltaTime = Math.Abs(next.StrainCountChange);
                strainIntegral += averageStrain * deltaTime;
            }
            rectangleArea += 10 * sortedStrains.Count;
            var last = sortedStrains.Last();
            double normalizedLastStrain = last.Strain * normalizationFactor;
            strainIntegral += normalizedLastStrain * last.StrainCountChange;
            strainRatio = 1 + rawConsistencyScore * 0.0000011;
            Console.WriteLine($"StrainRatio: {strainRatio}");
            return strainRatio;
        }


        protected abstract double StrainValueAt(DifficultyHitObject hitObject);

        public override void Process(DifficultyHitObject current)
        {
            strains.Add(new StrainValue { Strain = currentStrain * Math.Pow(StrainDecayBase, 1e-3 * current.DeltaTime), StrainCountChange = -1 });
            currentStrain = StrainValueAt(current);
            strains.Add(new StrainValue { Strain = currentStrain, StrainCountChange = 1 });
        }

    }
}
