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
    public abstract class TimeSkill : Skill
    {
        protected TimeSkill(Mod[] mods)
            : base(mods)
        {
        }

        private const double ms_to_minutes = 1.0 / 60000.0;

        private const double time_threshold_minutes = 24;
        private const double max_delta_time = 5000;
        private const double retry_cooldown_time = 60000;

        private const double epsilon = 1e-4;

        private readonly List<double> times = new List<double>();
        private readonly List<DifficultyHitObject> hitObjects = new List<DifficultyHitObject>();
        protected IReadOnlyList<double>? CachedStrainSequence => cachedStrainSequence;
        protected readonly List<double> SnapDifficulties = new List<double>();
        protected readonly List<double> FlowDifficulties = new List<double>();



        private List<double>? cachedStrainSequence;

        protected abstract void StrainValueAt(DifficultyHitObject current);

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            times.Add(current.Index == 0
                ? retry_cooldown_time + Math.Min(current.DeltaTime, max_delta_time)
                : times.Last() + Math.Min(current.DeltaTime, max_delta_time));
            hitObjects.Add(current);

            StrainValueAt(current);
            return 0;
        }

        protected abstract double HitProbability(double skill, double difficulty);
        protected virtual double HitProbabilitySnap(double skill, double difficulty) => HitProbability(skill, difficulty);
        protected virtual double HitProbabilityFlow(double skill, double difficulty) => HitProbability(skill, difficulty);

        protected virtual double PFlow(double snapDifficulty, double flowDifficulty) => 0;

        protected virtual double StrainDecay(DifficultyHitObject current) => 1.0;

        public override double DifficultyValue()
        {
            if (SnapDifficulties.Count == 0)
                return 0;

            double maxDiff = SnapDifficulties.Zip(FlowDifficulties, Math.Max).Max();

            if (maxDiff <= epsilon)
                return 0;

            double fcSkill = RootFinding.FindRootExpand(skill => timeSpentRetryingAtSkill(skill) - time_threshold_minutes, 0, 10);

            cachedStrainSequence = computeStrainSequence(fcSkill);

            return fcSkill;
        }

        private double timeSpentRetryingAtSkill(double skill)
        {
            if (skill <= 0) return double.PositiveInfinity;

            double timeSpentRetrying = 0;
            double hitProbabilityProduct = 1;
            double currentStrain = 0;

            for (int n = 0; n < SnapDifficulties.Count; n++)
            {
                double decay = StrainDecay(hitObjects[n]);
                double decayedStrain = currentStrain * decay;

                double snapStrain = decayedStrain + SnapDifficulties[n];
                double flowStrain = decayedStrain + FlowDifficulties[n];

                double pFlow = PFlow(snapStrain, flowStrain);
                double pSnap = 1 - pFlow;

                double pHit = HitProbabilitySnap(skill, snapStrain) * pSnap + HitProbabilityFlow(skill, flowStrain) * pFlow;

                currentStrain = snapStrain * pSnap + flowStrain * pFlow;

                double deltaTime = n > 0 ? times[n] - times[n - 1] : times[n];

                hitProbabilityProduct *= pHit;
                timeSpentRetrying += hitProbabilityProduct > 0 ? deltaTime / hitProbabilityProduct - deltaTime : double.PositiveInfinity;
            }

            return timeSpentRetrying * ms_to_minutes;
        }

        private List<double> computeStrainSequence(double skill)
        {
            var strains = new List<double>(SnapDifficulties.Count);
            double currentStrain = 0;

            for (int n = 0; n < SnapDifficulties.Count; n++)
            {
                double decay = StrainDecay(hitObjects[n]);
                double decayedStrain = currentStrain * decay;

                double snapStrain = decayedStrain + SnapDifficulties[n];
                double flowStrain = decayedStrain + FlowDifficulties[n];

                double pFlow = PFlow(snapStrain, flowStrain);
                double pSnap = 1 - pFlow;

                currentStrain = snapStrain * pSnap + flowStrain * pFlow;
                strains.Add(currentStrain);
            }

            return strains;
        }

        public double[] GetMissPenaltyCoefficients()
        {
            Dictionary<double, double> missCounts = new Dictionary<double, double>();

            if (SnapDifficulties.Count == 0)
                return Array.Empty<double>();

            double fcSkill = DifficultyValue();

            foreach (double skillProportion in PolynomialPenaltyUtils.SKILL_PROPORTIONS)
            {
                if (skillProportion == 1)
                {
                    missCounts[skillProportion] = 0;
                    continue;
                }

                double penalizedSkill = fcSkill * skillProportion;
                missCounts[skillProportion] = Math.Log(getMissCountAtSkill(penalizedSkill) + 1);
            }

            return PolynomialPenaltyUtils.GetPenaltyCoefficients(missCounts);
        }

        private double getMissCountAtSkill(double skill)
        {
            if (SnapDifficulties.Count == 0)
                return 0;
            if (skill <= 0)
                return SnapDifficulties.Count;

            IterativePoissonBinomial poiBin = new IterativePoissonBinomial();

            return Math.Max(0, RootFinding.FindRootExpand(x => retryTimeRequiredToObtainMissCount(x) - time_threshold_minutes, -50, 1000, accuracy: 0.01));

            double retryTimeRequiredToObtainMissCount(double missCount)
            {
                poiBin.Reset();
                double timeSpentRetrying = 0;
                double currentStrain = 0;

                for (int n = 0; n < SnapDifficulties.Count; n++)
                {
                    double decay = StrainDecay(hitObjects[n]);
                    double decayedStrain = currentStrain * decay;

                    double snapStrain = decayedStrain + SnapDifficulties[n];
                    double flowStrain = decayedStrain + FlowDifficulties[n];

                    double pFlow = PFlow(snapStrain, flowStrain);
                    double pSnap = 1 - pFlow;

                    double pHit = HitProbabilitySnap(skill, snapStrain) * pSnap + HitProbabilityFlow(skill, flowStrain) * pFlow;
                    double missProbability = 1 - pHit;

                    currentStrain = snapStrain * pSnap + flowStrain * pFlow;

                    double deltaTime = n > 0 ? times[n] - times[n - 1] : times[n];

                    poiBin.AddProbability(missProbability);

                    double missCountProb = poiBin.Cdf(missCount);
                    timeSpentRetrying += missCountProb > 0 ? deltaTime / missCountProb - deltaTime : double.PositiveInfinity;
                }

                return timeSpentRetrying * ms_to_minutes;
            }
        }

        public virtual double CountTopWeightedStrains(double difficultyValue)
        {
            if (cachedStrainSequence is null || cachedStrainSequence.Count == 0)
                return 0.0;

            double consistentTopStrain = difficultyValue * (1 - 0.95);

            if (consistentTopStrain == 0)
                return cachedStrainSequence.Count;

            return cachedStrainSequence.Sum(s => 1.1 / (1 + Math.Exp(-10 * (s / consistentTopStrain - 0.88))));
        }

        public static double DifficultyToPerformance(double difficulty) => 4.0 * Math.Pow(difficulty, 3.0);
    }
}
