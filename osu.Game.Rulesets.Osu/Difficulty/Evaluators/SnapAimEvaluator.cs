// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;
using static osu.Game.Rulesets.Osu.Difficulty.Preprocessing.OsuDifficultyHitObject;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class SnapAimEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            // Base snap difficulty is velocity.
            double difficulty = EvaluateDistanceBonus(current, withSliderTravelDistance) * 263;
            double sliderBonus = 0;
            //difficulty += EvaluateAgilityBonus(current) * 65;
            difficulty += EvaluateAngleBonus(current) * 263;
            difficulty += EvaluateVelocityChangeBonus(current) * 460;

            var osuPrevObj = (OsuDifficultyHitObject)current;

            if (osuPrevObj.BaseObject is Slider && withSliderTravelDistance)
            {
                // Reward sliders based on velocity.
                sliderBonus = osuPrevObj.TravelDistance / osuPrevObj.TravelTime;
            }

            // Add in additional slider velocity bonus.
            if (withSliderTravelDistance)
                difficulty += sliderBonus * 60;

            return difficulty;
        }

        public static double EvaluateDistanceBonus(DifficultyHitObject current, bool withSliderTravelDistance)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current;

            // Base snap difficulty is velocity.
            double distanceBonus = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;

            // But if the last object is a slider, then we extend the travel velocity through the slider into the current object.
            if (osuPrevObj.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = osuPrevObj.TravelDistance / osuPrevObj.TravelTime; // calculate the slider velocity from slider head to slider end.
                double movementVelocity = osuCurrObj.MinimumJumpDistance / osuCurrObj.MinimumJumpTime; // calculate the movement velocity from slider end to current object

                distanceBonus = Math.Max(distanceBonus, movementVelocity + travelVelocity); // take the larger total combined velocity.
            }

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
            double baseBpm = 240.0 / (1 + 0.15 * Smootherstep(currentAngle, 0, 120) * currDistanceMultiplier * prevDistanceMultiplier);

            // Agility bonus of 1 at base BPM.
            double agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, 4) - 1);

            return agilityBonus * 0.15;
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
            double prevDistanceMultiplier = Smootherstep(osuPrevObj.RawMovement.Length / osuPrevObj.Radius, 0, 0.25);

            // We also scale angle bonus by the difference in velocity from prevPrev -> prev and prev -> current. This addresses cut stream patterns.
            prevDistanceMultiplier *= Math.Pow((currVelocity > 0 ? Math.Min(1, prevVelocity * 1.4 / currVelocity) : 1), 1);

            double angleBonus = Smootherstep(currAngle, 0, 180) * currVelocity * prevDistanceMultiplier; // Gengaozo pattern

            double distanceScaling = 0.4 + 0.6 * Smootherstep(osuCurrObj.RawMovement.Length / osuCurrObj.Radius, 0.0, 8);

            return angleBonus * distanceScaling;
        }

        public static double EvaluateVelocityChangeBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 3))
                return 0;

            OsuDifficultyHitObject osuCurrObj = (OsuDifficultyHitObject)current;
            OsuDifficultyHitObject osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);
            OsuDifficultyHitObject osuPrevObj1 = (OsuDifficultyHitObject)current.Previous(1);

            double currVelocity = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;
            double prevVelocity = osuPrevObj.Movement.Length / osuPrevObj.StrainTime;

            double diameter = osuCurrObj.Radius * 2;

            double velChangeBonus = 0;

            if (Math.Max(prevVelocity, currVelocity) != 0)
            {

                // Scale with ratio of difference compared to 0.5 * max dist.
                double distRatio = Math.Pow(Math.Sin(Math.PI / 2 * Math.Abs(prevVelocity - currVelocity) / Math.Max(prevVelocity, currVelocity)), 2);

                // Reward for % distance up to 125 / strainTime for overlaps where velocity is still changing.
                double overlapVelocityBuff = Math.Min(diameter * 1.25 / Math.Min(osuCurrObj.StrainTime, osuPrevObj.StrainTime), Math.Abs(prevVelocity - currVelocity));

                velChangeBonus = overlapVelocityBuff * distRatio;

                // Penalize for rhythm changes.
                velChangeBonus *= Math.Pow(Math.Min(osuCurrObj.StrainTime, osuPrevObj.StrainTime) / Math.Max(osuCurrObj.StrainTime, osuPrevObj.StrainTime), 2);
            }

            return velChangeBonus;
        }
    }
}
