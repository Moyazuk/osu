// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public record OsuDifficultyTuning
    {
        public static OsuDifficultyTuning Default { get; } = new OsuDifficultyTuning();

        /// <summary>
        /// Scales the aim skill difficulty output.
        /// </summary>
        public double AimSkillDifficultyScale { get; init; } = 1.0;

        /// <summary>
        /// Scales the speed skill difficulty output.
        /// </summary>
        public double SpeedSkillDifficultyScale { get; init; } = 1.0;

        /// <summary>
        /// Scales the flashlight skill difficulty output.
        /// </summary>
        public double FlashlightSkillDifficultyScale { get; init; } = 1.0;

        /// <summary>
        /// Scales the aim component of performance.
        /// </summary>
        public double AimPerformanceScale { get; init; } = 1.0;

        /// <summary>
        /// Scales the speed component of performance.
        /// </summary>
        public double SpeedPerformanceScale { get; init; } = 1.0;

        /// <summary>
        /// Scales the accuracy component of performance.
        /// </summary>
        public double AccuracyPerformanceScale { get; init; } = 1.0;

        /// <summary>
        /// Scales the flashlight component of performance.
        /// </summary>
        public double FlashlightPerformanceScale { get; init; } = 1.0;

        /// <summary>
        /// Scales the combined performance value.
        /// </summary>
        public double TotalPerformanceScale { get; init; } = 1.0;

        /// <summary>
        /// Scales the wide angle bonus applied during aim evaluation.
        /// </summary>
        public double AimWideAngleBonusScale { get; init; } = 1.5;

        /// <summary>
        /// Scales the acute angle bonus applied during aim evaluation.
        /// </summary>
        public double AimAcuteAngleBonusScale { get; init; } = 2.55;

        /// <summary>
        /// Scales the slider bonus applied during aim evaluation when slider travel distance is considered.
        /// </summary>
        public double AimSliderBonusScale { get; init; } = 1.35;

        /// <summary>
        /// Scales the velocity change bonus applied during aim evaluation.
        /// </summary>
        public double AimVelocityChangeBonusScale { get; init; } = 0.75;

        /// <summary>
        /// Scales the wiggle bonus applied during aim evaluation.
        /// </summary>
        public double AimWiggleBonusScale { get; init; } = 1.02;

        /// <summary>
        /// Minimum BPM threshold before awarding additional speed bonus.
        /// </summary>
        public double SpeedMinSpeedBonusBpm { get; init; } = 200;

        /// <summary>
        /// Balancing factor applied when scaling speed bonus with BPM.
        /// </summary>
        public double SpeedBalancingFactor { get; init; } = 40;

        /// <summary>
        /// Maximum effective distance when computing speed bonus.
        /// </summary>
        public double SpeedSingleSpacingThreshold { get; init; } = OsuDifficultyHitObject.NORMALISED_DIAMETER * 1.25;

        /// <summary>
        /// Multiplier applied to the distance bonus component of speed evaluation.
        /// </summary>
        public double SpeedDistanceBonusScale { get; init; } = 0.8;

        /// <summary>
        /// Maximum time range in milliseconds for rhythm history consideration.
        /// </summary>
        public double RhythmHistoryTimeMax { get; init; } = 5 * 1000;

        /// <summary>
        /// Maximum number of historical objects considered for rhythm calculation.
        /// </summary>
        public int RhythmHistoryObjectsMax { get; init; } = 32;

        /// <summary>
        /// Multiplier applied to the final rhythm difficulty value.
        /// </summary>
        public double RhythmOverallScale { get; init; } = 1.0;

        /// <summary>
        /// Multiplier applied to rhythm ratio calculations.
        /// </summary>
        public double RhythmRatioMultiplier { get; init; } = 15.0;
    }
}
