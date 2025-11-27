// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public abstract class Aim : OsuStrainSkill
    {
        public readonly bool IncludeSliders;

        public readonly bool IncludeControlFactors;
        protected Aim(Mod[] mods, bool includeSliders, bool includeControlFactors)
            : base(mods)
        {
            IncludeSliders = includeSliders;
            previousStrains = new List<(double, double)>();
            IncludeControlFactors = includeControlFactors;
        }
        private double skillMultiplier => 25.3;
        private double strainDecayBase => 0.15;

        private readonly List<(double, double)> previousStrains;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double offset, DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;

            double strain = getCurrentStrainValue(offset, previousStrains);

            return strain;
        }

        private const double backwards_strain_influence = 1000;

        protected abstract double StrainValueOf(DifficultyHitObject current);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;

            double currentStrain = getCurrentStrainValue(osuCurrent.StartTime, previousStrains) * 4.85;


            double noteDifficulty = StrainValueOf(current) * skillMultiplier;

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);


            previousStrains.Add((osuCurrent.StartTime, noteDifficulty));

            return noteDifficulty + currentStrain;
        }

        private double getCurrentStrainValue(double endTime, List<(double Time, double Diff)> previousDifficulties)
        {
            if (previousDifficulties.Count < 2)
                return 0;

            double sum = 0;

            double highestNoteVal = 0;
            double prevDeltaTime = 0;

            int index = 1;

            while (index < previousDifficulties.Count)
            {
                double prevTime = previousDifficulties[index - 1].Time;
                double currTime = previousDifficulties[index].Time;

                double deltaTime = currTime - prevTime;
                double prevDifficulty = previousDifficulties[index - 1].Diff;

                // How much of the current deltaTime does not fall under the backwards strain influence value.
                double startTimeOffset = Math.Max(0, endTime - prevTime - backwards_strain_influence);

                // If the deltaTime doesn't fall into the backwards strain influence value at all, we can remove its corresponding difficulty.
                // We don't iterate index because the list moves backwards.
                if (startTimeOffset > deltaTime)
                {
                    previousDifficulties.RemoveAt(0);

                    continue;
                }

                highestNoteVal = Math.Max(prevDifficulty, strainDecay(prevDeltaTime));
                prevDeltaTime = deltaTime;

                sum += highestNoteVal * (strainDecayAntiderivative(startTimeOffset) - strainDecayAntiderivative(deltaTime));

                index++;
            }

            // CalculateInitialStrain stuff
            highestNoteVal = Math.Max(previousDifficulties.Last().Diff, highestNoteVal);
            double lastTime = previousDifficulties.Last().Time;
            sum += (strainDecayAntiderivative(0) - strainDecayAntiderivative(endTime - lastTime)) * highestNoteVal;

            return sum;

            double strainDecayAntiderivative(double t) => Math.Pow(strainDecayBase, t / 1000) / Math.Log(1.0 / strainDecayBase);
        }

        private static double probabilityOfSnap(double snap, double flow)
        {
            // If snap is easier - we always use snap
            if (snap <= flow)
                return 1.0;

            // If flow is easier - we decrease the weight of the snap difficulty accordingly
            return Math.Pow(flow / snap, 3.5);
        }

        // Same semantics as your old FlowAim probabilityOfFlow
        private static double probabilityOfFlow(double snap, double flow)
        {
            // If flow is easier - we always use flow
            if (flow <= snap)
                return 1.0;

            // If snap is easier - we decrease the weight of the flow difficulty accordingly
            return Math.Pow(snap / flow, 3.5);
        }

        // Turn the two probabilities into a 0..1 lerp factor:
        // 0   -> pure "snap" strain
        // 1   -> pure "flow" strain
        private static double blendFactor(double pSnap, double pFlow)
        {
            double sum = pSnap + pFlow;
            if (sum <= 0 || double.IsNaN(sum))
                return 0.5; // neutral fallback

            return pFlow / sum;
        }

        private static double Lerp(double a, double b, double t)
            => a + (b - a) * t;

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
