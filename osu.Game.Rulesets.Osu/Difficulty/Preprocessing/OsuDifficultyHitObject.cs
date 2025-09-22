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

        public const float MAXIMUM_SLIDER_RADIUS = NORMALISED_RADIUS * 2.4f;
        public const float ASSUMED_SLIDER_RADIUS = NORMALISED_RADIUS * 1.8f;

        protected new OsuHitObject BaseObject => (OsuHitObject)base.BaseObject;
        protected new OsuHitObject LastObject => (OsuHitObject)base.LastObject;

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
        /// Normalised distance from the "lazy" end position of the previous previous <see cref="OsuDifficultyHitObject"/> to the start position of the previous <see cref="OsuDifficultyHitObject"/>.
        /// <para>
        /// The "lazy" end position is the position at which the cursor ends up if the previous hitobject is followed with as minimal movement as possible (i.e. on the edge of slider follow circles).
        /// </para>
        /// </summary>
        public double PrevLazyJumpDistance { get; private set; }

        /// <summary>
        /// The time taken to travel through <see cref="LazyJumpDistance"/>, with a minimum value of 25ms.
        /// </summary>
        public double MinimumJumpTime { get; private set; }

        /// <summary>
        /// The time taken to travel through <see cref="LazyJumpDistance"/>, with a minimum value of 25ms.
        /// </summary>
        public double? PrevMinimumJumpTime { get; private set; }

        public double SliderlessJumpDistance { get; private set; }

        public double PrevSliderlessJumpDistance { get; private set; }

        /// <summary>
        /// The position of the cursor at the point of completion of this <see cref="OsuDifficultyHitObject"/> if it is a <see cref="Slider"/>
        /// and was hit with as few movements as possible.
        /// </summary>
        public Vector2 CursorPosition { get; private set; }

        /// <summary>
        /// Angle the player has to take to hit this <see cref="OsuDifficultyHitObject"/>.
        /// Calculated as something.
        /// </summary>
        public double? Angle { get; private set; }

        /// <summary>
        /// Angle the player has to take to hit this <see cref="OsuDifficultyHitObject"/>.
        /// Calculated as something.
        /// </summary>
        public double? PrevAngle { get; private set; }

        /// <summary>
        /// Angle the player has to take to hit this <see cref="OsuDifficultyHitObject"/>.
        /// Calculated as the angle between the circles (current-2, current-1, current).
        /// </summary>
        public double? SliderlessAngle { get; private set; }

        /// <summary>
        /// Angle the player has to take to hit this <see cref="OsuDifficultyHitObject"/>.
        /// Calculated as the angle between the circles (current-2, current-1, current).
        /// </summary>
        public double? PrevSliderlessAngle { get; private set; }

        /// <summary>
        /// Retrieves the full hit window for a Great <see cref="HitResult"/>.
        /// </summary>
        public double HitWindowGreat { get; private set; }

        /// <summary>
        /// Selective bonus for maps with higher circle size.
        /// </summary>
        public double SmallCircleBonus { get; private set; }

        /// <summary>
        /// Returns true if the <see cref="DifficultyHitObject"/> requires a tap (is a circle or slider head)
        /// </summary>
        public bool IsTapObject { get; private set; }

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="OsuDifficultyHitObject"/> satisfying <see cref="IsTapObject"/>, with a minimum of 25ms.
        /// </summary>
        public double TapStrainTime;

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="OsuDifficultyHitObject"/> satisfying <see cref="IsTapObject"/>, with a minimum of 25ms.
        /// </summary>
        public double? PrevTapStrainTime;

        /// <summary>
        /// The distance travelled by the cursor upon completion of this <see cref="OsuDifficultyHitObject"/> if it is a <see cref="Slider"/>
        /// and was hit with as few movements as possible.
        /// </summary>
        public double TravelDistance { get; private set; }

        /// <summary>
        /// The time taken to travel through <see cref="TravelDistance"/>, not adjusted for clock rate.
        /// Only use within <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public double UnscaledTravelTime { get; private set; }

        /// <summary>
        /// The time taken to travel through <see cref="TravelDistance"/>, with a minimum value of 25ms for <see cref="Slider"/> objects.
        /// </summary>
        public double TravelTime { get; private set; }

        /// <summary>
        /// The extra time to hit the circle if cheesed.
        /// </summary>
        public double ExtraDeltaTime { get; private set; }

        public double? VectorAngle { get; private set; }

        /// <summary>
        /// The index of this <see cref="DifficultyHitObject"/> in the list of all <see cref="DifficultyHitObject"/>s satisfying <see cref="IsTapObject"/>.
        /// Is null if this object is not a circle or slider head.
        /// </summary>
        public int? TapIndex;

        public OsuDifficultyHitObject? Parent;

        private readonly IReadOnlyList<DifficultyHitObject>? difficultyTapHitObjects;

        private readonly OsuDifficultyHitObject? lastLastDifficultyObject;
        private readonly OsuDifficultyHitObject? lastDifficultyObject;
        private readonly OsuDifficultyHitObject? lastLastTapDifficultyObject;
        private readonly OsuDifficultyHitObject? lastTapDifficultyObject;

        public OsuDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, int index, List<DifficultyHitObject>? tapObjects = null, int? tapIndex = null, DifficultyHitObject? parent = null)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            lastLastDifficultyObject = index > 1 ? (OsuDifficultyHitObject)Previous(1) : null;
            lastDifficultyObject = index > 0 ? (OsuDifficultyHitObject)Previous(0) : null;

            // Capped to 25ms to prevent difficulty calculation breaking from simultaneous objects.
            AdjustedDeltaTime = Math.Max(DeltaTime, MIN_DELTA_TIME);

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

            if (tapObjects is not null && tapIndex is not null)
            {
                difficultyTapHitObjects = tapObjects;
                TapIndex = tapIndex;
                IsTapObject = true;

                lastLastTapDifficultyObject = tapIndex > 1 ? (OsuDifficultyHitObject)PreviousTap(1) : null;
                lastTapDifficultyObject = tapIndex > 0 ? (OsuDifficultyHitObject)PreviousTap(0) : null;

                if (lastTapDifficultyObject is not null)
                    TapStrainTime = Math.Max(StartTime - lastTapDifficultyObject.StartTime, MIN_DELTA_TIME);
                else
                {
                    TapStrainTime = Math.Max(DeltaTime, MIN_DELTA_TIME);
                }
            }
            else
                IsTapObject = false;

            if ((tapObjects is not null && tapIndex is null) || (tapObjects is null && tapIndex is not null))
                throw new MissingFieldException("tapObjects or tapIndex is not assigned during construction.");

            Parent = (OsuDifficultyHitObject?)parent;


            calculateCursorPosition();

            setDistances(clockRate);
            setTapDistances(clockRate);

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

            // Use larger radius for small cs bonus if object is slidertick/end
            double radius = IsTapObject ? BaseObject.Radius : BaseObject.Radius * ASSUMED_SLIDER_RADIUS / NORMALISED_RADIUS;
            SmallCircleBonus = Math.Max(1.0, 1.0 + (30 - radius) / 40);
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
                double windowRatio = Math.Pow(Math.Min(1, currDeltaTime / HitWindowGreat), 2);
                return 1.0 - Math.Pow(speedRatio, 1 - windowRatio);
            }

            return 0;
        }

        private void setDistances(double clockRate)
        {
            // We don't need to calculate either angle or distance when one of the last->curr objects is a spinner
            if (BaseObject is Spinner || LastObject is Spinner)
                return;

            // We will scale distances by this factor, so we can assume a uniform CircleSize among beatmaps.
            float scalingFactor = NORMALISED_RADIUS / (float)BaseObject.Radius;

            Vector2 currCursorPosition = CursorPosition;
            Vector2 lastCursorPosition = lastDifficultyObject?.CursorPosition ?? LastObject.StackedPosition;

            LazyJumpDistance = Vector2.Subtract(currCursorPosition, lastCursorPosition).Length * scalingFactor;
            MinimumJumpTime = AdjustedDeltaTime;
            PrevMinimumJumpTime = lastDifficultyObject?.MinimumJumpTime ?? null;

            if (LastObject is SliderTailCircle && lastDifficultyObject?.Parent is not null)
            {
                double trackingEndTime = Math.Max(
                    // SliderTailCircle always occurs at the final end time of the slider, but the player only needs to hold until within a lenience before it.
                    // This leniency is not scaled by clock rate, it is in the same position regardless of rate.
                    lastDifficultyObject.Parent.BaseObject.GetEndTime() + SliderEventGenerator.TAIL_LENIENCY,
                    // There's an edge case where one or more ticks/repeats fall within that leniency range.
                    // In such a case, the player needs to track until the final tick or repeat.
                    lastDifficultyObject.Parent.BaseObject.NestedHitObjects.LastOrDefault(n => n is not SliderTailCircle)?.StartTime ?? double.MinValue
                ) / clockRate;

                MinimumJumpTime = Math.Max(StartTime - trackingEndTime, MIN_DELTA_TIME);

                if (lastDifficultyObject is not null)
                {
                    float tailJumpDistance = Vector2.Subtract(LastObject.StackedPosition, BaseObject.StackedPosition).Length * scalingFactor;
                    double minimumJumpDistance = Math.Max(0, Math.Min(LazyJumpDistance - (MAXIMUM_SLIDER_RADIUS - ASSUMED_SLIDER_RADIUS), tailJumpDistance - MAXIMUM_SLIDER_RADIUS));

                    float distanceBetweenStartPositions = (CursorPosition * scalingFactor - lastDifficultyObject.LastObject.StackedPosition * scalingFactor).Length;

                    if (minimumJumpDistance < distanceBetweenStartPositions)
                    {
                        // MinimumJumpDistance can be sometimes calculated to be ~0 in cases where the player wouldn't move the cursor anywhere and treat the slider as just a normal circle.
                        //
                        //        o---<s===>  ← slider (s - start, length smaller than the followcircle)
                        //        ↑
                        //    next object
                        //
                        // In this case MinimumJumpDistance is calculated to be less than the jump from start of the object to the start of the next one which is impossible.
                        // Therefore, we set minimal distance and time to be that of a normal start-to-start jump.

                        MinimumJumpTime = MinimumJumpTime + lastDifficultyObject.MinimumJumpTime + (SliderEventGenerator.TAIL_LENIENCY / clockRate);
                        LazyJumpDistance = Math.Min(LazyJumpDistance, distanceBetweenStartPositions);
                    }
                    else
                    {
                        LazyJumpDistance = minimumJumpDistance;
                    }
                }
            }

            if (lastDifficultyObject is not null && lastLastDifficultyObject is not null && lastLastDifficultyObject.BaseObject is not Spinner)
            {
                // // Calculates angle based on actual object positions
                Vector2 v1 = lastLastDifficultyObject.CursorPosition - lastCursorPosition;
                Vector2 v2 = currCursorPosition - lastCursorPosition;

                VectorAngle = Math.Atan2(Math.Abs(v2.Y), Math.Abs(v2.X));

                OsuDifficultyHitObject prevObj = lastDifficultyObject;
                OsuDifficultyHitObject prevPrevObj = lastLastDifficultyObject;

                // If the current cursor pos is close enough to the previous one
                // Ignore the angle from it and recalc the angle from earlier objects
                // Ensures doubletaps and sliderjumps are treated properly
                // For maps like /b/3455732
                while (v2.Length * scalingFactor < 20 && prevPrevObj is not null)
                {
                    v1 = prevPrevObj.CursorPosition - prevObj.CursorPosition;
                    v2 = currCursorPosition - prevObj.CursorPosition;

                    prevObj = prevPrevObj;
                    prevPrevObj = (OsuDifficultyHitObject)prevPrevObj.Previous(0);
                }

                while (v1.Length * scalingFactor < 20 && prevPrevObj is not null)
                {
                    v1 = prevPrevObj.CursorPosition - prevObj.CursorPosition;
                    PrevMinimumJumpTime = Math.Max(prevObj.StartTime - prevPrevObj.StartTime, MIN_DELTA_TIME);

                    prevPrevObj = (OsuDifficultyHitObject)prevPrevObj.Previous(0);
                }

                PrevLazyJumpDistance = v1.Length * scalingFactor;
                PrevAngle = prevObj.Angle;

                float dot = Vector2.Dot(v1, v2);
                float det = v1.X * v2.Y - v1.Y * v2.X;

                Angle = Math.Abs(Math.Atan2(det, dot));
            }
            else if (lastDifficultyObject is not null)
            {
                PrevLazyJumpDistance = lastDifficultyObject.LazyJumpDistance;
                PrevMinimumJumpTime = lastDifficultyObject.MinimumJumpTime;
                PrevAngle = lastDifficultyObject.Angle;
            }
            else
            {
                PrevLazyJumpDistance = 0;
                PrevMinimumJumpTime = null;
                PrevAngle = null;
            }

            if (!IsTapObject && Parent is not null)
                Parent.TravelDistance += LazyJumpDistance;

            if (BaseObject is SliderTailCircle && Parent?.BaseObject is Slider currentSlider)
            {
                // Bonus for repeat sliders until a better per nested object strain system can be achieved.
                Parent.TravelTime = Math.Max(Parent.UnscaledTravelTime / clockRate, MIN_DELTA_TIME);
                Parent.TravelDistance *= Math.Pow(1 + currentSlider.RepeatCount / 2.5, 1.0 / 2.5);
            }

            // // Give some distance from the radius back for longer sliders
            // // Don't do this actually, it breaks normal sliders with many ticks
            // if (!IsTapObject)
            //     LazyJumpDistance = Interpolation.Lerp(LazyJumpDistance, LazyJumpDistance + ASSUMED_SLIDER_RADIUS, LazyJumpDistance / (LazyJumpDistance + ASSUMED_SLIDER_RADIUS));
        }

        private void setTapDistances(double clockRate)
        {
            // We don't need to calculate either angle or distance when one of the last->curr objects is a spinner
            if (BaseObject is Spinner || LastObject is Spinner || !IsTapObject)
            {
                PrevSliderlessJumpDistance = 0;
                PrevTapStrainTime = null;
                PrevSliderlessAngle = null;
                return;
            }

            // We will scale distances by this factor, so we can assume a uniform CircleSize among beatmaps.
            float scalingFactor = NORMALISED_RADIUS / (float)BaseObject.Radius;

            Vector2 currCursorPosition = BaseObject.StackedPosition;
            Vector2 lastCursorPosition = lastTapDifficultyObject?.BaseObject.StackedPosition ?? LastObject.StackedPosition;

            SliderlessJumpDistance = Vector2.Subtract(CursorPosition, lastCursorPosition).Length * scalingFactor;

            if (lastTapDifficultyObject is null)
            {
                PrevSliderlessJumpDistance = 0;
                PrevTapStrainTime = null;
                PrevSliderlessAngle = null;
                return;
            }

            PrevTapStrainTime = lastTapDifficultyObject.TapStrainTime;

            if (lastLastTapDifficultyObject is not null && lastLastTapDifficultyObject.BaseObject is not Spinner)
            {
                // // Calculates angle based on actual object positions
                Vector2 v1 = lastLastTapDifficultyObject.BaseObject.StackedPosition - lastTapDifficultyObject.BaseObject.StackedPosition;
                Vector2 v2 = BaseObject.StackedPosition - lastTapDifficultyObject.BaseObject.StackedPosition;

                OsuDifficultyHitObject? prevObj = lastTapDifficultyObject;
                OsuDifficultyHitObject? prevPrevObj = lastLastTapDifficultyObject;

                // If the current cursor pos is close enough to the previous one
                // Ignore the angle from it and recalc the angle from earlier objects
                // Ensures doubletaps and sliderjumps are treated properly
                // For maps like /b/3455732
                while (v2.Length * scalingFactor < 20 && prevPrevObj is not null)
                {
                    v1 = prevPrevObj.BaseObject.StackedPosition - prevObj.BaseObject.StackedPosition;
                    v2 = BaseObject.StackedPosition - prevObj.BaseObject.StackedPosition;

                    prevObj = prevPrevObj;
                    prevPrevObj = (OsuDifficultyHitObject?)prevPrevObj.PreviousTap(0);
                }

                while (v1.Length * scalingFactor < 20 && prevPrevObj is not null)
                {
                    v1 = prevPrevObj.BaseObject.StackedPosition - prevObj.BaseObject.StackedPosition;
                    PrevTapStrainTime = Math.Max(prevObj.StartTime - prevPrevObj.StartTime, MIN_DELTA_TIME);

                    prevPrevObj = (OsuDifficultyHitObject?)prevPrevObj.PreviousTap(0);
                }

                PrevSliderlessJumpDistance = v1.Length * scalingFactor;
                PrevSliderlessAngle = prevObj.Angle;

                float dot = Vector2.Dot(v1, v2);
                float det = v1.X * v2.Y - v1.Y * v2.X;

                SliderlessAngle = Math.Abs(Math.Atan2(det, dot));
            }
            else
            {
                PrevSliderlessJumpDistance = lastTapDifficultyObject.SliderlessJumpDistance;
                PrevTapStrainTime = lastTapDifficultyObject.TapStrainTime;
                PrevSliderlessAngle = lastTapDifficultyObject.SliderlessAngle;
            }
        }

        private void calculateCursorPosition()
        {
            if (Index == 0 || IsTapObject)
            {
                CursorPosition = BaseObject.StackedPosition;
                return;
            }

            Vector2 nextPosition = BaseObject.StackedPosition;
            Vector2? lazyEndPosition = null;

            if (BaseObject is SliderTailCircle && Parent?.BaseObject is Slider slider)
            {
                double trackingEndTime = Math.Max(
                    // SliderTailCircle always occurs at the final end time of the slider, but the player only needs to hold until within a lenience before it.
                    // This leniency is not scaled by clock rate, it is in the same position regardless of rate.
                    slider.EndTime + SliderEventGenerator.TAIL_LENIENCY,
                    // There's an edge case where one or more ticks/repeats fall within that leniency range.
                    // In such a case, the player needs to track until the final tick or repeat.
                    slider.NestedHitObjects.LastOrDefault(n => n is not SliderTailCircle)?.StartTime ?? double.MinValue
                );

                Parent.UnscaledTravelTime = trackingEndTime - slider.StartTime;

                double endTimeMin = Parent.UnscaledTravelTime / slider.SpanDuration;
                if (endTimeMin % 2 >= 1)
                    endTimeMin = 1 - endTimeMin % 1;
                else
                    endTimeMin %= 1;

                lazyEndPosition = slider.StackedPosition + slider.Path.PositionAt(endTimeMin);
            }

            // Calculates end position based on if the cursor has moved enough from previous end position
            double scalingFactor = NORMALISED_RADIUS / BaseObject.Radius;

            Vector2 lastCursorPosition = lastDifficultyObject?.CursorPosition ?? LastObject.StackedPosition;

            Vector2 currMovement = nextPosition - lastCursorPosition;
            double currMovementLength = currMovement.Length * scalingFactor;

            double requiredMovementLength = ASSUMED_SLIDER_RADIUS;

            if (lazyEndPosition is not null)
            {
                // The end of a slider has special aim rules due to the relaxed time constraint on position.
                // There is both a lazy end position as well as the actual end slider position. We assume the player takes the simpler movement.
                // For sliders that are circular, the lazy end position may actually be farther away than the sliders true end.
                // This code is designed to prevent buffing situations where lazy end is actually a less efficient movement.
                Vector2 lazyMovement = Vector2.Subtract((Vector2)lazyEndPosition, lastCursorPosition);

                if (lazyMovement.Length < currMovement.Length)
                    currMovement = lazyMovement;

                currMovementLength = scalingFactor * currMovement.Length;
            }

            if (currMovementLength > requiredMovementLength)
            {
                // this finds the positional delta from the required radius and the current position, and updates the currCursorPosition accordingly, as well as rewarding distance.
                Vector2 currCursorPosition = Vector2.Add(lastCursorPosition, Vector2.Multiply(currMovement, (float)((currMovementLength - requiredMovementLength) / currMovementLength)));
                CursorPosition = currCursorPosition;
            }
            else
            {
                CursorPosition = lastCursorPosition;
            }
        }

        public DifficultyHitObject PreviousTap(int backwardsIndex)
        {
            if (TapIndex is null)
                return default;

            int index = (int)TapIndex - (backwardsIndex + 1);
            return index >= 0 && index < difficultyTapHitObjects.Count ? difficultyTapHitObjects[index] : default;
        }

        public DifficultyHitObject NextTap(int forwardsIndex)
        {
            if (TapIndex is null)
                return default;

            int index = (int)TapIndex + (forwardsIndex + 1);
            return index >= 0 && index < difficultyTapHitObjects.Count ? difficultyTapHitObjects[index] : default;
        }

        public static bool IsTickFarEnough(OsuHitObject a, OsuHitObject b)
        {
            double scalingFactor = NORMALISED_RADIUS / a.Radius;

            return ASSUMED_SLIDER_RADIUS < Vector2.Subtract(a.StackedPosition, b.StackedPosition).Length * scalingFactor;
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
