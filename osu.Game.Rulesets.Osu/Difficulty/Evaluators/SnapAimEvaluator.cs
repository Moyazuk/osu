// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class SnapAimEvaluator
    {
        private static double multiplier => 60.75;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.Index <= 2 ||
                current.BaseObject is Spinner ||
                current.Previous(0).BaseObject is Spinner ||
                current.Previous(1).BaseObject is Spinner ||
                current.Previous(2).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj0 = (OsuDifficultyHitObject)current.Previous(0);
            var osuPrevObj1 = (OsuDifficultyHitObject)current.Previous(1);

            var currMovement = osuCurrObj.Movement;
            var prevMovement = osuPrevObj0.Movement;

            double currTime = osuCurrObj.StrainTime;
            double prevTime = osuPrevObj0.StrainTime;

            double currVelocity = currMovement.Length / currTime;
            double prevVelocity = prevMovement.Length / prevTime;
            double minVelocity = Math.Min(currVelocity, prevVelocity);

            // Base snap difficulty is velocity.
            double difficulty = currVelocity;

            // Add a bonus for agility.
            double agilityBonus = 5000 / (Math.Max(25, Math.Max(osuCurrObj.StrainTime, osuPrevObj0.StrainTime) - 50) * Math.Max(osuCurrObj.StrainTime, osuPrevObj0.StrainTime));

            double angleBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj0.Angle != null && osuPrevObj1.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuPrevObj0.Angle.Value;
                double lastLastAngle = osuPrevObj1.Angle.Value;

                // We give a bonus to the width of the angle.
                angleBonus = calculateAngleSpline(Math.Abs(currAngle), false) * Math.Min(minVelocity, (currMovement + prevMovement).Length / Math.Max(currTime, prevTime));

                // Slightly nerf the wide angle buff if there is a sharp angle in between 2 wide angles.
                angleBonus *= 1 - 0.25 * calculateAngleSpline(Math.Abs(lastAngle), true) * calculateAngleSpline(Math.Abs(lastLastAngle), false);
            }

            double velChangeBonus = Math.Max(0, Math.Min(Math.Abs(prevMovement.Length / prevTime - currMovement.Length / currTime) - minVelocity, Math.Max(50 / Math.Max(osuCurrObj.StrainTime, osuPrevObj0.StrainTime), minVelocity)));

            angleBonus *= 1;
            velChangeBonus *= 1;

            difficulty += agilityBonus * (1 + angleBonus + velChangeBonus);

            return difficulty * multiplier;
        }

        private static double calculateAngleSpline(double angle, bool reversed)
        {
            if (reversed)
                return 1 - Math.Pow(Math.Sin(Math.Clamp(1.2 * angle - 5.4 * Math.PI / 12.0, 0, Math.PI / 2)), 2);

            return Math.Pow(Math.Sin(Math.Clamp(1.2 * angle - Math.PI / 4.0, 0, Math.PI / 2)), 2);
        }
    }
}
