// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim
{
    public static class FlowAimEvaluator
    {
        /// <summary>
        /// Evaluates difficulty of "flow aim" - aiming pattern where player doesn't stop their cursor on every object and instead "flows" through them.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance, double previousPFlow)
        {
            if (current.Index <= 1 || current.BaseObject is Spinner)
                return 0;

            var osuNextObj = (OsuDifficultyHitObject?)current.Next();
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject)current.Previous();

            var osuLastLastObj = (OsuDifficultyHitObject)current.Previous(1);

            double currDistance = withSliderTravelDistance ? osuCurrObj.LazyJumpDistance : osuCurrObj.JumpDistance;
            double prevDistance = withSliderTravelDistance ? osuLastObj.LazyJumpDistance : osuLastObj.JumpDistance;

            double currVelocity = calculateCurrentVelocity(
                osuCurrObj,
                osuLastObj,
                currDistance,
                withSliderTravelDistance);

            double prevVelocity = prevDistance / osuLastObj.AdjustedDeltaTime;

            double flowContinuationDifficulty = currVelocity;
            double flowTransitionDifficulty = currVelocity;

            // Apply high circle size bonus to the base velocity.
            // We use reduced CS bonus here because the bonus was made for an evaluator with a different d/t scaling
            flowContinuationDifficulty *= DiffUtils.Pow(osuCurrObj.SmallCircleBonus, 0.55);
            flowTransitionDifficulty *= DiffUtils.Pow(osuCurrObj.SmallCircleBonus, 0.55);

            flowContinuationDifficulty *= calculateRhythmChangeBonus(osuCurrObj, osuLastObj);
            flowContinuationDifficulty *= calculateAngularVelocityBonus(osuCurrObj, osuLastObj);

            if (osuNextObj is not null)
            {
                flowTransitionDifficulty *= calculateNextAngularVelocityBonus(osuCurrObj, osuNextObj);
            }

            flowContinuationDifficulty += calculateAcuteAngleBonus(
                osuCurrObj,
                osuLastObj,
                osuLastLastObj,
                osuNextObj,
                currVelocity);

            flowTransitionDifficulty += calculateTransitionAcuteAngleBonus(
                osuCurrObj,
                osuLastObj,
                osuLastLastObj,
                osuNextObj,
                currVelocity);

            flowContinuationDifficulty += calculateVelocityChangeBonus(
                osuCurrObj,
                osuLastObj,
                currVelocity,
                prevVelocity,
                currDistance,
                calculateOverlapWeight(osuCurrObj, osuLastObj, osuLastLastObj),
                withSliderTravelDistance);

            if (osuNextObj is not null)
            {
                double nextDistance = withSliderTravelDistance ? osuNextObj.LazyJumpDistance : osuNextObj.JumpDistance;

                double nextVelocity = nextDistance / osuLastObj.AdjustedDeltaTime;

                flowTransitionDifficulty += calculateTransitionVelocityChangeBonus(
                    osuCurrObj,
                    osuLastObj,
                    currVelocity,
                    nextVelocity,
                    currDistance,
                    calculateOverlapWeight(osuCurrObj, osuLastObj, osuLastLastObj),
                    withSliderTravelDistance);
            }

            if (osuCurrObj.BaseObject is Slider && withSliderTravelDistance)
            {
                flowContinuationDifficulty += calculateSliderBonus(osuCurrObj);
                flowTransitionDifficulty += calculateSliderBonus(osuCurrObj);
            }

            flowTransitionDifficulty *= 1 - previousPFlow;

            flowContinuationDifficulty *= previousPFlow;

            double flowDifficulty = flowTransitionDifficulty + flowContinuationDifficulty;

            // Final velocity is being raised to a power because flow difficulty scales harder with both high distance and time, and we want to account for that
            flowDifficulty = DiffUtils.Pow(flowDifficulty, 1.45);

            // Reduce difficulty for low spacing since spacing below radius is always to be flowed
            return flowDifficulty * DiffUtils.Smootherstep(currDistance, 0, OsuDifficultyHitObject.NORMALISED_RADIUS);
        }

        private static double calculateRhythmChangeBonus(OsuDifficultyHitObject current, OsuDifficultyHitObject last)
        {
            const double maximum_rhythm_change_bonus = 0.1;

            double bonus = DiffUtils.Pow(
                (Math.Max(current.AdjustedDeltaTime, last.AdjustedDeltaTime) - Math.Min(current.AdjustedDeltaTime, last.AdjustedDeltaTime)) / 50,
                4);

            return 1 + Math.Min(maximum_rhythm_change_bonus, bonus);
        }

        /// <summary>
        /// Scales flow difficulty by angular velocity.
        /// This nerfs consistent angles whilst buffing "erratic" flow.
        /// </summary>
        private static double calculateAngularVelocityBonus(OsuDifficultyHitObject current, OsuDifficultyHitObject last)
        {
            if (current.Angle == null || last.Angle == null)
                return 1;

            double angleDifference = Math.Abs(current.Angle.Value - last.Angle.Value);
            double angleDifferenceAdjusted = Math.Sin(angleDifference / 2) * 180.0;
            double angularVelocity = angleDifferenceAdjusted / (current.AdjustedDeltaTime * 0.1);

            return 0.8 + Math.Sqrt(angularVelocity / 180.0);
        }

        /// <summary>
        /// Scales flow difficulty by angular velocity.
        /// This nerfs consistent angles whilst buffing "erratic" flow.
        /// </summary>
        private static double calculateNextAngularVelocityBonus(OsuDifficultyHitObject current, OsuDifficultyHitObject next)
        {
            if (current.Angle == null || next.Angle == null)
                return 1;

            double angleDifference = Math.Abs(current.Angle.Value - next.Angle.Value);
            double angleDifferenceAdjusted = Math.Sin(angleDifference / 2) * 180.0;
            double angularVelocity = angleDifferenceAdjusted / (current.AdjustedDeltaTime * 0.1);

            return 0.8 + Math.Sqrt(angularVelocity / 180.0);
        }

        private static double calculateAcuteAngleBonus(
            OsuDifficultyHitObject current,
            OsuDifficultyHitObject last,
            OsuDifficultyHitObject lastLast,
            OsuDifficultyHitObject? next,
            double currVelocity)
        {
            const double acute_angle_multiplier = 0.95;

            if (current.Angle == null)
                return 0;

            double currAcuteness = AngleUtils.CalculateAcuteness(current.Angle.Value);

            double overlapWeight = calculateOverlapWeight(current, last, lastLast);

            return currVelocity * currAcuteness * overlapWeight * acute_angle_multiplier;
        }

        private static double calculateTransitionAcuteAngleBonus(
            OsuDifficultyHitObject current,
            OsuDifficultyHitObject last,
            OsuDifficultyHitObject lastLast,
            OsuDifficultyHitObject? next,
            double currVelocity)
        {
            const double acute_angle_multiplier = 0.9;

            if (next?.Angle == null)
                return 0;

            double currAcuteness = AngleUtils.CalculateAcuteness(next.Angle.Value);

            double overlapWeight = calculateOverlapWeight(next, current, last);

            return currVelocity * currAcuteness * overlapWeight * acute_angle_multiplier;
        }

        private static double calculateVelocityChangeBonus(
            OsuDifficultyHitObject current,
            OsuDifficultyHitObject previous,
            double currVelocity,
            double prevVelocity,
            double currDistance,
            double overlappedNotesWeight,
            bool withSliderTravelDistance)
        {
            const double velocity_change_multiplier = 1.9;

            if (Math.Max(prevVelocity, currVelocity) == 0)
                return 0;

            if (withSliderTravelDistance)
                currVelocity = currDistance / current.AdjustedDeltaTime;

            double velocityChangeBonus = Math.Pow(Math.Abs(prevVelocity - currVelocity), 1);

            velocityChangeBonus *= DiffUtils.Pow(Math.Min(current.AdjustedDeltaTime, previous.AdjustedDeltaTime) / Math.Max(current.AdjustedDeltaTime, previous.AdjustedDeltaTime), 3);

            return velocityChangeBonus *
                   overlappedNotesWeight *
                   velocity_change_multiplier;
        }

        private static double calculateTransitionVelocityChangeBonus(
            OsuDifficultyHitObject current,
            OsuDifficultyHitObject previous,
            double currVelocity,
            double nextVelocity,
            double currDistance,
            double overlappedNotesWeight,
            bool withSliderTravelDistance)
        {
            const double velocity_change_multiplier = 1.9;

            if (Math.Max(currVelocity, nextVelocity) == 0)
                return 0;

            if (withSliderTravelDistance)
                currVelocity = currDistance / current.AdjustedDeltaTime;

            double velocityChangeBonus = Math.Pow(Math.Abs(nextVelocity - currVelocity), 1);

            velocityChangeBonus *= DiffUtils.Pow(Math.Min(current.AdjustedDeltaTime, previous.AdjustedDeltaTime) / Math.Max(current.AdjustedDeltaTime, previous.AdjustedDeltaTime), 3);

            return velocityChangeBonus *
                   overlappedNotesWeight *
                   velocity_change_multiplier;
        }

        private static double calculateSliderBonus(OsuDifficultyHitObject current)
            => current.TravelDistance / current.TravelTime;

        private static double calculateCurrentVelocity(OsuDifficultyHitObject current, OsuDifficultyHitObject previous, double currDistance, bool withSliderTravelDistance)
        {
            double currVelocity = currDistance / current.AdjustedDeltaTime;

            // If the last object is a slider, then we extend the travel velocity through the slider into the current object.
            if (previous.BaseObject is Slider && withSliderTravelDistance)
            {
                double sliderDistance = previous.LazyTravelDistance + current.LazyJumpDistance;
                currVelocity = Math.Max(currVelocity, sliderDistance / current.AdjustedDeltaTime);
            }

            return currVelocity;
        }

        private static double calculateOverlapWeight(OsuDifficultyHitObject current, OsuDifficultyHitObject previous, OsuDifficultyHitObject lastLast)
        {
            double o1 = calculateOverlapFactor(current, previous);
            double o2 = calculateOverlapFactor(current, lastLast);
            double o3 = calculateOverlapFactor(previous, lastLast);

            return 1 - o1 * o2 * o3;
        }

        private static double calculateOverlapFactor(OsuDifficultyHitObject first, OsuDifficultyHitObject second)
        {
            var firstBase = (OsuHitObject)first.BaseObject;
            var secondBase = (OsuHitObject)second.BaseObject;
            double objectRadius = firstBase.Radius;

            double distance = Vector2.Distance(firstBase.StackedPosition, secondBase.StackedPosition);
            return Math.Clamp(1 - DiffUtils.Pow(Math.Max(distance - objectRadius, 0) / objectRadius, 2), 0, 1);
        }
    }
}
