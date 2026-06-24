// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : TimeSkill
    {
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double currentStrain;

        private double currentJerkStrain;

        private double currentJerkStamina;

        private double previousPFlow;

        protected override double time_threshold_minutes => 960;

        private double skillMultiplierSnap => 125.9;
        private double skillMultiplierAgility => 0.00155;
        private double skillMultiplierFlow => 265.0;

        private double skillMultiplierJerkFlow => 1500000;
        private double skillMultiplierTotal => 1.12;
        private double combinedSnapNormExponent => 1.2;

        private double jerkStrainMultiplier => 0.45;

        private double jerkStaminaMultiplier => 0.55;

        private double jerkPowerMean => 1.5;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(0.2, ms / 1000);

        private double jerkStrainDecay(double ms) => Math.Pow(0.05
            , ms / 1000);

        private double jerkStaminaDecay(double ms) => Math.Pow(0.65
            , ms / 1000);

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double decay = strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            double jerkDecay = jerkStrainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            double jerkStamDecay = jerkStaminaDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * skillMultiplierSnap;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current) * skillMultiplierAgility;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders, previousPFlow) * skillMultiplierFlow;
            double flowJerkDifficulty = FlowAimEvaluator.EvaluateJerkDifficultyOf(current, IncludeSliders, previousPFlow) * skillMultiplierJerkFlow;

            var (totalDifficulty, totalJerk) = calculateTotalValue(snapDifficulty, agilityDifficulty, flowDifficulty, flowJerkDifficulty, current);

            currentStrain *= decay;
            currentStrain += totalDifficulty * (1 - decay);

            currentJerkStrain *= jerkDecay;
            currentJerkStrain += totalJerk * (1 - jerkDecay) * jerkStrainMultiplier;

            currentJerkStamina *= jerkStamDecay;
            currentJerkStamina += totalJerk * (1 - jerkStamDecay) * jerkStaminaMultiplier;

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            double result = currentStrain + DifficultyCalculationUtils.Norm(jerkPowerMean, currentJerkStrain, currentJerkStamina);

            return result;
        }

        private (double aim, double jerk) calculateTotalValue(double snapDifficulty, double agilityDifficulty, double flowDifficulty, double flowJerkDifficulty, DifficultyHitObject current)
        {
            // We compare flow to combined snap and agility because snap by itself doesn't have enough difficulty to be above flow on streams
            // Agility on the other hand is supposed to measure the rate of cursor velocity changes while snapping
            // So snapping every circle on a stream requires an enormous amount of agility at which point it's easier to flow
            double combinedSnapDifficulty = DifficultyCalculationUtils.Norm(1, snapDifficulty, agilityDifficulty);
            double combinedFlowDifficulty = DifficultyCalculationUtils.Norm(1, flowDifficulty, flowJerkDifficulty);

            double pSnap = calculateSnapFlowProbability(combinedFlowDifficulty / combinedSnapDifficulty);
            double pFlow = 1 - pSnap;

            if (current.Next(0) != null &&
                !(current.Next(0).BaseObject is Slider) &&
                !(current.Next(0).BaseObject is Spinner))
            {
                double nextSnapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current.Next(0), IncludeSliders) * skillMultiplierSnap;
                double nextAgilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current.Next(0)) * skillMultiplierAgility;
                double nextFlowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current.Next(0), IncludeSliders, pFlow) * skillMultiplierFlow;
                double nextJerkDifficulty = FlowAimEvaluator.EvaluateJerkDifficultyOf(current.Next(0), IncludeSliders, pFlow) * skillMultiplierJerkFlow;

                double nextCombinedSnap = DifficultyCalculationUtils.Norm(1, nextSnapDifficulty, nextAgilityDifficulty);
                double nextCombinedFlow = DifficultyCalculationUtils.Norm(1, nextFlowDifficulty, nextJerkDifficulty);

                double nextPSnap = calculateSnapFlowProbability(nextCombinedFlow / nextCombinedSnap);
                double nextPFlow = 1 - nextPSnap;

                double nextTotalDifficulty = nextSnapDifficulty * nextPSnap + nextFlowDifficulty * nextPFlow;
                double nextTotalJerk = nextAgilityDifficulty * nextPSnap + nextJerkDifficulty * nextPFlow;

                double nextFinal = nextTotalDifficulty + nextTotalJerk;

                double combinedFlowDifficultyWithLookahead = DifficultyCalculationUtils.Norm(1,
                    combinedFlowDifficulty, nextFinal);

                pSnap = calculateSnapFlowProbability(combinedFlowDifficultyWithLookahead / combinedSnapDifficulty);
                pFlow = 1 - pSnap;
            }

            previousPFlow = pFlow;

            if (Mods.Any(m => m is OsuModTouchDevice))
            {
                // we don't adjust agility here since agility represents TD difficulty in a decent enough way
                snapDifficulty = Math.Pow(snapDifficulty, 0.89);
                combinedSnapDifficulty = DifficultyCalculationUtils.Norm(combinedSnapNormExponent, snapDifficulty, agilityDifficulty);
            }

            if (Mods.Any(m => m is OsuModRelax))
            {
                combinedSnapDifficulty *= 0.75;
                flowDifficulty *= 0.6;
            }

            double totalDifficulty = snapDifficulty * pSnap + flowDifficulty * pFlow;

            double totalJerk = agilityDifficulty * pSnap + flowJerkDifficulty * pFlow;

            double totalAimStrain = totalDifficulty * skillMultiplierTotal;

            double totalJerkStrain = totalJerk * skillMultiplierTotal;

            return (totalAimStrain, totalJerkStrain);
        }

        // A function that turns the ratio of snap : flow into the probability of snapping/flowing
        // It has the constraints:
        // P(snap) + P(flow) = 1 (the object is always either snapped or flowed)
        // P(snap) = f(snap/flow), P(flow) = f(flow/snap) (ie snap and flow are symmetric and reversible)
        // Therefore: f(x) + f(1/x) = 1
        // 0 <= f(x) <= 1 (cannot have negative or greater than 100% probability of snapping or flowing)
        // This logistic function is a solution, which fits nicely with the general idea of interpolation and provides a tuneable constant
        private static double calculateSnapFlowProbability(double ratio)
        {
            const double k = 7.27;

            if (ratio == 0)
                return 0;

            if (double.IsNaN(ratio))
                return 1;

            return DifficultyCalculationUtils.Logistic(-k * Math.Log(ratio));
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders(double difficultyValue)
        {
            if (sliderStrains.Count == 0)
                return 0;

            double consistentTopStrain = difficultyValue * (1 - 0.9); // What would the top strain be if all strain values were identical

            if (consistentTopStrain == 0)
                return 0;

            // Use a weighted sum of all strains. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DifficultyCalculationUtils.Logistic(s / consistentTopStrain, 0.88, 10, 1.1));
        }
    }
}
