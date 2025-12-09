// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    public class OsuDifficultyHitObject : DifficultyHitObject
    {
        /// <summary>
        /// A distance by which all distances should be scaled in order to assume a uniform circle size.
        /// </summary>
        public const int NORMALISED_RADIUS = 50; // Change radius to 50 to make 100 the diameter. Easier for mental maths.

        public const int NORMALISED_DIAMETER = NORMALISED_RADIUS * 2;

        public const int MIN_DELTA_TIME = 25;

        private float assumed_slider_radius = NORMALISED_RADIUS * 1.0f;
        private float redundant_slider_radius = NORMALISED_RADIUS * 1.3f;

        protected new OsuHitObject BaseObject => (OsuHitObject)base.BaseObject;

        /// <summary>
        /// <see cref="DifficultyHitObject.DeltaTime"/> capped to a minimum of <see cref="MIN_DELTA_TIME"/>ms.
        /// </summary>
        public readonly double AdjustedDeltaTime;

        /// <summary>
        /// Normalised distance from the "lazy" end position of the previous <see cref="OsuDifficultyHitObject"/> to the start position of this <see cref="OsuDifficultyHitObject"/>.
        /// <para>
        /// The "lazy" end position is the position at which the cursor ends up if the previous hitobject is followed with as minimal movement as possible (i.e. on the edge of slider follow circles).
        /// </para>
        /// </summary>
        public double LazyJumpDistance { get; private set; }

        /// <summary>
        /// Normalised shortest distance to consider for a jump between the previous <see cref="OsuDifficultyHitObject"/> and this <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        /// <remarks>
        /// This is bounded from above by <see cref="LazyJumpDistance"/>, and is smaller than the former if a more natural path is able to be taken through the previous <see cref="OsuDifficultyHitObject"/>.
        /// </remarks>
        /// <example>
        /// Suppose a linear slider - circle pattern.
        /// <br />
        /// Following the slider lazily (see: <see cref="LazyJumpDistance"/>) will result in underestimating the true end position of the slider as being closer towards the start position.
        /// As a result, <see cref="LazyJumpDistance"/> overestimates the jump distance because the player is able to take a more natural path by following through the slider to its end,
        /// such that the jump is felt as only starting from the slider's true end position.
        /// <br />
        /// Now consider a slider - circle pattern where the circle is stacked along the path inside the slider.
        /// In this case, the lazy end position correctly estimates the true end position of the slider and provides the more natural movement path.
        /// </example>
        public double MinimumJumpDistance { get; private set; }

        /// <summary>
        /// The time taken to travel through <see cref="MinimumJumpDistance"/>, with a minimum value of 25ms.
        /// </summary>
        public double MinimumJumpTime { get; private set; }

        /// <summary>
        /// Normalised distance between the start and end position of this <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public double TravelDistance { get; private set; }

        /// <summary>
        /// The time taken to travel through <see cref="TravelDistance"/>, with a minimum value of 25ms for <see cref="Slider"/> objects.
        /// </summary>
        public double TravelTime { get; private set; }

        /// <summary>
        /// The position of the cursor at the point of completion of this <see cref="OsuDifficultyHitObject"/> if it is a <see cref="Slider"/>
        /// and was hit with as few movements as possible.
        /// </summary>
        public Vector2? LazyEndPosition { get; private set; }

        /// <summary>
        /// The distance travelled by the cursor upon completion of this <see cref="OsuDifficultyHitObject"/> if it is a <see cref="Slider"/>
        /// and was hit with as few movements as possible.
        /// </summary>
        public double LazyTravelDistance { get; private set; }

        /// <summary>
        /// The time taken by the cursor upon completion of this <see cref="OsuDifficultyHitObject"/> if it is a <see cref="Slider"/>
        /// and was hit with as few movements as possible.
        /// </summary>
        public double LazyTravelTime { get; private set; }

        /// <summary>
        /// Angle the player has to take to hit this <see cref="OsuDifficultyHitObject"/>.
        /// Calculated as the angle between the circles (current-2, current-1, current).
        /// </summary>
        public double? Angle { get; private set; }

        public double? AngleSigned { get; private set; }

        /// <summary>
        /// Retrieves the full hit window for a Great <see cref="HitResult"/>.
        /// </summary>
        public double HitWindowGreat { get; private set; }

        /// <summary>
        /// Selective bonus for maps with higher circle size.
        /// </summary>
        public double SmallCircleBonus { get; private set; }

        public double? NormalisedVectorAngle { get; private set; }

        /// <summary>
        /// The extra time to hit the circle if cheesed.
        /// </summary>
        public double ExtraDeltaTime { get; private set; }

        private readonly OsuDifficultyHitObject? lastLastDifficultyObject;
        public List<Movement> Movements { get; } = new List<Movement>();

        public Movement? PreviousMovement => lastDifficultyObject?.Movements.Last();

        private readonly OsuDifficultyHitObject? lastDifficultyObject;

        public OsuDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            lastDifficultyObject = index > 0 ? (OsuDifficultyHitObject)objects[index - 1] : null;
            var osuLastObj = (OsuHitObject)lastObject;

            // Capped to 25ms to prevent difficulty calculation breaking from simultaneous objects.
            AdjustedDeltaTime = Math.Max(DeltaTime, MIN_DELTA_TIME);

            SmallCircleBonus = Math.Max(1.0, 1.0 + (30 - BaseObject.Radius) / 40);

            double hitWindowOk;
            if (BaseObject is Slider sliderObject)
            {
                HitWindowGreat = sliderObject.HeadCircle.HitWindows.WindowFor(HitResult.Great) / clockRate;
                hitWindowOk = sliderObject.HeadCircle.HitWindows.WindowFor(HitResult.Ok) / clockRate;
            }
            else
            {
                HitWindowGreat = BaseObject.HitWindows.WindowFor(HitResult.Great) / clockRate;
                hitWindowOk = BaseObject.HitWindows.WindowFor(HitResult.Ok) / clockRate;
            }

            float scalingFactor = NORMALISED_RADIUS / (float)BaseObject.Radius;

            if (lastDifficultyObject != null)
            {
                // remove slider movements from the previous object that are equal to a head->head jump
                var headToHeadMovement = new Movement
                {
                    Start = lastDifficultyObject.BaseObject.StackedPosition,
                    StartTime = lastDifficultyObject.StartTime,
                    StartRadius = lastDifficultyObject.BaseObject.Radius,
                    End = BaseObject.StackedPosition,
                    EndTime = StartTime,
                    EndRadius = BaseObject.Radius
                };

                var movementsToRemove = new List<Movement>();

                for (int i = 1; i < lastDifficultyObject.Movements.Count; i++)
                {
                    var nestedMovement = lastDifficultyObject.Movements[i];

                    if (staysWithinRadius(headToHeadMovement, nestedMovement, redundant_slider_radius / scalingFactor))
                    {
                        //if (nestedMovement.Distance > headToHeadMovement.Distance)
                        {
                            // if a movement repeats head-to-head movement it can be removed, but only if all subsequent movements also follow the same line
                            movementsToRemove.Add(nestedMovement);
                        }
                    }
                    else if (movementsToRemove.Count > 0)
                    {
                        // cancel movement removal if the next movement doesn't also stay within radius since we'll need to move the cursor for both this and all previous movements to complete the slider
                        movementsToRemove.Clear();
                        break;
                    }
                }

                for (int i = 1; i < lastDifficultyObject.Movements.Count - 1; i++)
                {
                    var nestedMovement = lastDifficultyObject.Movements[i];

                    if (nestedMovement.Distance < redundant_slider_radius)
                    {
                        var nextNestedMovement = lastDifficultyObject.Movements[i + 1];
                        nextNestedMovement.Start = nestedMovement.Start;
                        nextNestedMovement.StartTime = nestedMovement.StartTime;
                        nextNestedMovement.StartRadius = nestedMovement.StartRadius;

                        movementsToRemove.Add(nestedMovement);
                    }
                }

                foreach (var movement in movementsToRemove)
                {
                    lastDifficultyObject.Movements.Remove(movement);
                }
            }

            var prevMovement = lastDifficultyObject?.Movements.LastOrDefault();
            var prevEndPosition = prevMovement?.End ?? lastDifficultyObject?.BaseObject.StackedPosition ?? osuLastObj.StackedEndPosition;
            double prevEndTime = prevMovement?.EndTime ?? lastDifficultyObject?.EndTime ?? (osuLastObj.StartTime / clockRate);

            Movements.Add(new Movement
            {
                Start = prevEndPosition,
                StartTime = prevEndTime,
                StartRadius = prevMovement?.EndRadius ?? (float?)lastDifficultyObject?.BaseObject.Radius ?? 1f,
                End = BaseObject.StackedPosition,
                EndTime = StartTime,
                EndRadius = BaseObject.Radius
            });

            computeSliderMovements(clockRate);

            if (PreviousMovement != null)
                Angle = Movements.First().Angle(PreviousMovement);

            LazyJumpDistance = Movements.First().Distance;
            MinimumJumpTime = Movements.First().Time;
            MinimumJumpDistance = LazyJumpDistance;

            LazyTravelDistance = Movements.Where(x => x.IsNested).Sum(x => x.Distance);

            if (BaseObject is Slider currentSlider)
            {
                TravelDistance = LazyTravelDistance;
                TravelTime = Math.Max(LazyTravelTime / clockRate, MIN_DELTA_TIME);
            }
            // Worst case if the player wanted to cheese notes while still getting 100s.
            // The extra delta time is repeatedly halved if the delta time says constant.
            // If a slowdown occurs (deltaTimeDifference > 0), add the slowdown to the extra delta time,
            // and cap it back to the 50 hit window.
            if (lastDifficultyObject != null)
            {
                double deltaTimeDifference = DeltaTime - lastDifficultyObject.DeltaTime;
                ExtraDeltaTime = Math.Min(lastDifficultyObject.ExtraDeltaTime / 2.0 + Math.Max(0, deltaTimeDifference), hitWindowOk);

                double cheeseFromOverlap = Math.Min(1, LazyJumpDistance / 100) * (1 - Math.Min(1, lastDifficultyObject.LazyJumpDistance / 100));
                ExtraDeltaTime = Math.Max(ExtraDeltaTime, hitWindowOk * cheeseFromOverlap);
            }
            else
            {
                ExtraDeltaTime = hitWindowOk;
            }
        }

        public double OpacityAt(double time, bool hidden)
        {
            if (time > BaseObject.StartTime)
            {
                // Consider a hitobject as being invisible when its start time is passed.
                // In reality the hitobject will be visible beyond its start time up until its hittable window has passed,
                // but this is an approximation and such a case is unlikely to be hit where this function is used.
                return 0.0;
            }

            double fadeInStartTime = BaseObject.StartTime - BaseObject.TimePreempt;
            double fadeInDuration = BaseObject.TimeFadeIn;

            if (hidden)
            {
                // Taken from OsuModHidden.
                double fadeOutStartTime = BaseObject.StartTime - BaseObject.TimePreempt + BaseObject.TimeFadeIn;
                double fadeOutDuration = BaseObject.TimePreempt * OsuModHidden.FADE_OUT_DURATION_MULTIPLIER;

                return Math.Min
                (
                    Math.Clamp((time - fadeInStartTime) / fadeInDuration, 0.0, 1.0),
                    1.0 - Math.Clamp((time - fadeOutStartTime) / fadeOutDuration, 0.0, 1.0)
                );
            }

            return Math.Clamp((time - fadeInStartTime) / fadeInDuration, 0.0, 1.0);
        }

        /// <summary>
        /// Returns how possible is it to doubletap this object together with the next one and get perfect judgement in range from 0 to 1
        /// </summary>
        public double GetDoubletapness(OsuDifficultyHitObject? osuNextObj)
        {
            if (osuNextObj != null)
            {
                double currDeltaTime = Math.Max(1, DeltaTime);
                double nextDeltaTime = Math.Max(1, osuNextObj.DeltaTime);
                double deltaDifference = Math.Abs(nextDeltaTime - currDeltaTime);
                double speedRatio = currDeltaTime / Math.Max(currDeltaTime, deltaDifference);
                double windowRatio = Math.Pow(Math.Min(1, currDeltaTime / (2 * HitWindowGreat)), 2);
                return 1.0 - Math.Pow(speedRatio, 1 - windowRatio);
            }

            return 0;
        }

        private void computeSliderMovements(double clockRate)
        {
            if (BaseObject is not Slider slider)
                return;

            if (LazyEndPosition != null)
                return;

            // TODO: This commented version is actually correct by the new lazer implementation, but intentionally held back from
            // difficulty calculator to preserve known behaviour.
            // double trackingEndTime = Math.Max(
            //     // SliderTailCircle always occurs at the final end time of the slider, but the player only needs to hold until within a lenience before it.
            //     slider.Duration + SliderEventGenerator.TAIL_LENIENCY,
            //     // There's an edge case where one or more ticks/repeats fall within that leniency range.
            //     // In such a case, the player needs to track until the final tick or repeat.
            //     slider.NestedHitObjects.LastOrDefault(n => n is not SliderTailCircle)?.StartTime ?? double.MinValue
            // );

            double trackingEndTime = Math.Max(
                slider.StartTime + slider.Duration + SliderEventGenerator.TAIL_LENIENCY,
                slider.StartTime + slider.Duration / 2
            );

            IList<HitObject> nestedObjects = slider.NestedHitObjects;

            SliderTick? lastRealTick = null;

            foreach (var hitobject in slider.NestedHitObjects)
            {
                if (hitobject is SliderTick tick)
                    lastRealTick = tick;
            }

            if (lastRealTick?.StartTime > trackingEndTime)
            {
                trackingEndTime = lastRealTick.StartTime;

                // When the last tick falls after the tracking end time, we need to re-sort the nested objects
                // based on time. This creates a somewhat weird ordering which is counter to how a user would
                // understand the slider, but allows a zero-diff with known diffcalc output.
                //
                // To reiterate, this is definitely not correct from a difficulty calculation perspective
                // and should be revisited at a later date (likely by replacing this whole code with the commented
                // version above).
                List<HitObject> reordered = nestedObjects.ToList();

                reordered.Remove(lastRealTick);
                reordered.Add(lastRealTick);

                nestedObjects = reordered;
            }

            LazyTravelTime = trackingEndTime - slider.StartTime;

            double endTimeMin = LazyTravelTime / slider.SpanDuration;
            if (endTimeMin % 2 >= 1)
                endTimeMin = 1 - endTimeMin % 1;
            else
                endTimeMin %= 1;

            LazyEndPosition = slider.StackedPosition + slider.Path.PositionAt(endTimeMin); // temporary lazy end position until a real result can be derived.

            Vector2 currCursorPosition = slider.StackedPosition;
            double currCursorTime = slider.StartTime;
            double currRadius = NORMALISED_RADIUS;

            float scalingFactor = NORMALISED_RADIUS / (float)slider.Radius; // lazySliderDistance is coded to be sensitive to scaling, this makes the maths easier with the thresholds being used.

            for (int i = 1; i < nestedObjects.Count; i++)
            {
                var currNestedObj = (OsuHitObject)nestedObjects[i];

                Vector2 currMovement = currNestedObj.StackedPosition - currCursorPosition;
                double newCurrTime = currNestedObj.StartTime;

                // Amount of movement required so that the cursor position needs to be updated.
                double nestedRadius = assumed_slider_radius;

                if (i == nestedObjects.Count - 1)
                {
                    // The end of a slider has special aim rules due to the relaxed time constraint on position.
                    // There is both a lazy end position as well as the actual end slider position. We assume the player takes the simpler movement.
                    // For sliders that are circular, the lazy end position may actually be farther away than the sliders true end.
                    // This code is designed to prevent buffing situations where lazy end is actually a less efficient movement.
                    Vector2 lazyMovement = (Vector2)LazyEndPosition - currCursorPosition;

                    if (lazyMovement.Length < currMovement.Length)
                    {
                        currMovement = lazyMovement;
                        newCurrTime = trackingEndTime;
                    }
                }

                double currMovementLength = currMovement.Length * scalingFactor;

                if (currMovementLength > nestedRadius)
                {
                    double movementLengthMultiplier = (currMovementLength - nestedRadius) / currMovementLength;

                    var newCurrPosition = currCursorPosition + currMovement * (float)movementLengthMultiplier;

                    Movements.Add(new Movement
                    {
                        Start = currCursorPosition,
                        StartTime = currCursorTime / clockRate,
                        StartRadius = currRadius / scalingFactor,
                        End = newCurrPosition,
                        EndTime = newCurrTime / clockRate,
                        EndRadius = nestedRadius / scalingFactor,
                        IsNested = true
                    });

                    currCursorPosition = newCurrPosition;
                    currCursorTime = newCurrTime;
                    currRadius = nestedRadius;
                }

                if (i == nestedObjects.Count - 1)
                    LazyEndPosition = currCursorPosition;
            }
        }

        private static bool staysWithinRadius(Movement movementA, Movement movementB, float radius)
        {
            var smallestMovement = movementA.Distance < movementB.Distance ? movementA : movementB;
            var biggestMovement = movementA.Distance > movementB.Distance ? movementA : movementB;

            float dStart = distancePointToMovement(smallestMovement.Start, biggestMovement);
            float dEnd = distancePointToMovement(smallestMovement.End, biggestMovement);

            return dStart <= radius && dEnd <= radius;
        }

        private static float distancePointToMovement(Vector2 point, Movement movement)
        {
            Vector2 ab = movement.End - movement.Start;
            float t = Vector2.Dot(point - movement.Start, ab) / ab.LengthSquared;
            t = Math.Clamp(t, 0, 1);
            Vector2 closest = movement.Start + t * ab;
            return Vector2.Distance(point, closest);
        }
    }
}
