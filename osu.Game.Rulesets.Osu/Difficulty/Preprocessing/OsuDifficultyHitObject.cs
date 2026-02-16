// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
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
        private float repeat_slider_radius = NORMALISED_RADIUS * 1.1f;
        private float redundant_slider_radius = NORMALISED_RADIUS * 1.3f;

        protected new OsuHitObject BaseObject => (OsuHitObject)base.BaseObject;

        /// <summary>
        /// <see cref="DifficultyHitObject.DeltaTime"/> capped to a minimum of <see cref="MIN_DELTA_TIME"/>ms.
        /// </summary>
        public readonly double AdjustedDeltaTime;

        /// <summary>
        /// Amount of time elapsed between lastDifficultyObject's <see cref="DifficultyHitObject.EndTime"/> and <see cref="DifficultyHitObject.StartTime"/> capped to a minimum of <see cref="MIN_DELTA_TIME"/>ms.
        /// </summary>
        public double LastObjectEndDeltaTime { get; private set; }

        /// <summary>
        /// Time (in ms) between the object first appearing and the time it needs to be clicked.
        /// <see cref="OsuHitObject.TimePreempt"/> adjusted by clock rate.
        /// </summary>
        public readonly double Preempt;

        /// <summary>
        /// Normalised distance from the "lazy" end position of the previous <see cref="OsuDifficultyHitObject"/> to the start position of this <see cref="OsuDifficultyHitObject"/>.
        /// <para>
        /// The "lazy" end position is the position at which the cursor ends up if the previous hitobject is followed with as minimal movement as possible (i.e. on the edge of slider follow circles).
        /// </para>
        /// </summary>
        public double LazyJumpDistance { get; private set; }

        /// <summary>
        /// The position of the cursor at the point of completion of this <see cref="OsuDifficultyHitObject"/> if it is a <see cref="Slider"/>
        /// and was hit with as few movements as possible.
        /// </summary>
        public Vector2? LazyEndPosition { get; private set; }

        /// <summary>
        /// The time taken by the cursor upon completion of this <see cref="OsuDifficultyHitObject"/> if it is a <see cref="Slider"/>
        /// and was hit with as few movements as possible.
        /// </summary>
        public double LazyTravelTime { get; private set; }

        /// <summary>
        /// Selective bonus for maps with higher circle size.
        /// </summary>
        public double SmallCircleBonus { get; private set; }

        // legacy angle
        public double? Angle { get; private set; }

        public double PathLengthToMovementLengthRatio { get; set; } = 1;

        public List<Movement> Movements { get; } = new List<Movement>();

        public Movement? PreviousMovement => lastDifficultyObject?.Movements.Last();

        private readonly OsuDifficultyHitObject? lastDifficultyObject;

        public OsuDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            lastDifficultyObject = index > 0 ? (OsuDifficultyHitObject)objects[index - 1] : null;

            // Capped to 25ms to prevent difficulty calculation breaking from simultaneous objects.
            AdjustedDeltaTime = Math.Max(DeltaTime, MIN_DELTA_TIME);
            LastObjectEndDeltaTime = lastDifficultyObject != null ? Math.Max(StartTime - lastDifficultyObject.EndTime, MIN_DELTA_TIME) : AdjustedDeltaTime;

            SmallCircleBonus = 1;

            Preempt = BaseObject.TimePreempt / clockRate;

            adjustPreviousObjectMovements();
            addInitialMovement((OsuHitObject)lastObject, clockRate);
            computeSliderMovements(clockRate);
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

            // Equal to `OsuHitObject.TimeFadeIn` minus any adjustments from the HD mod.
            double fadeInDuration = 400 * Math.Min(1, BaseObject.TimePreempt / OsuHitObject.PREEMPT_MIN);

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
                double windowRatio = Math.Pow(Math.Min(1, currDeltaTime / HitWindow(HitResult.Great)), 5);

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

                if (currNestedObj is SliderRepeat)
                {
                    nestedRadius = repeat_slider_radius;
                }

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

        private void addInitialMovement(OsuHitObject osuLastObj, double clockRate)
        {
            var prevMovement = lastDifficultyObject?.Movements.LastOrDefault();
            var prevEndPosition = prevMovement?.End ?? lastDifficultyObject?.BaseObject.StackedPosition ?? osuLastObj.StackedEndPosition;
            double prevEndTime = prevMovement?.EndTime ?? lastDifficultyObject?.EndTime ?? (osuLastObj.StartTime / clockRate);

            var initialMovement = new Movement
            {
                Start = prevEndPosition,
                StartTime = prevEndTime,
                StartRadius = prevMovement?.EndRadius ?? (float?)lastDifficultyObject?.BaseObject.Radius ?? 1f,
                End = BaseObject.StackedPosition,
                EndTime = StartTime,
                EndRadius = BaseObject.Radius
            };
            Movements.Add(initialMovement);

            // i love kwotaq reading!!
            if (prevMovement != null)
            {
                Angle = initialMovement.Angle(prevMovement);
            }

            LazyJumpDistance = initialMovement.Distance;
        }

        private void adjustPreviousObjectMovements()
        {
            if (lastDifficultyObject?.BaseObject is not Slider slider)
                return;

            float scalingFactor = NORMALISED_RADIUS / (float)BaseObject.Radius;

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

            if (slider.RepeatCount < 1)
            {
                for (int i = 1; i < lastDifficultyObject.Movements.Count; i++)
                {
                    var nestedMovement = lastDifficultyObject.Movements[i];
                    if (!nestedMovement.IsNested)
                        continue;

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
            }

            for (int i = 1; i < lastDifficultyObject.Movements.Count; i++)
            {
                var nestedMovement = lastDifficultyObject.Movements[i];
                if (!nestedMovement.IsNested)
                    continue;

                // remove all movements shorter than the follow radius and adjust remaining movements to be continuous
                if (nestedMovement.Distance < redundant_slider_radius)
                {
                    if (i < lastDifficultyObject.Movements.Count - 1)
                    {
                        var nextNestedMovement = lastDifficultyObject.Movements[i + 1];
                        nextNestedMovement.Start = nestedMovement.Start;
                        nextNestedMovement.StartTime = nestedMovement.StartTime;
                        nextNestedMovement.StartRadius = nestedMovement.StartRadius;
                    }

                    movementsToRemove.Add(nestedMovement);
                }
            }

            foreach (var movement in movementsToRemove)
            {
                lastDifficultyObject.Movements.Remove(movement);
            }

            // set path to movement length ratio after we're done removing all the redundant movements
            double movementDistance = lastDifficultyObject.Movements.Count > 1
                ? lastDifficultyObject.Movements.Where(x => x.IsNested).Sum(x => x.Distance) + (redundant_slider_radius / scalingFactor)
                : headToHeadMovement.Distance;

            if (movementDistance > redundant_slider_radius)
            {
                double sliderLength = slider.Path.Distance * scalingFactor;
                lastDifficultyObject.PathLengthToMovementLengthRatio = Math.Pow(Math.Clamp(movementDistance / sliderLength, 0, 1), sliderLength * 0.001);
            }
        }

        public static bool IsValid(DifficultyHitObject current, int notesBackward, int notesForward = 0)
        {
            if (current.Index < notesBackward || current.IndexFromEnd < notesForward || current.BaseObject is Spinner)
                return false;

            for (int i = 0; i < notesBackward; i++)
            {
                if (current.Previous(i).BaseObject is Spinner)
                    return false;
            }

            for (int i = 0; i < notesForward; i++)
            {
                if (current.Next(i).BaseObject is Spinner)
                    return false;
            }

            return true;
        }
    }
}
