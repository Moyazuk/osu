// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public record OsuDifficultyTuning
    {
        public static OsuDifficultyTuning Default { get; } = new OsuDifficultyTuning();

        public double AimPerformanceScale { get; init; } = 0.9908482591569793;
        public double SpeedPerformanceScale { get; init; } = 0.9203745866050772;
        public double AccuracyPerformanceScale { get; init; } = 1.0082007218433866;
        public double FlashlightPerformanceScale { get; init; } = 1.0;
        public double TotalPerformanceScale { get; init; } = 1.0038706194956604;

        public double AimSkillMultiplier { get; init; } = 161.36223340053525;
        public double AimSnapDifficultyScale { get; init; } = 0.9880152365659264;
        public double AimFlowDifficultyScale { get; init; } = 1.0056361584220896;
        public double AimAgilityDifficultyScale { get; init; } = 1.0033726383531625;
        public double AimStrainDecayBase { get; init; } = 0.14650031484809722;
        public double AimAgilityStrainDecayBase { get; init; } = 0.8418533482609462;
        public double AimBackwardsStrainInfluence { get; init; } = 993.1901222198533;
        public double AimFlowStrainMultiplier { get; init; } = 1.2614349699223022;
        public double AimSnapStrainMultiplier { get; init; } = 4.21898150275772;
        public double AimFlowTransitionScale { get; init; } = 1.0092938045190654;
        public double AimSnapTransitionScale { get; init; } = 0.9991916862820013;

        public double FlowDistanceExponent { get; init; } = 0.9990365423372973;
        public double FlowJerkScale { get; init; } = 0.025031046966124553;
        public double FlowAngularVelocityScale { get; init; } = 25.29832019213616;
        public double FlowVelocityChangeScale { get; init; } = 1.0031017291188102;
        public double FlowSliderBonusScale { get; init; } = 0.3019225126649998;
        public double FlowOverallScale { get; init; } = 2.1841320216100897;
        public double FlowMaxAngleRadians { get; init; } = 2.973519710902897;
        public double FlowOverlapNerfMax { get; init; } = 0.049377845539107144;
        public double FlowVelocityBonusScale { get; init; } = 0.10062660777843487;
        public double FlowVelocityBonusExponent { get; init; } = 1.0086070334489086;
        public double FlowJerkDistanceThreshold { get; init; } = 4.99430621504257;
        public double FlowJerkDistanceScale { get; init; } = 5.040237241522457;

        public double SnapVelocityChangeBonusScale { get; init; } = 0.9951358450858717;
        public double SnapVelocityChangePenaltyExponent { get; init; } = 2.0264647223103647;
        public double SnapSliderBonusScale { get; init; } = 0.2985196609090063;
        public double SnapWideAngleBonusScale { get; init; } = 652.7162441204558;
        public double SnapAngleRepeatBaseScale { get; init; } = 0.14952322292276657;
        public double SnapAngleRepeatVectorScale { get; init; } = 0.9381339068840214;
        public double SnapAngleRepeatExponent { get; init; } = 1.9925947578915775;
        public double SnapWideAngleTimeExponent { get; init; } = 1.511049057978587;
        public double SnapWideAngleBackAndForthScale { get; init; } = 0.35149913145848544;
        public double SnapOverlapVelocityScale { get; init; } = 1.2405024221844385;
        public double SnapWideAngleMinRadians { get; init; } = 0.6974822265500067;
        public double SnapWideAngleMaxRadians { get; init; } = 2.442622884905144;
        public double SnapAngleRepeatSmootherstepHighRadians { get; init; } = 1.586170334343379;
        public double SnapAngleRepeatSmootherstepLowRadians { get; init; } = 0.5278935779677786;

        public double AgilityBaseBpm { get; init; } = 239.9386654907725;
        public double AgilityExponent { get; init; } = 3.9990377266128716;
        public double AgilityCheesabilityTimeScale { get; init; } = 7.979450748453583;
        public double AgilityAngleBonusScale { get; init; } = 0.6432915318396804;
        public double AgilityAngleRepeatBaseScale { get; init; } = 0.30057783652451486;
        public double AgilityAngleRepeatVectorScale { get; init; } = 0.9545144193175182;
        public double AgilityAngleRepeatExponent { get; init; } = 1.9694753835241083;
        public double AgilityOverallScale { get; init; } = 0.02562974677680281;

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

        public int RhythmHistoryTimeMax { get; init; } = 5037;
        public int RhythmHistoryObjectsMax { get; init; } = 32;
        public double RhythmOverallScale { get; init; } = 1.2694168931216885;
        public double RhythmRatioScale { get; init; } = 15.132288421763583;
        public double RhythmDeltaDifferenceEpsilonMultiplier { get; init; } = 0.3005438694516366;
        public double RhythmRatioCap { get; init; } = 0.5059570346227311;
        public double RhythmDifferenceMultiplierBase { get; init; } = 2.011505490315272;
        public double RhythmDifferenceMultiplierScale { get; init; } = 8.06749778712925;
        public double RhythmSliderChangeScale { get; init; } = 0.1324657737473568;
        public double RhythmPrevSliderChangeScale { get; init; } = 0.30268409563632837;
        public double RhythmRepeatedPolarityScale { get; init; } = 0.5038218158263353;
        public double RhythmSpeedUpSlowDownScale { get; init; } = 0.12573004299866103;
        public double RhythmRepeatedIslandScale { get; init; } = 0.5027442090882261;
        public double RhythmIslandRepeatLimit { get; init; } = 3.0191737405066728;
        public double RhythmIslandPowerMaxValue { get; init; } = 2.7497780666161846;
        public double RhythmIslandPowerMultiplier { get; init; } = 0.2406369829140169;
        public double RhythmIslandPowerMidpointOffset { get; init; } = 58.81553408830218;
        public double RhythmDoubletapnessScale { get; init; } = 0.753949619834168;
        public double RhythmSpeedUpSliderScale { get; init; } = 0.6076693241999842;

        public double SpeedSkillMultiplier { get; init; } = 0.9641895179406436;
        public double SpeedStrainDecayBase { get; init; } = 0.30425433872350177;
        public double SpeedMinBonusBpm { get; init; } = 196.0004841322914;
        public double SpeedBalancingFactor { get; init; } = 36.6828934363201;
        public double SpeedBonusScale { get; init; } = 0.7668965610023273;
        public double SpeedBonusExponent { get; init; } = 2.02672901655492;
        public double SpeedBaseScale { get; init; } = 1015.0478052734637;
    }
}
