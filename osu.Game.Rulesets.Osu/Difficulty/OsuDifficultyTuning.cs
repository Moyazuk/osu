// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public record OsuDifficultyTuning
    {
        public static OsuDifficultyTuning Default { get; } = new OsuDifficultyTuning();

        /// <summary>
        /// Scales the aim skill difficulty output.
        /// </summary>
        public double AimSkillDifficultyScale { get; init; } = 162;

        /// <summary>
        /// Scales the speed skill difficulty output.
        /// </summary>
        public double SpeedSkillDifficultyScale { get; init; } = 0.95;

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
        public double TotalPerformanceScale { get; init; } = 1.14;

        public double AimPerformanceExponent { get; init; } = 1.1;
        public double SpeedPerformanceExponent { get; init; } = 1.1;
        public double AccuracyPerformanceExponent { get; init; } = 1.1;
        public double FlashlightPerformanceExponent { get; init; } = 1.1;

        public double TotalPerformanceExponent { get; init; } = 1.0 / 1.1;

        public double AimWideAngleBonusScale { get; init; } = 1300.0;

        public double AimVelocityChangeBonusScale { get; init; } = 0.35;

        public double AimSliderBonusScale { get; init; } = 0.3;

        public double FlowAimWiggleBonusScale { get; init; } = 1200.0;

        public double FlowVelocityChangeBonusScale { get; init; } = 4.0;

        public double FlowSliderBonusScale { get; init; } = 0.3;

        public double FlowOverallEvaluatorScale { get; init; } = 0.875;

        public double FlowAnglularVelocityScale { get; init; } = 0.05;

        public double FlowDistanceExponent { get; init; } = 1.1;

        public double FlowExponentialAngleScaling { get; init; } = 0.35;

        public double AgilityBaseBPM { get; init; } = 240.0;

        public double AgilityExponent { get; init; } = 4.0;

        public double AgilityOverallMultiplier { get; init; } = 0.0125;

        public double AgilityVelocityChangeMultiplier { get; init; } = 0.1;

        public double AgilityDistanceBonusMultiplier { get; init; } = 0.00000000175;

        public double AgilityAngleBonusMultiplier { get; init; } = 0.65;


    }
}
