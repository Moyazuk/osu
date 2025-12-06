// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
namespace osu.Game.Rulesets.Osu.Difficulty
{
    public record OsuDifficultyTuning
    {
        public static OsuDifficultyTuning Default { get; } = new OsuDifficultyTuning();

public double RhythmPrevSame { get; init; }           = 0.18204614332420946;
public double RhythmPrev_4over3 { get; init; }        = 1.7477937905038308;
public double RhythmPrev_3over2 { get; init; }        = 0.22073379081703295;
public double RhythmPrev_5over3 { get; init; }        = 0.9164905159056068;
public double RhythmPrev_2over1 { get; init; }        = 0.5214479927422407;
public double RhythmPrev_5over2 { get; init; }        = 9.082718470263325;
public double RhythmPrev_3over1 { get; init; }        = 0.11756607295348734;
public double RhythmPrev_4over1 { get; init; }        = 0.258795970739696;

public double RhythmNextSame { get; init; }           = 0.025137167637841948;
public double RhythmNext_4over3 { get; init; }        = 1.0698682465105485;
public double RhythmNext_3over2 { get; init; }        = 0.535264520024332;
public double RhythmNext_5over3 { get; init; }        = 0.19721491195776125;
public double RhythmNext_2over1 { get; init; }        = 0.09107399667207179;
public double RhythmNext_5over2 { get; init; }        = 0.8035457795771961;
public double RhythmNext_3over1 { get; init; }        = 0.4253593856656883;
public double RhythmNext_4over1 { get; init; }        = 0.3592376127847374;

public double FingerControl_Skill_Multiplier { get; init; } = 26.98320592872538;
public double Speed_Burst_Multiplier { get; init; }         = 3.689614190831363;

// Previous-2 multipliers
public double RhythmPrev2Same { get; init; }        = 1.9946438975783503;
public double RhythmPrev2_4over3 { get; init; }     = 0.9600843756553028;
public double RhythmPrev2_3over2 { get; init; }     = 1.016736655939538;
public double RhythmPrev2_5over3 { get; init; }     = 0.9580311573975129;
public double RhythmPrev2_2over1 { get; init; }     = 0.8761278331033272;
public double RhythmPrev2_5over2 { get; init; }     = 0.790782060674392;
public double RhythmPrev2_3over1 { get; init; }     = 1.1558440725878187;
public double RhythmPrev2_4over1 { get; init; }     = 0.9807703617611822;

// Next-2 multipliers
public double RhythmNext2Same { get; init; }        = 1.5439727021956822;
public double RhythmNext2_4over3 { get; init; }     = 0.6389635396666126;
public double RhythmNext2_3over2 { get; init; }     = 1.1612574242241256;
public double RhythmNext2_5over3 { get; init; }     = 0.7479085833446782;
public double RhythmNext2_2over1 { get; init; }     = 0.6807449339801217;
public double RhythmNext2_5over2 { get; init; }     = 1.8028649715014315;
public double RhythmNext2_3over1 { get; init; }     = 0.9148896215868557;
public double RhythmNext2_4over1 { get; init; }     = 3.231086736136281;

public double Uniqueness_Divisor { get; init; }     = 8;

public double Uniqueness_Exponent { get; init; }     = 4;




    }
}
