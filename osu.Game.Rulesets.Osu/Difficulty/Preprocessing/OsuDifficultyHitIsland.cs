// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    public class OsuDifficultyHitIsland
    {
        public double DeltaTime { get; }
        public double? StartDeltaTime { get; }
        public int Length { get; private set; }
        public int Polarity => Length % 2;

        public readonly int Index;

        public OsuDifficultyHitObject FirstObject { get; private set; }

        public OsuDifficultyHitObject LastObject { get; private set; }

        private readonly List<OsuDifficultyHitIsland> islands;

        public OsuDifficultyHitIsland(double deltaTime, double? startDeltaTime, OsuDifficultyHitObject firstObject, OsuDifficultyHitObject lastObject, int length, int index, List<OsuDifficultyHitIsland> islands)
        {
            DeltaTime = deltaTime;
            StartDeltaTime = startDeltaTime;
            FirstObject = firstObject;
            LastObject = lastObject;
            Length = length;
            Index = index;
            this.islands = islands;
        }

        internal bool TryExtend(OsuDifficultyHitObject nextObject, double tolerance)
        {
            double nextDeltaTime = nextObject.StartTime - LastObject.StartTime;

            if (Math.Abs(nextDeltaTime - DeltaTime) > tolerance)
                return false;

            LastObject = nextObject;
            Length++;
            return true;
        }

        public OsuDifficultyHitIsland? Previous(int backwardsIndex)
        {
            int index = Index - (backwardsIndex + 1);
            return index >= 0 && index < islands.Count ? islands[index] : null;
        }

        public OsuDifficultyHitIsland? Next(int forwardsIndex)
        {
            int index = Index + (forwardsIndex + 1);
            return index >= 0 && index < islands.Count ? islands[index] : null;
        }

        internal void Shrink(OsuDifficultyHitObject newLastObject)
        {
            LastObject = newLastObject;
            Length--;
        }
    }

    public class OsuDifficultyHitIslandBuilder
    {
        private readonly List<OsuDifficultyHitIsland> islands = new List<OsuDifficultyHitIsland>();

        private OsuDifficultyHitIsland? currentIsland;
        private OsuDifficultyHitObject? previousNote;

public void Process(OsuDifficultyHitObject obj, bool isSpinner)
{
    if (isSpinner)
    {
        currentIsland = null;
        previousNote = null;
        return;
    }

    if (previousNote == null)
    {
        previousNote = obj;
        return;
    }

    double tolerance = obj.HitWindowGreat / 4;

    if (currentIsland != null && currentIsland.TryExtend(obj, tolerance))
    {
        obj.Island = currentIsland;
    }
    else if (currentIsland == null)
    {
        double deltaTime = obj.StartTime - previousNote.StartTime;

        currentIsland = new OsuDifficultyHitIsland(deltaTime, null, previousNote, obj, length: 2, islands.Count, islands);
        islands.Add(currentIsland);

        previousNote.Island = currentIsland;
        obj.Island = currentIsland;
    }
    else
    {
        double deltaTime = obj.StartTime - previousNote.StartTime;
        double oldIslandDeltaTime = currentIsland.DeltaTime;

        if (deltaTime < oldIslandDeltaTime)
        {
            if (currentIsland.Length == 1)
            {
                islands.RemoveAt(islands.Count - 1);
            }
            else
            {
                var secondToLast = (OsuDifficultyHitObject)previousNote.Previous(0)!;
                currentIsland.Shrink(secondToLast);
            }

            currentIsland = new OsuDifficultyHitIsland(deltaTime, oldIslandDeltaTime, previousNote, obj, length: 2, islands.Count, islands);
            islands.Add(currentIsland);

            previousNote.Island = currentIsland;
            obj.Island = currentIsland;
        }
        else
        {
            currentIsland = new OsuDifficultyHitIsland(deltaTime, deltaTime, obj, obj, length: 1, islands.Count, islands);
            islands.Add(currentIsland);

            obj.Island = currentIsland;
        }
    }

    previousNote = obj;
}
    }
}
