// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public record OsuDifficultyTuning
    {
        public static OsuDifficultyTuning Default { get; } = new OsuDifficultyTuning();

        public double AimPerformanceScale { get; init; } = 1.0;
        public double SpeedPerformanceScale { get; init; } = 1.0;
        public double AccuracyPerformanceScale { get; init; } = 1.0;
        public double FlashlightPerformanceScale { get; init; } = 1.0;
        public double TotalPerformanceScale { get; init; } = 1.0;

        public double AimSkillMultiplier { get; init; } = 162.0;
        public double AimSnapDifficultyScale { get; init; } = 1.0;
        public double AimFlowDifficultyScale { get; init; } = 1.0;
        public double AimAgilityDifficultyScale { get; init; } = 1.0;
        public double AimStrainDecayBase { get; init; } = 0.15;
        public double AimAgilityStrainDecayBase { get; init; } = 0.85;
        public double AimBackwardsStrainInfluence { get; init; } = 1000.0;
        public double AimFlowStrainMultiplier { get; init; } = 1.25;
        public double AimSnapStrainMultiplier { get; init; } = 4.25;
        public double AimFlowTransitionScale { get; init; } = 1.0;
        public double AimSnapTransitionScale { get; init; } = 1.0;

        public double FlowDistanceExponent { get; init; } = 1.0;
        public double FlowJerkScale { get; init; } = 0.025;
        public double FlowAngularVelocityScale { get; init; } = 25.0;
        public double FlowVelocityChangeScale { get; init; } = 1.0;
        public double FlowSliderBonusScale { get; init; } = 0.3;
        public double FlowOverallScale { get; init; } = 2.175;
        public double FlowMaxAngleRadians { get; init; } = Math.PI * 170.0 / 180.0;
        public double FlowOverlapNerfMax { get; init; } = 0.05;
        public double FlowVelocityBonusScale { get; init; } = 0.1;
        public double FlowVelocityBonusExponent { get; init; } = 1.0;
        public double FlowJerkDistanceThreshold { get; init; } = 5.0;
        public double FlowJerkDistanceScale { get; init; } = 5.0;

        public double SnapVelocityChangeBonusScale { get; init; } = 1.0;
        public double SnapVelocityChangePenaltyExponent { get; init; } = 2.0;
        public double SnapSliderBonusScale { get; init; } = 0.3;
        public double SnapWideAngleBonusScale { get; init; } = 650.0;
        public double SnapAngleRepeatBaseScale { get; init; } = 0.15;
        public double SnapAngleRepeatVectorScale { get; init; } = 0.95;
        public double SnapAngleRepeatExponent { get; init; } = 2.0;
        public double SnapWideAngleTimeExponent { get; init; } = 1.5;
        public double SnapWideAngleBackAndForthScale { get; init; } = 0.35;
        public double SnapOverlapVelocityScale { get; init; } = 1.25;
        public double SnapWideAngleMinRadians { get; init; } = Math.PI * 40.0 / 180.0;
        public double SnapWideAngleMaxRadians { get; init; } = Math.PI * 140.0 / 180.0;
        public double SnapAngleRepeatSmootherstepHighRadians { get; init; } = Math.PI * 90.0 / 180.0;
        public double SnapAngleRepeatSmootherstepLowRadians { get; init; } = Math.PI * 30.0 / 180.0;

        public double AgilityBaseBpm { get; init; } = 240.0;
        public double AgilityExponent { get; init; } = 4.0;
        public double AgilityCheesabilityTimeScale { get; init; } = 8.0;
        public double AgilityAngleBonusScale { get; init; } = 0.65;
        public double AgilityAngleRepeatBaseScale { get; init; } = 0.3;
        public double AgilityAngleRepeatVectorScale { get; init; } = 0.95;
        public double AgilityAngleRepeatExponent { get; init; } = 2.0;
        public double AgilityOverallScale { get; init; } = 0.0255;

        public double FlashlightSkillMultiplier { get; init; } = 0.05512;
        public double FlashlightStrainDecayBase { get; init; } = 0.15;
        public double FlashlightMaxOpacityBonusScale { get; init; } = 0.4;
        public double FlashlightHiddenBonusScale { get; init; } = 0.2;
        public double FlashlightMinVelocityScale { get; init; } = 0.5;
        public double FlashlightSliderBonusScale { get; init; } = 1.3;
        public double FlashlightMinAngleScale { get; init; } = 0.2;
        public int FlashlightHistoryLength { get; init; } = 10;
        public double FlashlightSmallDistanceNerfDistance { get; init; } = 75.0;
        public double FlashlightStackNerfDistance { get; init; } = 25.0;
        public double FlashlightResultExponent { get; init; } = 2.0;
        public double FlashlightAngleRepeatThreshold { get; init; } = 0.02;
        public double FlashlightAngleRepeatDecayScale { get; init; } = 0.1;
        public double FlashlightSliderVelocityExponent { get; init; } = 0.5;

        public int RhythmHistoryTimeMax { get; init; } = 5 * 1000;
        public int RhythmHistoryObjectsMax { get; init; } = 32;
        public double RhythmOverallScale { get; init; } = 1.25;
        public double RhythmRatioScale { get; init; } = 15.0;
        public double RhythmDeltaDifferenceEpsilonMultiplier { get; init; } = 0.3;
        public double RhythmRatioCap { get; init; } = 0.5;
        public double RhythmDifferenceMultiplierBase { get; init; } = 2.0;
        public double RhythmDifferenceMultiplierScale { get; init; } = 8.0;
        public double RhythmSliderChangeScale { get; init; } = 0.125;
        public double RhythmPrevSliderChangeScale { get; init; } = 0.3;
        public double RhythmRepeatedPolarityScale { get; init; } = 0.5;
        public double RhythmSpeedUpSlowDownScale { get; init; } = 0.125;
        public double RhythmRepeatedIslandScale { get; init; } = 0.5;
        public double RhythmIslandRepeatLimit { get; init; } = 3.0;
        public double RhythmIslandPowerMaxValue { get; init; } = 2.75;
        public double RhythmIslandPowerMultiplier { get; init; } = 0.24;
        public double RhythmIslandPowerMidpointOffset { get; init; } = 58.33;
        public double RhythmDoubletapnessScale { get; init; } = 0.75;
        public double RhythmSpeedUpSliderScale { get; init; } = 0.6;

        public double SpeedSkillMultiplier { get; init; } = 0.95;
        public double SpeedStrainDecayBase { get; init; } = 0.3;
        public double SpeedMinBonusBpm { get; init; } = 200;
        public double SpeedBalancingFactor { get; init; } = 40;
        public double SpeedBonusScale { get; init; } = 0.75;
        public double SpeedBonusExponent { get; init; } = 2.0;
        public double SpeedBaseScale { get; init; } = 1000;
    }
}
