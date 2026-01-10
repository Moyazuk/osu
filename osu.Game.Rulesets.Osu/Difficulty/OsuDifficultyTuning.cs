// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public record OsuDifficultyTuning
    {
        public static OsuDifficultyTuning Default { get; } = new OsuDifficultyTuning();
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

        public double AimSkillStrainScale { get; init; } = 1.0;
        public double SpeedSkillStrainScale { get; init; } = 1.0;
        public double FlashlightSkillStrainScale { get; init; } = 1.0;

        public double AimWideAngleBonusScale { get; init; } = 1.5;
        public double AimAcuteAngleScale { get; init; } = 2.55;
        public double AimSliderBonusScale { get; init; } = 1.35;
        public double AimVelocityChangeBonusScale { get; init; } = 0.75;
        public double AimWiggleBonusScale { get; init; } = 1.02;

        public double RhythmPrevSame            { get; init; } = 0.3259437360425038;
        public double RhythmPrev_4over3         { get; init; } = 0.001274441740005855;
        public double RhythmPrev_3over2         { get; init; } = 0.21819343740210662;
        public double RhythmPrev_5over3         { get; init; } = 0.4377158166601563;
        public double RhythmPrev_2over1         { get; init; } = 0.5780649411877421;
        public double RhythmPrev_5over2         { get; init; } = 8.072146795521881;
        public double RhythmPrev_3over1         { get; init; } = 0.11214497037882809;
        public double RhythmPrev_4over1         { get; init; } = 0.10696043663502434;

        public double RhythmNextSame            { get; init; } = 0.022977969025782585;
        public double RhythmNext_4over3         { get; init; } = 1.745530417366166;
        public double RhythmNext_3over2         { get; init; } = 0.33011421571569005;
        public double RhythmNext_5over3         { get; init; } = 0;
        public double RhythmNext_2over1         { get; init; } = 0.06398664563904001;
        public double RhythmNext_5over2         { get; init; } = 0.34849431501616074;
        public double RhythmNext_3over1         { get; init; } = 0.00112237196924423;
        public double RhythmNext_4over1         { get; init; } = 0.0011115603186338896;


        public double RhythmPrev2Same           { get; init; } = 7.334737127866351;
        public double RhythmPrev2_4over3        { get; init; } = 0.997088943909741;
        public double RhythmPrev2_3over2        { get; init; } = 1.9765346106114774;
        public double RhythmPrev2_5over3        { get; init; } = 1.066581892059917;
        public double RhythmPrev2_2over1        { get; init; } = 0.6697916732180355;
        public double RhythmPrev2_5over2        { get; init; } = 0.361446711627019;
        public double RhythmPrev2_3over1        { get; init; } = 0.002242173886498486;
        public double RhythmPrev2_4over1        { get; init; } = 0.0012950415027351164;

        public double RhythmNext2Same           { get; init; } = 3.3158387138829446;
        public double RhythmNext2_4over3        { get; init; } = 0.3031390859734716;
        public double RhythmNext2_3over2        { get; init; } = 1.5788511998411576;
        public double RhythmNext2_5over3        { get; init; } = 0.471148521081547;
        public double RhythmNext2_2over1        { get; init; } = 0.5602841202404043;
        public double RhythmNext2_5over2        { get; init; } = 1.262941837545127;
        public double RhythmNext2_3over1        { get; init; } = 0.0014987653891473459;
        public double RhythmNext2_4over1        { get; init; } = 0.001000003387394558;

        public double Uniqueness_Divisor        { get; init; } = 19.754227669560123;
        public double Uniqueness_Exponent       { get; init; } = 2.4582656313645748;



        public double SpeedSingleSpacingThreshold { get; init; } = 125;
        public double SpeedMinBonusBpm { get; init; } = 200;
        public double SpeedBalancingFactor { get; init; } = 40;
        public double SpeedDistanceScale { get; init; } = 0.8;

        public double BurstMultiplier { get; init; } = 2.55;

        public double StreamMultiplier { get; init; } = 0.010;

        public double StaminaMultiplier { get; init; } = 0.0825;

        public double SpeedPowerMeanExponent { get; init; } = 1.5;
        public double StrainDecayBurst { get; init; } = 0.05;

        public double StrainDecayStream { get; init; } = 0.01;

        public double StrainDecayStreamExponent { get; init; } = 1.6;

        public double StrainDecayStamina { get; init; } = 0.001;

        public double StrainDecayStaminaExponent { get; init; } = 3.5;

        public double StrainDecayStaminaChange { get; init; } = 25;
    }
}
