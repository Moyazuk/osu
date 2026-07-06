// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed
{
    public static class StaminaEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;

            double bpmBonus = 0.0;

            double bigBpmBonus = 0.0;

            if (DiffUtils.MillisecondsToBPM(osuCurrObj.AdjustedDeltaTime) > 240)
                bpmBonus = Math.Pow((DiffUtils.BPMToMilliseconds(240) - osuCurrObj.AdjustedDeltaTime) / 30, 1.5);

            if (DiffUtils.MillisecondsToBPM(osuCurrObj.AdjustedDeltaTime) > 290)
                bigBpmBonus = Math.Pow((DiffUtils.BPMToMilliseconds(290) - osuCurrObj.AdjustedDeltaTime) / 20, 1);

            double finalValue = (1 + bigBpmBonus) * 1000 / osuCurrObj.AdjustedDeltaTime;

            double doubleTapFeasibility = 1.0 - osuCurrObj.CalculateDoubleTapFeasibility((OsuDifficultyHitObject?)osuCurrObj.Next(0));

            return finalValue * doubleTapFeasibility;
        }
    }
}
