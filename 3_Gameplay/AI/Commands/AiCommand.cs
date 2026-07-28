using UnityEngine;

public enum AiCommandKind : byte
{
    SetMove = 0,
    ClearMove = 1,
    RequestSkill = 2,
}

public readonly struct AiCommand
{
    public AiCommand(
        AiCommandKind kind,
        Vector3 direction,
        bool wantsRun,
        string source)
    {
        Kind = kind;
        Direction = direction;
        WantsRun = wantsRun;
        Source = source ?? string.Empty;
        Intent = default;
    }

    public AiCommand(GameplayIntent intent, string source)
    {
        Kind = AiCommandKind.RequestSkill;
        Direction = Vector3.zero;
        WantsRun = false;
        Source = source ?? string.Empty;
        Intent = intent;
    }

    public AiCommandKind Kind { get; }
    public Vector3 Direction { get; }
    public bool WantsRun { get; }
    public string Source { get; }
    public GameplayIntent Intent { get; }
}

public sealed class AiCommandBuffer
{
    bool _hasMovement;
    AiCommand _movement;
    bool _hasSkill;
    AiCommand _skill;

    public int Count => (_hasMovement ? 1 : 0) + (_hasSkill ? 1 : 0);

    public void BeginTick()
    {
        _hasMovement = false;
        _movement = default;
        _hasSkill = false;
        _skill = default;
    }

    public void SetMovement(Vector3 direction, bool wantsRun, string source)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
        }

        _movement = new AiCommand(
            AiCommandKind.SetMove,
            direction,
            wantsRun,
            source);
        _hasMovement = true;
    }

    public void ClearMovement(string source)
    {
        _movement = new AiCommand(
            AiCommandKind.ClearMove,
            Vector3.zero,
            false,
            source);
        _hasMovement = true;
    }

    public bool TryConsumeMovement(out AiCommand command)
    {
        if (!_hasMovement)
        {
            command = default;
            return false;
        }

        command = _movement;
        _hasMovement = false;
        _movement = default;
        return true;
    }

    public void RequestSkill(in GameplayIntent intent, string source)
    {
        if (_hasSkill)
        {
            return;
        }

        _skill = new AiCommand(intent, source);
        _hasSkill = true;
    }

    public bool TryConsumeSkill(out AiCommand command)
    {
        if (!_hasSkill)
        {
            command = default;
            return false;
        }

        command = _skill;
        _hasSkill = false;
        _skill = default;
        return true;
    }
}
