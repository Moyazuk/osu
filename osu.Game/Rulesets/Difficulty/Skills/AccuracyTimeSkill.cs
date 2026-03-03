// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Difficulty.Skills
{
    public abstract class AccuracyTimeSkill : Skill
    {
        protected AccuracyTimeSkill(Mod[] mods)
            : base(mods)
        {
        }

        private const double ms_to_minutes = 1.0 / 60000.0;

        // FC time specific constants
        private const double time_threshold_minutes = 24;
        private const double max_delta_time = 5000;
        private const double retry_cooldown_time = 60000;

        private const double epsilon = 1e-4;

        private readonly List<double> times = new List<double>();
        private readonly List<DifficultyHitObject> hitObjects = new List<DifficultyHitObject>();

        /// <summary>
        /// Returns the strain value at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double StrainValueAt(DifficultyHitObject current);

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            times.Add(current.Index == 0
                ? retry_cooldown_time + Math.Min(current.DeltaTime, max_delta_time)
                : times.Last() + Math.Min(current.DeltaTime, max_delta_time));

            hitObjects.Add(current);

            return StrainValueAt(current);
        }

        protected abstract IJudgementProbabilities JudgementProbabilities(double skill, double difficulty, DifficultyHitObject hitObject);

        public override double DifficultyValue()
        {
            if (ObjectDifficulties.Count == 0 || ObjectDifficulties.Max() <= epsilon)
                return 0;

            // Lower bound and upper bound are generally unimportant
            return RootFinding.FindRootExpand(skill => getScoreLossAtSkill(skill) - time_threshold_minutes, 0, 10);
        }

        /// <summary>
        /// The coefficients of a quartic fitted to the miss counts at each skill level.
        /// </summary>
        /// <returns>The coefficients for our penalty polynomial.</returns>
        public double[] GetScoreLossCoefficients()
        {
            Dictionary<double, double> scoreLoss = new Dictionary<double, double>();

            // If there are no notes, we just return a zero-polynomial.
            if (ObjectDifficulties.Count == 0 || ObjectDifficulties.Max() == 0)
                return Array.Empty<double>();

            double ssSkill = DifficultyValue();

            foreach (double skillProportion in PolynomialPenaltyUtils.SKILL_PROPORTIONS)
            {
                if (skillProportion == 1)
                {
                    scoreLoss[skillProportion] = 0;
                    continue;
                }

                double penalizedSkill = ssSkill * skillProportion;

                // We take the log to squash miss counts, which have large absolute value differences, but low relative differences, into a straighter line for the polynomial.
                scoreLoss[skillProportion] = Math.Log(getScoreLossAtSkill(penalizedSkill) + 1);
            }

            return PolynomialPenaltyUtils.GetPenaltyCoefficients(scoreLoss);
        }

        /// <summary>
        /// Find the highest accuracy (lowest score loss) that a player with the provided <paramref name="skill"/> would likely achieve within 12 minutes of retrying.
        /// </summary>
        private double getScoreLossAtSkill(double skill)
        {
            double maxDiff = ObjectDifficulties.Max();

            if (maxDiff == 0)
                return 0;
            if (skill <= 0)
                // TODO: HACK shouldn't be multiplying by 300 like a constant
                return ObjectDifficulties.Count * 300;

            IterativePoissonBinomial poiBin = new IterativePoissonBinomial();

            return Math.Max(0, RootFinding.FindRootExpand(x => retryTimeRequiredToObtainScoreLoss(x) - time_threshold_minutes, -50, 1000, accuracy: 0.01));

            double retryTimeRequiredToObtainScoreLoss(double scoreLoss)
            {
                poiBin.Reset();
                double timeSpentRetrying = 0;

                for (int n = ObjectDifficulties.Count - 1; n >= 0; n--)
                {
                    double deltaTime = n > 0 ? times[n] - times[n - 1] : times[n];
                    IJudgementProbabilities judgementProbabilities = JudgementProbabilities(skill, ObjectDifficulties[n], hitObjects[n]).Inverse();
                    poiBin.Add(judgementProbabilities.Score, judgementProbabilities.Variance, judgementProbabilities.Gamma);

                    double missCountProb = poiBin.Cdf(scoreLoss);
                    timeSpentRetrying += missCountProb > 0 ? deltaTime / missCountProb - deltaTime : double.PositiveInfinity;
                }

                return timeSpentRetrying * ms_to_minutes;
            }
        }

        /// <summary>
        /// Calculates the number of strains weighted against the top strain.
        /// The result is scaled by clock rate as it affects the total number of strains.
        /// </summary>
        public virtual double CountTopWeightedStrains(double difficultyValue)
        {
            if (ObjectDifficulties.Count == 0)
                return 0.0;

            // What would the top strain be if all strain values were identical.
            // We don't have decay weight in FC time, so we just use the old live one of 0.95.
            double consistentTopStrain = difficultyValue * (1 - 0.95);

            if (consistentTopStrain == 0)
                return ObjectDifficulties.Count;

            // Use a weighted sum of all strains. Constants are arbitrary and give nice values
            return ObjectDifficulties.Sum(s => 1.1 / (1 + Math.Exp(-10 * (s / consistentTopStrain - 0.88))));
        }

        public static double DifficultyToPerformance(double difficulty) => 4.0 * Math.Pow(difficulty, 3.0);
    }
}
