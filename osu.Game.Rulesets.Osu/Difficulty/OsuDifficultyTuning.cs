// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
namespace osu.Game.Rulesets.Osu.Difficulty
{
    public record OsuDifficultyTuning
    {
        public static OsuDifficultyTuning Default { get; } = new OsuDifficultyTuning();

        // === Rhythm ratio multipliers (previous-object-based) ===
        // These correspond to the original table:
        // (1.0, 0.01), (4/3, 2.0), (1.5, 1.5), (5/3, 3.0),
        // (2.0, 0.1), (2.5, 1.2), (3.0, 0.25), (4.0, 0.0)

        public double RhythmPrevSame { get; init; }           = 0.04410736050592817;
        public double RhythmPrev_4over3 { get; init; }        = 1.1554317668289367;
        public double RhythmPrev_3over2 { get; init; }        = 0.22111510141459123;
        public double RhythmPrev_5over3 { get; init; }        = 0.7766724977827884;
        public double RhythmPrev_2over1 { get; init; }        = 0.5079059671437692;
        public double RhythmPrev_5over2 { get; init; }        = 0.3637312935612961;
        public double RhythmPrev_3over1 { get; init; }        = 0.07196699765215764;
        public double RhythmPrev_4over1 { get; init; }        = 0.8155013645285654;

        // === Rhythm ratio multipliers (next-object-based) ===
        // You can start with the same defaults, or something different.
        public double RhythmNextSame { get; init; }           = 0.004230316977379918;
        public double RhythmNext_4over3 { get; init; }        = 8.154977245900707;
        public double RhythmNext_3over2 { get; init; }        = 4.639184597073296;
        public double RhythmNext_5over3 { get; init; }        = 0.7309951897984441;
        public double RhythmNext_2over1 { get; init; }        = 0.026198136902901376;
        public double RhythmNext_5over2 { get; init; }        = 0.41843893959907785;
        public double RhythmNext_3over1 { get; init; }        = 0.8060051172545362;
        public double RhythmNext_4over1 { get; init; }        = 0.001001941459829822;

        public double RhythmOverallScale { get; init; }        = 0.9516785397282094;
    }
}
