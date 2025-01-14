// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuProbabilitySkill
    {
        public Aim(Mod[] mods, bool withSliders)
            : base(mods)
        {
        }
        enum AimTypes
        {
            Snap,
            Flow,
            Agility
        }
        struct AimSkills
        {
            public double snapStrain;
            public double flowStrain;
            public double agilityStrain;
            public double OfType(AimTypes type)
            {
                switch (type)
                {
                    case AimTypes.Snap:
                        return snapStrain;
                    case AimTypes.Flow:
                        return flowStrain;
                    case AimTypes.Agility:
                        return agilityStrain;
                }
                throw new Exception("Erm... what the sigma");
            }
            public AimSkills(double snap, double flow, double agility)
            {
                snapStrain = snap;
                flowStrain = flow;
                agilityStrain = agility;
            }
        }

        private static readonly Dictionary<AimTypes, Func<DifficultyHitObject, double>> Evaluators = new()
        {
            { AimTypes.Snap, SnapAimEvaluator.EvaluateDifficultyOf },
            { AimTypes.Flow, FlowAimEvaluator.EvaluateDifficultyOf },
            { AimTypes.Agility, AgilityEvaluator.EvaluateDifficultyOf }
        };

        private readonly List<AimSkills> previousStrains = new List<AimSkills>();

        private double strainDecayBase => 0.15;
        private double strainIncreaseRate => 10;
        private static Dictionary<AimTypes, double>  strainInfluence = new()
        {
            {AimTypes.Snap, 1},
            {AimTypes.Flow, 0.5},
            {AimTypes.Agility, 0}
        };

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty == 0) return 1;
            if (skill == 0) return 0;

            return SpecialFunctions.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            (double currentStrain, double retainedStrain) snapStrain = calculateTypeStrain(current, AimTypes.Snap);
            (double currentStrain, double retainedStrain) flowStrain = calculateTypeStrain(current, AimTypes.Flow);
            (double currentStrain, double retainedStrain) agilityStrain = calculateTypeStrain(current, AimTypes.Agility);

            if (flowStrain.currentStrain < snapStrain.currentStrain + agilityStrain.currentStrain)
            {
                previousStrains.Add(new AimSkills(snapStrain.retainedStrain, flowStrain.currentStrain, agilityStrain.retainedStrain));
                return Math.Max(flowStrain.currentStrain, snapStrain.retainedStrain + agilityStrain.retainedStrain);
            }
            previousStrains.Add(new AimSkills(snapStrain.currentStrain, flowStrain.retainedStrain, agilityStrain.currentStrain));
            return Math.Max(flowStrain.retainedStrain, snapStrain.currentStrain + agilityStrain.currentStrain);
        }
        // flowStrain is the strain if this object is flowed, retainedFlowStrain is the amount of strain retained from previous objects if it is not
        private (double currentStrain, double retainedStrain) calculateTypeStrain(DifficultyHitObject current, AimTypes type)
        {
            // Validate inputs
            if (current == null)
                throw new ArgumentNullException(nameof(current), "DifficultyHitObject cannot be null.");
            if (!Enum.IsDefined(typeof(AimTypes), type))
                throw new ArgumentException($"Invalid AimTypes value: {type}");

            // Validate evaluator
            if (!Evaluators.TryGetValue(type, out var evaluator) || evaluator == null)
                throw new ArgumentException($"No evaluator found for aim type: {type}");

            // Validate strainInfluence
            if (strainInfluence == null || !strainInfluence.ContainsKey(type))
                throw new ArgumentException($"strainInfluence does not contain an entry for aim type: {type}");

            // Perform calculations
            double currentDifficulty = evaluator(current);
            double priorDifficulty = highestPreviousStrain(current, current.DeltaTime, type);
            double debugConstant = 4;

            if (priorDifficulty < 0)
                throw new InvalidOperationException($"Invalid prior difficulty for type {type}.");

            double currentStrain = getStrainValueOf(currentDifficulty, priorDifficulty);
            double retainedStrain = getStrainValueOf(0, priorDifficulty);
            return (currentDifficulty + currentStrain * strainInfluence[type], retainedStrain * strainInfluence[type]);
        }

        private double getStrainValueOf(double currentDifficulty, double priorDifficulty) => (priorDifficulty * strainIncreaseRate + currentDifficulty) / (strainIncreaseRate + 1);

        private double highestPreviousStrain(DifficultyHitObject current, double time, AimTypes type)
        {
            double hardestPreviousDifficulty = 0;
            double cumulativeDeltaTime = time;

            double timeDecay(double ms) => Math.Pow(strainDecayBase, Math.Pow(ms / 900, 7));

            for (int i = 0; i < previousStrains.Count; i++)
            {
                if (cumulativeDeltaTime > 1200)
                {
                    previousStrains.RemoveRange(0, i);
                    break;
                }

                hardestPreviousDifficulty = Math.Max(hardestPreviousDifficulty, previousStrains[^(i + 1)].OfType(type) * timeDecay(cumulativeDeltaTime));

                cumulativeDeltaTime += current.Previous(i).DeltaTime;
            }

            return hardestPreviousDifficulty;
        }
    }
}
