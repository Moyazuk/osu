// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;
using static osu.Game.Rulesets.Osu.Difficulty.Preprocessing.OsuDifficultyHitObject;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class SnapAimEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            // Base snap difficulty is velocity.
            double difficulty = EvaluateDistanceBonus(current) * 240;
            //difficulty += EvaluateAgilityBonus(current) * 65;
            difficulty += EvaluateAngleBonus(current) * 190;
            difficulty += EvaluateVelocityChangeBonus(current) * 40;

            return difficulty;
        }

        public static double EvaluateDistanceBonus(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;

            // Base snap difficulty is velocity.
            double distanceBonus = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;

            return distanceBonus;
        }

        public static double EvaluateAgilityBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 2))
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double currVelocity = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;
            double prevVelocity = osuPrevObj.Movement.Length / osuPrevObj.StrainTime;

            double currDistanceMultiplier = Smootherstep(osuCurrObj.RawMovement.Length / osuCurrObj.Radius, 0.5, 1);
            double prevDistanceMultiplier = Smootherstep(osuPrevObj.RawMovement.Length / osuPrevObj.Radius, 0.5, 1);

            // If the previous notes are stacked, we add the previous note's strainTime since there was no movement since at least 2 notes earlier.
            // https://youtu.be/-yJPIk-YSLI?t=186
            double currTime = osuCurrObj.StrainTime + osuPrevObj.StrainTime * (1 - prevDistanceMultiplier);
            double prevTime = osuPrevObj.StrainTime;

            double currentAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;

            // We reward high bpm more for wider angles, but only when both current and previous distance are over 0.5 radii.
            double baseBpm = 240.0 / (1 + 0.10 * Smootherstep(currentAngle, 0, 120) * currDistanceMultiplier * prevDistanceMultiplier);

            // Agility bonus of 1 at base BPM.
            double agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, 6) - 1);

            return agilityBonus * 1.45;
        }

        public static double EvaluateAngleBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 3, 1))
                return 1;

            OsuDifficultyHitObject osuCurrObj = (OsuDifficultyHitObject)current;
            OsuDifficultyHitObject osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double currAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;

            double currVelocity = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;
            double prevVelocity = osuPrevObj.Movement.Length / osuPrevObj.StrainTime;

            // We scale angle bonus by the amount of overlap between the previous 2 notes. This addresses cheesable angles
            double prevDistanceMultiplier = Smootherstep(osuPrevObj.RawMovement.Length / osuPrevObj.Radius, 0.5, 1);

            // We also scale angle bonus by the difference in velocity from prevPrev -> prev and prev -> current. This addresses cut stream patterns.
            prevDistanceMultiplier *= currVelocity > 0 ? Math.Min(1, prevVelocity * 1.4 / currVelocity) : 1;

            double angleBonus = Smootherstep(currAngle, 0, 180) * currVelocity * prevDistanceMultiplier; // Gengaozo pattern

            return angleBonus;
        }

        public static double EvaluateVelocityChangeBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 3))
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            Vector2 prevMovement = osuPrevObj.Movement;
            Vector2 currMovement = osuCurrObj.Movement;

            double currTime = osuCurrObj.StrainTime;
            double prevTime = osuPrevObj.StrainTime;

            double currVelocity = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;
            double prevVelocity = osuPrevObj.Movement.Length / osuPrevObj.StrainTime;

            double baseVelocityChange = Math.Max(0, Math.Min(Math.Abs(prevVelocity - currVelocity) - Math.Min(currVelocity, prevVelocity), Math.Max(osuCurrObj.Radius / Math.Max(osuCurrObj.StrainTime, osuPrevObj.StrainTime), Math.Min(currVelocity, prevVelocity))));

            double prevAngleBonus = CurveBuilder.BuildSmootherStep(osuPrevObj.Angle!.Value, (0, 0.4), (1.04, 0), (2.62, 1));
            double currAngleBonus = CurveBuilder.BuildSmootherStep(osuCurrObj.Angle!.Value, (0.52, 1), (2.09, 0), (3.14, 0.4));

            double angleBonus = 0.3 * prevAngleBonus + 0.7 * currAngleBonus;

            double overlapNerf = Math.Pow(Math.Clamp((osuCurrObj.RawMovement.Length - osuPrevObj.Radius / 1.5) / osuPrevObj.Radius, 0, 1), 2);

            double velChangeBonus = baseVelocityChange * ((1.3 + angleBonus) * overlapNerf);

            return velChangeBonus;
        }
    }
}
