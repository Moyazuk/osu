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
        private static double multiplier => 129.5;

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

            var currMovement = osuCurrObj.Movement;
            var prevMovement = osuPrevObj.Movement;

            double currVelocity = currMovement.Length / osuCurrObj.StrainTime;
            double prevVelocity = prevMovement.Length / osuPrevObj.StrainTime;
            double minVelocity = Math.Min(currVelocity, prevVelocity);

            double currTime = osuCurrObj.StrainTime;

            double flowDistance;

            if (osuCurrObj.Angle != null && osuPrevObj.Angle != null && prevVelocity != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                flowDistance = CalculateArcLength(currMovement.Length, prevVelocity, currAngle);

                // Check for invalid values and reset flowDistance if necessary
                if (double.IsInfinity(flowDistance) || double.IsNaN(flowDistance))
                {
                    flowDistance = currMovement.Length;
                }
            }
            else
            {
                flowDistance = currMovement.Length;
            }

            // Base flow difficulty is distance / time, with a bit of time subtracted to buff speed flow.
            double difficulty = flowDistance / currTime;

            Console.WriteLine($"diff= {difficulty} cm = {currMovement.Length} pv = {prevVelocity} ca = {osuCurrObj.Angle.Value}");

            double velChangeBonus = Math.Abs(prevVelocity - currVelocity);

            difficulty += 0.65 * (velChangeBonus);

            return difficulty * multiplier;
        }

        private static double calculateAngleSpline(double angle, bool reversed)
        {
            if (reversed)
                return 1 - Math.Pow(Math.Sin(Math.Clamp(1.2 * angle - 5.4 * Math.PI / 12.0, 0, Math.PI / 2)), 2);

            return Math.Pow(Math.Sin(Math.Clamp(1.2 * angle - Math.PI / 4.0, 0, Math.PI / 2)), 2);
        }

        public static double CalculateArcLength(double length, double velocity, double angle)
        {
            double d = length;
            double a = angle; 
            double PVel = velocity;

            double m = (a / Math.PI) * d;
            double h = Math.Min(PVel, Math.Min(m, d - m));

            double h_L = m;
            double r_L = Math.Sqrt(m * m + Math.Pow((m * m - h * h) / (2 * h), 2));
            double v_L = -Math.Sqrt(r_L * r_L - m * m);

            double h_R = m;
            double v_R = (h * h + m * m - d * d + 2 * (d - m) * h_R) / (2 * h);
            double r_R = Math.Sqrt((d - h_R) * (d - h_R) + v_R * v_R);

            double leftArcLength = Integrate(0, m, x => ArcLengthIntegrand(x, h_L, v_L, r_L));
            double rightArcLength = Integrate(m, d, x => ArcLengthIntegrand(x, h_R, v_R, r_R));

            return leftArcLength + rightArcLength;
        }

        private static double ArcLengthIntegrand(double x, double h_C, double v_C, double r_C)
        {
            double derivative = -(x - h_C) / Math.Sqrt(r_C * r_C - (x - h_C) * (x - h_C));
            return Math.Sqrt(1 + derivative * derivative);
        }

        private static double Integrate(double a, double b, Func<double, double> f, int n = 1000)
        {
            double h = (b - a) / n;
            double sum = 0.5 * (f(a) + f(b));
            for (int i = 1; i < n; i++)
            {
                sum += f(a + i * h);
            }
            return sum * h;
        }
    }
}
