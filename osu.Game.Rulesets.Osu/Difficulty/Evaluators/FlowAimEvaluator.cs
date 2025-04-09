// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowAimEvaluator
    {
        private static double multiplier => 0.0045;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.Index <= 2 ||
                current.BaseObject is Spinner ||
                current.Previous(0).BaseObject is Spinner ||
                current.Previous(1).BaseObject is Spinner ||
                current.Previous(2).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double currTime = osuCurrObj.StrainTime;

            // Base flow difficulty is distance / time, with a bit of time subtracted to buff speed flow.
            double difficulty = Math.Pow(osuCurrObj.LazyJumpDistance, 2) / currTime;

            double currVelocity = osuCurrObj.LazyJumpDistance / osuCurrObj.StrainTime;
            double prevVelocity = osuPrevObj.LazyJumpDistance / osuPrevObj.StrainTime;
            double minVelocity = Math.Min(currVelocity, prevVelocity);

            double angleBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuPrevObj.Angle.Value;

                // We reward for angle changes or the acuteness of the angle, whichever is higher. Possibly a case out there to reward both.
                angleBonus = Math.Max(
                    Math.Pow(Math.Sin((currAngle - lastAngle) / 2), 2) * minVelocity,
                    calculateAngleSpline(Math.Abs(currAngle), true));
            }

            difficulty += 320 * angleBonus;

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
