// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AngleEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.Index <= 2 ||
                current.BaseObject is Spinner ||
                current.Previous(0).BaseObject is Spinner ||
                current.Previous(1).BaseObject is Spinner)
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj0 = (OsuDifficultyHitObject)current.Previous(0);
            var osuPrevObj1 = (OsuDifficultyHitObject)current.Previous(1);
            var osuLast2Obj = (OsuDifficultyHitObject)current.Previous(2);

            double currVelocity = osuCurrObj.LazyJumpDistance / osuCurrObj.StrainTime;
            double prevVelocity = osuPrevObj0.LazyJumpDistance / (osuPrevObj0.StrainTime);
            double currTime = osuCurrObj.StrainTime;
            double prevTime = osuPrevObj0.StrainTime;
            double acuteAngleBonus = 0;
            double wideAngleBonus = 0;
            double wiggleBonus = 0;
            double velocityChangeBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj0.Angle != null && osuPrevObj1.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuPrevObj0.Angle.Value;
                double angleBonus = Math.Min(currVelocity, prevVelocity);

                wideAngleBonus = calcWideAngleBonus(currAngle);
                acuteAngleBonus = calcAcuteAngleBonus(currAngle);

                // Penalize angle repetition.
                wideAngleBonus *= 0.45 + 0.55 * (1 - Math.Min(wideAngleBonus, Math.Pow(calcWideAngleBonus(lastAngle), 3)));
                wideAngleBonus *= 0.77 + 0.23 * (DifficultyCalculationUtils.Smootherstep(Math.Abs(currAngle - lastAngle), double.DegreesToRadians(0), double.DegreesToRadians(30)));
                acuteAngleBonus *= 0.15 + 0.85 * (1 - Math.Min(acuteAngleBonus, Math.Pow(calcAcuteAngleBonus(lastAngle), 3)));

                // Apply full wide angle bonus for distance more than one diameter
                wideAngleBonus *= angleBonus * DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance, 0, diameter);

                // Apply acute angle bonus for BPM above 300 1/2 and distance more than one diameter
                acuteAngleBonus *= angleBonus *
                                   DifficultyCalculationUtils.Smootherstep(DifficultyCalculationUtils.MillisecondsToBPM(osuCurrObj.StrainTime, 2), 200, 400) *
                                   DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance, diameter, diameter * 2);
            }

            if (osuLast2Obj != null)
            {
                // If objects just go back and forth through a middle point - don't give as much wide bonus
                // Use Previous(2) and Previous(0) because angles calculation is done prevprev-prev-curr, so any object's angle's center point is always the previous object
                var lastBaseObject = (OsuHitObject)osuPrevObj0.BaseObject;
                var last2BaseObject = (OsuHitObject)osuLast2Obj.BaseObject;

                float distance = (last2BaseObject.StackedPosition - lastBaseObject.StackedPosition).Length;

                if (distance < 1)
                {
                    wideAngleBonus *= 1 - 0.25 * (1 - distance);
                }
            }

            if (Math.Max(prevVelocity, currVelocity) != 0)
            {
                // We want to use the average velocity over the whole object when awarding differences, not the individual jump and slider path velocities.
                prevVelocity = (osuPrevObj0.LazyJumpDistance + osuPrevObj1.TravelDistance) / osuPrevObj0.StrainTime;
                currVelocity = (osuCurrObj.LazyJumpDistance + osuPrevObj0.TravelDistance) / osuCurrObj.StrainTime;

                // Scale with ratio of difference compared to 0.5 * max dist.
                double distRatio = Math.Pow(Math.Sin(Math.PI / 2 * Math.Abs(prevVelocity - currVelocity) / Math.Max(prevVelocity, currVelocity)), 2);

                // Reward for % distance up to 125 / strainTime for overlaps where velocity is still changing.
                double overlapVelocityBuff = Math.Min(diameter * 1.25 / Math.Min(osuCurrObj.StrainTime, osuPrevObj0.StrainTime), Math.Abs(prevVelocity - currVelocity));

                velocityChangeBonus = overlapVelocityBuff * distRatio;

                // Penalize for rhythm changes.
                velocityChangeBonus *= Math.Pow(Math.Min(osuCurrObj.StrainTime, osuPrevObj0.StrainTime) / Math.Max(osuCurrObj.StrainTime, osuPrevObj0.StrainTime), 2);
            }

            return Math.Max(acuteAngleBonus * 5.7, wideAngleBonus * 4.7 + velocityChangeBonus * 2.1);
        }

        private static double calcAcuteAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(140), double.DegreesToRadians(40));
        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(40), double.DegreesToRadians(140));
    }
}
