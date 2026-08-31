using System;

/// <summary>Fixed-capacity replay storage for exact step/entity alignment. It never samples by wall-clock time.</summary>
public sealed class AnimationTransitionReplay243
{
    readonly AnimationGameplayTraceRecord[] _gameplay;
    readonly AnimationPresentationTraceRecord243[] _presentation;
    int _gameplayStart;
    int _presentationStart;
    int _gameplayCount;
    int _presentationCount;

    public int GameplayCount => _gameplayCount;
    public int PresentationCount => _presentationCount;
    public int GameplayCapacity => _gameplay.Length;
    public int PresentationCapacity => _presentation.Length;

    public AnimationTransitionReplay243(int gameplayCapacity, int presentationCapacity)
    {
        _gameplay = new AnimationGameplayTraceRecord[Math.Max(1, gameplayCapacity)];
        _presentation = new AnimationPresentationTraceRecord243[Math.Max(1, presentationCapacity)];
    }

    public void AddGameplay(in AnimationGameplayTraceRecord record)
    {
        Add(_gameplay, ref _gameplayStart, ref _gameplayCount, in record);
    }

    public void AddPresentation(in AnimationPresentationTraceRecord243 record)
    {
        Add(_presentation, ref _presentationStart, ref _presentationCount, in record);
    }

    public AnimationGameplayTraceRecord GetGameplayAt(int chronologicalIndex)
    {
        if (chronologicalIndex < 0 || chronologicalIndex >= GameplayCount)
        {
            throw new ArgumentOutOfRangeException(nameof(chronologicalIndex));
        }
        return _gameplay[(_gameplayStart + chronologicalIndex) % _gameplay.Length];
    }

    public bool TryFindFirstGameplayDifference(
        AnimationTransitionReplay243 actual,
        in AnimationGameplayTraceTolerance tolerance,
        out int expectedIndex,
        out int actualIndex,
        out AnimationGameplayTraceDifference difference)
    {
        expectedIndex = -1;
        actualIndex = -1;
        difference = default;
        if (actual == null)
        {
            difference = new AnimationGameplayTraceDifference(
                AnimationGameplayTraceDifferenceKind.Alignment, "ActualReplay", "available", "null");
            return true;
        }

        var paired = Math.Min(GameplayCount, actual.GameplayCount);
        for (var i = 0; i < paired; i++)
        {
            var expected = GetGameplayAt(i);
            var observed = actual.GetGameplayAt(i);
            if (AnimationGameplayTraceComparer.TryFindDifference(in expected, in observed, in tolerance, out difference))
            {
                expectedIndex = i;
                actualIndex = i;
                return true;
            }
        }

        if (GameplayCount != actual.GameplayCount)
        {
            expectedIndex = paired;
            actualIndex = paired;
            difference = new AnimationGameplayTraceDifference(
                AnimationGameplayTraceDifferenceKind.Alignment,
                "GameplayRecordCount",
                GameplayCount.ToString(),
                actual.GameplayCount.ToString());
            return true;
        }

        return false;
    }

    static void Add<T>(T[] buffer, ref int start, ref int count, in T record)
    {
        var index = (start + count) % buffer.Length;
        buffer[index] = record;
        if (count < buffer.Length)
        {
            count++;
            return;
        }
        start = (start + 1) % buffer.Length;
    }
}
