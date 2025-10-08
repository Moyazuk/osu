// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AgilityEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double baseFactor = 1;
            double wideBonus = 1;

            if (osuCurrObj.Angle != null && osuPrevObj.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuPrevObj.Angle.Value;

                wideBonus += calcWideAngleBonus(currAngle) * 0.3;

                baseFactor = 1 - 0.25 * DifficultyCalculationUtils.Smoothstep(lastAngle, double.DegreesToRadians(90), double.DegreesToRadians(40)) * angleDifference(currAngle, lastAngle);
            }

            // Penalize angle repetition.
            double angleRepetitionNerf = Math.Pow(baseFactor + (1 - baseFactor) * angleVectorRepetition(osuCurrObj), 2);

            // Agility bonus of 1 at base BPM.
            double agilityBonus = Math.Max(0, Math.Pow(DifficultyCalculationUtils.MillisecondsToBPM(Math.Max(osuCurrObj.AdjustedDeltaTime, osuPrevObj.AdjustedDeltaTime), 2) / (270.0 / wideBonus), 4.0) - 1);

            double difficulty = agilityBonus * angleRepetitionNerf;

            difficulty *= osuCurrObj.SmallCircleBonus;

            return difficulty * 1.0;
        }

        private static double angleDifference(double curAngle, double lastAngle)
        {
            return Math.Cos(2 * Math.Min(Math.PI / 4, Math.Abs(curAngle - lastAngle)));
        }

        private static double angleVectorRepetition(OsuDifficultyHitObject current)
        {
            const double note_limit = 6;

            double constantAngleCount = 0;
            int index = 0;
            double notesProcessed = 0;

            while (notesProcessed < note_limit)
            {
                var loopObj = (OsuDifficultyHitObject)current.Previous(index);

                if (loopObj.IsNull())
                    break;

                if (Math.Abs(current.DeltaTime - loopObj.DeltaTime) > 25)
                    break;

                if (loopObj.NormalisedVectorAngle.IsNotNull() && current.NormalisedVectorAngle.IsNotNull())
                {
                    double angleDifference = Math.Abs(current.NormalisedVectorAngle.Value - loopObj.NormalisedVectorAngle.Value);
                    constantAngleCount += Math.Cos(8 * Math.Min(Math.PI / 16, angleDifference));
                }

                notesProcessed++;
                index++;
            }

            return Math.Pow(Math.Min(0.5 / constantAngleCount, 1), 2);
        }

        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(90), double.DegreesToRadians(140));
    }
}
