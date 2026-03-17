// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    public enum FlowChunkState { None, Entry, Continuation }

    public class Movement
    {
        /// <summary>
        /// First (primary) movement of the object
        /// </summary>
        public bool PrimaryMovement { get; set; }

        public Vector2 Start { get; set; }
        public double StartTime { get; set; }
        public Vector2 End { get; set; }
        public double EndTime { get; set; }
        public double StartRadius { get; set; }
        public double EndRadius { get; set; }

        public Colour4 DebugColour { get; set; }

        public FlowChunkState FlowChunkState { get; set; }


        public Movement? PreviousMovement { get; set; }
        public Movement? NextMovement { get; set; }

        public double Time => Math.Max(EndTime - StartTime, OsuDifficultyHitObject.MIN_DELTA_TIME);
        public double Distance => (End * (OsuDifficultyHitObject.NORMALISED_RADIUS / (float)Math.Max(StartRadius, EndRadius)) - Start * (OsuDifficultyHitObject.NORMALISED_RADIUS / (float)Math.Max(EndRadius, StartRadius))).Length;

        public override string ToString()
        {
            return $"{Start}->{End} ({Distance:N2}px, {Time:N2}ms)";
        }

        public double Angle(Movement other, bool signed = false)
        {
            Vector2 v1 = other.Start - other.End;
            Vector2 v2 = End - Start;

            float dot = Vector2.Dot(v1, v2);
            float det = v1.X * v2.Y - v1.Y * v2.X;

            double angle = Math.Atan2(det, dot);
            return signed ? angle : Math.Abs(angle);
        }

        public double NormalizedAngleVector(Movement other)
        {
            Vector2 v1 = other.Start - other.End;
            Vector2 v2 = End - Start;

            float dot = Vector2.Dot(v1, v2);
            float det = v1.X * v2.Y - v1.Y * v2.X;

            return Math.Atan2(Math.Abs(v2.Y), Math.Abs(v2.X));
        }

public static void AnnotateFlowChunks(List<Movement> movements, List<DifficultyHitObject> hitObjects)
{
    const double flowThreshold = 0.6;
    const double flowEndThreshold = 0.5;
    const double maxChunkDeltaTime = 350;
    const double maxChunkDuration = 1000;
    const double maxFlowVelocityChange = 0.25;

    bool inFlowChunk = false;
    bool flowColorToggle = false;
    double chunkStartTime = 0;
    double entryFlowDifficulty = 0;

    for (int i = 0; i < movements.Count; i++)
    {
        var movement = movements[i];
        var hitObject = hitObjects[i];

        double pFlow = CalculateFlowProbability(movement, hitObject);
        double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(hitObject, movement);

        bool rhythmBreak = movement.Time > maxChunkDeltaTime;
        bool chunkTooLong = inFlowChunk && (movement.StartTime - chunkStartTime) > maxChunkDuration;
        bool velocityChanged = inFlowChunk && entryFlowDifficulty > 0 &&
                               Math.Abs(flowDifficulty - entryFlowDifficulty) / entryFlowDifficulty > maxFlowVelocityChange;
        bool isFlow = pFlow > flowThreshold && !rhythmBreak;
        bool endChunk = pFlow < flowEndThreshold || rhythmBreak || chunkTooLong || velocityChanged;

        if (isFlow && inFlowChunk && !endChunk)
        {
            movement.FlowChunkState = FlowChunkState.Continuation;
        }
        else if (isFlow && (!inFlowChunk || endChunk))
        {
            movement.FlowChunkState = FlowChunkState.Entry;
            inFlowChunk = true;
            chunkStartTime = movement.StartTime;
            entryFlowDifficulty = flowDifficulty;
            flowColorToggle = !flowColorToggle;
        }
        else
        {
            movement.FlowChunkState = FlowChunkState.None;
            inFlowChunk = false;
        }

        movement.DebugColour = movement.FlowChunkState switch
        {
            FlowChunkState.None => movement.PrimaryMovement ? Colour4.LightGreen : Colour4.White,
            _ => flowColorToggle ? Colour4.HotPink : Colour4.Yellow
        };
    }
}

        public static (double combinedSnap, double flowDifficulty) CalculateFlowProbabilityInputs(Movement movement, DifficultyHitObject hitObject)
        {
            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(hitObject, movement) * 135.0;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(hitObject, movement) * 135.0;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(hitObject, movement) * 265.0;

            double combinedSnap = snapDifficulty + agilityDifficulty;
            return (combinedSnap, flowDifficulty);
        }

        public static double CalculateFlowProbability(Movement movement, DifficultyHitObject hitObject)
        {
            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(hitObject, movement) * 135.0;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(hitObject, movement) * 135.0;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(hitObject, movement) * 265.0;

            double combinedSnap = snapDifficulty + agilityDifficulty;
            return 1 - calculateSnapFlowProbability(flowDifficulty / combinedSnap);
        }

        private static double calculateSnapFlowProbability(double ratio)
        {
            const double k = 7.27;
            if (ratio == 0) return 0;
            if (double.IsNaN(ratio)) return 1;

            return DifficultyCalculationUtils.Logistic(-k * Math.Log(ratio));
        }
    }
}
