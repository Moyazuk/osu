// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        private const double performance_base_multiplier = 1.14; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things.
        private const double difficulty_multiplier = 0.0675;
        private const double star_rating_multiplier = 0.0265;

        public override int Version => 20250306;

        private double mechanicalDifficultyRating;

        public OsuDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        public static double CalculateDifficultyMultiplier(Mod[] mods, int totalHits, int spinnerCount)
        {
            double multiplier = performance_base_multiplier;

            if (mods.Any(m => m is OsuModSpunOut) && totalHits > 0)
                multiplier *= 1.0 - Math.Pow((double)spinnerCount / totalHits, 0.85);

            return multiplier;
        }

        /// <summary>
        /// Calculates a visibility bonus that is applicable to Hidden and Traceable.
        /// </summary>
        public static double CalculateVisibilityBonus(Mod[] mods, double approachRate, double visibilityFactor = 1)
        {
            // NOTE: TC's effect is only noticeable in performance calculations until lazer mods are accounted for server-side.
            bool isAlwaysPartiallyVisible = mods.OfType<OsuModHidden>().Any(m => !m.OnlyFadeApproachCircles.Value) || mods.OfType<OsuModTraceable>().Any();

            // Start from normal curve, rewarding lower AR up to AR5
            double readingBonus = 0.04 * (12.0 - Math.Max(approachRate, 5));

            readingBonus *= visibilityFactor;

            // For AR up to 0 - reduce reward for very low ARs when object is visible
            if (approachRate < 5)
                readingBonus += (isAlwaysPartiallyVisible ? 0.04 : 0.03) * (5.0 - Math.Max(approachRate, 0));

            // Starting from AR0 - cap values so they won't grow to infinity
            if (approachRate < 0)
                readingBonus += (isAlwaysPartiallyVisible ? 0.1 : 0.075) * (1 - Math.Pow(1.5, approachRate));

            return readingBonus;
        }

        public static double CalculateRateAdjustedApproachRate(double approachRate, double clockRate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(approachRate, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN) / clockRate;
            return IBeatmapDifficultyInfo.InverseDifficultyRange(preempt, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN);
        }

        public static double CalculateRateAdjustedOverallDifficulty(double overallDifficulty, double clockRate)
        {
            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(overallDifficulty);

            double hitWindowGreat = hitWindows.WindowFor(HitResult.Great) / clockRate;

            return (79.5 - hitWindowGreat) / 6;
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods };

            var aim = skills.OfType<Aim>().Single(a => a.IncludeSliders && !a.WithCheesability);
            var aimWithoutSliders = skills.OfType<Aim>().Single(a => !a.IncludeSliders && !a.WithCheesability);
            var aimCheesed = skills.OfType<Aim>().Single(a => a.IncludeSliders && a.WithCheesability);
            var speed = skills.OfType<Speed>().Single();
            var flashlight = skills.OfType<Flashlight>().SingleOrDefault();

            double speedNotes = speed.RelevantNoteCount();
            double speedDifficultStrainCount = speed.CountTopWeightedNotes();

            double aimNoSlidersDifficultStrainCount = aimWithoutSliders.CountTopWeightedStrains();
            double aimNoSlidersTopWeightedSliderCount = aimWithoutSliders.CountTopWeightedSliders();
            Polynomial aimMissPenaltyCurve = ((OsuProbabilitySkill)skills[0]).GetMissPenaltyCurve();

            double aimTopWeightedSliderFactor = aimNoSlidersTopWeightedSliderCount / Math.Max(1, aimNoSlidersDifficultStrainCount - aimNoSlidersTopWeightedSliderCount);

            double speedTopWeightedSliderCount = speed.CountTopWeightedSliders();
            double speedTopWeightedSliderFactor = speedTopWeightedSliderCount / Math.Max(1, speedDifficultStrainCount - speedTopWeightedSliderCount);

            double difficultSliders = aim.GetDifficultSliders();

            double approachRate = CalculateRateAdjustedApproachRate(beatmap.Difficulty.ApproachRate, clockRate);
            double overallDifficulty = CalculateRateAdjustedOverallDifficulty(beatmap.Difficulty.OverallDifficulty, clockRate);

            int hitCircleCount = beatmap.HitObjects.Count(h => h is HitCircle);
            int sliderCount = beatmap.HitObjects.Count(h => h is Slider);
            int spinnerCount = beatmap.HitObjects.Count(h => h is Spinner);

            int totalHits = beatmap.HitObjects.Count;

            double drainRate = beatmap.Difficulty.DrainRate;

            double aimDifficultyValue = aim.DifficultyValue();
            double aimNoSlidersDifficultyValue = aimWithoutSliders.DifficultyValue();
            double cheesedAimDifficultyValue = aimCheesed.DifficultyValue();
            double speedDifficultyValue = speed.DifficultyValue();

            double mechanicalDifficultyRating = calculateMechanicalDifficultyRating(aimDifficultyValue, speedDifficultyValue);
            double sliderFactor = aimDifficultyValue > 0 ? OsuRatingCalculator.CalculateDifficultyRating(aimNoSlidersDifficultyValue) / OsuRatingCalculator.CalculateDifficultyRating(aimDifficultyValue) : 1;


            var osuRatingCalculator = new OsuRatingCalculator(mods, totalHits, approachRate, overallDifficulty, mechanicalDifficultyRating, sliderFactor);

            double aimRating = osuRatingCalculator.ComputeAimRating(aimDifficultyValue);
            double speedRating = osuRatingCalculator.ComputeSpeedRating(speedDifficultyValue);
            double aimRatingCheesed = osuRatingCalculator.ComputeAimRating(cheesedAimDifficultyValue);

            double flashlightRating = 0.0;

            if (flashlight is not null)
                flashlightRating = osuRatingCalculator.ComputeFlashlightRating(flashlight.DifficultyValue());

            double cheeseFactor = aimRating > 0 ? aimRatingCheesed / aimRating : 1;
            double sliderNestedScorePerObject = LegacyScoreUtils.CalculateNestedScorePerObject(beatmap, totalHits);
            double legacyScoreBaseMultiplier = LegacyScoreUtils.CalculateDifficultyPeppyStars(beatmap);

            var simulator = new OsuLegacyScoreSimulator();
            var scoreAttributes = simulator.Simulate(WorkingBeatmap, beatmap);

            double baseAimPerformance = OsuStrainSkill.DifficultyToPerformance(aimRating);
            double baseSpeedPerformance = OsuStrainSkill.DifficultyToPerformance(speedRating);
            double baseFlashlightPerformance = Flashlight.DifficultyToPerformance(flashlightRating);

            double basePerformance =
                Math.Pow(
                    Math.Pow(baseAimPerformance, 1.1) +
                    Math.Pow(baseSpeedPerformance, 1.1) +
                    Math.Pow(baseFlashlightPerformance, 1.1), 1.0 / 1.1
                );

            double multiplier = CalculateDifficultyMultiplier(mods, totalHits, spinnerCount);
            double starRating = calculateStarRating(basePerformance, multiplier);


            OsuDifficultyAttributes attributes = new OsuDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                AimDifficulty = aimRating,
                AimDifficultSliderCount = difficultSliders,
                SpeedDifficulty = speedRating,
                SpeedNoteCount = speedNotes,
                FlashlightDifficulty = flashlightRating,
                SliderFactor = sliderFactor,
                AimMissPenaltyCurve = aimMissPenaltyCurve,
                CheeseFactor = cheeseFactor,
                SpeedDifficultStrainCount = speedDifficultStrainCount,
                AimTopWeightedSliderFactor = aimTopWeightedSliderFactor,
                SpeedTopWeightedSliderFactor = speedTopWeightedSliderFactor,
                DrainRate = drainRate,
                MaxCombo = beatmap.GetMaxCombo(),
                HitCircleCount = hitCircleCount,
                SliderCount = sliderCount,
                SpinnerCount = spinnerCount,
                NestedScorePerObject = sliderNestedScorePerObject,
                LegacyScoreBaseMultiplier = legacyScoreBaseMultiplier,
                MaximumLegacyComboScore = scoreAttributes.ComboScore
            };

            return attributes;
        }

        private static double calculateMechanicalDifficultyRating(double aimDifficultyValue, double speedDifficultyValue)
        {
            double aimValue = OsuStrainSkill.DifficultyToPerformance(calculateDifficultyRating(aimDifficultyValue));
            double speedValue = OsuStrainSkill.DifficultyToPerformance(calculateDifficultyRating(speedDifficultyValue));

            double totalValue = Math.Pow(Math.Pow(aimValue, 1.1) + Math.Pow(speedValue, 1.1), 1 / 1.1);

            return calculateStarRating(totalValue, performance_base_multiplier);
        }

        private static double calculateStarRating(double basePerformance, double multiplier)
        {
            if (basePerformance <= 0.00001)
                return 0;

            return Math.Cbrt(multiplier) * star_rating_multiplier * (Math.Cbrt(100000 / Math.Pow(2, 1 / 1.1) * basePerformance) + 4);
        }

        private static double calculateDifficultyRating(double difficultyValue) => Math.Sqrt(difficultyValue) * difficulty_multiplier;

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();
            List<DifficultyHitObject> tapObjects = new List<DifficultyHitObject>();

            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                if (beatmap.HitObjects[i] is Slider slider)
                {
                    IList<HitObject> nestedObjects = slider.NestedHitObjects;

                    // Add slider head as tap object
                    if (objects.Count > 0)
                        objects.Add(new OsuDifficultyHitObject(slider, objects.Last().BaseObject, clockRate, objects, objects.Count, tapObjects, tapObjects.Count));
                    else
                        objects.Add(new OsuDifficultyHitObject(slider, beatmap.HitObjects[i - 1], clockRate, objects, objects.Count, tapObjects, tapObjects.Count));
                    tapObjects.Add(objects.Last());

                    // tapObjects not included in args since nested objects past head don't require a tap
                    // Includes slider ticks, reverse arrows, and slider tails
                    // Ticks are ignored if they're too close to the previous tick to avoid inflating strain
                    OsuHitObject lastTick = slider;

                    for (int j = 1; j < nestedObjects.Count; j++)
                    {
                        if (nestedObjects[j] is not SliderTick || OsuDifficultyHitObject.IsTickFarEnough(lastTick, (SliderTick)nestedObjects[j]))
                        {
                            objects.Add(new OsuDifficultyHitObject(nestedObjects[j], lastTick, clockRate, objects, objects.Count, parent: tapObjects.Last()));

                            lastTick = (OsuHitObject)nestedObjects[j];
                        }
                    }
                }
                else
                {
                    if (objects.Count > 0)
                        objects.Add(new OsuDifficultyHitObject(beatmap.HitObjects[i], objects.Last().BaseObject, clockRate, objects, objects.Count, tapObjects, tapObjects.Count));
                    else
                        objects.Add(new OsuDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], clockRate, objects, objects.Count, tapObjects, tapObjects.Count));
                    tapObjects.Add(objects.Last());
                }
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            var skills = new List<Skill>
            {
                new Aim(mods, true, false),
                new Aim(mods, false, false),
                new Speed(mods),
                new Aim(mods, true, true),
            };

            if (mods.Any(h => h is OsuModFlashlight))
                skills.Add(new Flashlight(mods));

            return skills.ToArray();
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModTouchDevice(),
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
            new OsuModFlashlight(),
            new OsuModHidden(),
            new OsuModSpunOut(),
        };
    }
}
