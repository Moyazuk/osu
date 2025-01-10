// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Osu.Objects;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Objects;
using MathNet.Numerics.Interpolation;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using osu.Game.Rulesets.Osu.Difficulty.MathUtil;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class FingerControl
    {
        private const double strain_multiplier = 1.3;
        private const double repetition_weight = 0.7;
        private const double hard_strain_threshold = 1.1;

        private double identicalStrainTolerance;

        private List<double> noteHistory = new List<double>();
        private List<double> noteHistoryVirtual = new List<double>();
        private LinearSpline prevFractionSpline = LinearSpline.InterpolateSorted(
            new double[] { 1.0, 1.5 , 2.0, 3.0, 4.0 },
            new double[] { 0.5, 1.5, 0.9, 0.25, 0.0 } 
        );
        private LinearSpline nextFractionSpline = LinearSpline.InterpolateSorted(
            new double[] { 1.0 , 7.0/6.0, 1.5 , 1.75, 2.0, 3.0, 4.0 },
            new double[] { 0.05, 1.0    , 0.75, 1.0 , 0.5, 0.0, 0.0 }
        );

        private double compareStrains(double strain1, double strain2, LinearSpline fractionSpline)
        {
            if (strain1 == 0 || strain2 == 0)
                return 1;

            double fraction = Math.Max(strain1 / strain2, strain2 / strain1);
            return Math.Max(0.0, fractionSpline.Interpolate(fraction)); // spline can sometimes dip below 0 which breaks everything 
        }

        public FingerAttributes CalculateFingerControlDiff(List<DifficultyHitObject> hitObjects, double clockRate, double greatHitWindow)
        {
            if (hitObjects.Count == 0)
                return new FingerAttributes();

            double prevTime = hitObjects[0].StartTime / 1000.0;
            double prevStrainTime = 0;
            double prevVirtualStrainTime = 0;
            double currStrain = 0;
            List<double> strainHistory = new List<double> { 0 };
            List<double> specificStrainHistory = new List<double> { 0 };
            var sw = new StringWriter();
            sw.WriteLine($"{hitObjects[0].StartTime / 1000.0} 0 0");

            var hardStrainsAmount = 0;

            identicalStrainTolerance = greatHitWindow / 2000;

            // calculate strain value for each hit object
            for (int i = 1; i < hitObjects.Count; i++)
            {
                double currTime = hitObjects[i].StartTime / 1000.0;
                double deltaTime = (currTime - prevTime) / clockRate;

                double strainTime = Math.Max(deltaTime, 0.035);
                double virtualStrainTime = strainTime;
                double strainDecayBase = Math.Pow(0.75, 1 / Math.Min(strainTime, 0.15));

                currStrain *= Math.Pow(strainDecayBase, deltaTime);

                strainHistory.Add(currStrain);

                if (hitObjects[i-1].BaseObject is Slider prevSlider)
                    virtualStrainTime = Math.Max((currTime - prevSlider.EndTime / 1000.0) / clockRate, 0.035);
                
                double strain = strain_multiplier * RhythmEvaluator.EvaluateDifficultyOf(hitObjects[i]);

                
                if (i < hitObjects.Count - 1)
                {
                    double nextTime = hitObjects[i+1].StartTime / 1000.0;
                    double nextStrainTime = Math.Max((nextTime - currTime) / clockRate, 0.035);
                    double nextVirtualStrainTime = 0;
                    if (hitObjects[i].BaseObject is Slider currSlider)
                        nextVirtualStrainTime = Math.Max((nextTime - currSlider.EndTime / 1000.0) / clockRate, 0.035);

                    double multiplier = Math.Min(
                        Math.Min(compareStrains(strainTime, nextStrainTime, nextFractionSpline), compareStrains(strainTime, nextVirtualStrainTime, nextFractionSpline)),
                        Math.Min(compareStrains(virtualStrainTime, nextStrainTime, nextFractionSpline), compareStrains(virtualStrainTime, nextVirtualStrainTime, nextFractionSpline))
                    );
                    if (hitObjects[i+1].BaseObject is Slider)
                        multiplier /= 2;

                    strain *= multiplier;
                }
                else
                {
                    // last object strain can get too big because of lack of next object multiplier so we make it very low
                    strain *= 0.05;
                }

                specificStrainHistory.Add(strain);
                
                currStrain += strain;

                if (currStrain > 80)
                    hardStrainsAmount++;

                sw.WriteLine($"{currTime} {currStrain} {strain}");

                prevTime = currTime;
                
                if (deltaTime > 0.035)
                {
                    prevStrainTime = strainTime;
                    prevVirtualStrainTime = virtualStrainTime;
                }
            }

            string graphText = sw.ToString();
            sw.Dispose();

            var strainHistoryArray = strainHistory.ToArray();

            Array.Sort(strainHistoryArray);
            Array.Reverse(strainHistoryArray);

            double diff = 0;
            double k = 0.98;

            for (int i = 0; i < hitObjects.Count; i++)
                diff += strainHistoryArray[i] * Math.Pow(k, i);

            return new FingerAttributes
            {
                FingerDifficulty = diff * (1 - k),
                StrainHistory = specificStrainHistory,
                HardStrainAmount = hardStrainsAmount,
                Graph = graphText
            };
        }
    }
}
