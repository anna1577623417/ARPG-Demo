using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 188.3 W8 — CombatObject 生成器（Player 持有）。
/// <para>对外：<see cref="Spawn"/> 创建新 CombatObject；<see cref="Tick"/> 每帧推进所有 Active。</para>
/// <para>OnExpire 嵌套生成链深 ≤ 3，防递归炸栈（§9 铁律 7）。</para>
/// </summary>
public sealed class CombatObjectSpawner
{
    public const int MaxOnExpireDepth = 3;
    const int OverlapBufferSize = 32;

    readonly List<CombatObjectRuntime> _active = new(16);
    readonly Stack<CombatObjectRuntime> _pool = new(16);
    readonly Collider[] _scratch = new Collider[OverlapBufferSize];

    public int ActiveCount => _active.Count;
    public IReadOnlyList<CombatObjectRuntime> Active => _active;

    /// <summary>生成 CombatObject。返回 null 表示拒绝（无效定义 / 递归过深）。</summary>
    public CombatObjectRuntime Spawn(
        CombatObjectDefinitionSO def,
        Entity source,
        Vector3 spawnPos,
        Quaternion spawnRot,
        int onExpireDepth = 0)
    {
        if (def == null) return null;
        if (!def.IsValid(out var reason))
        {
            CombatHitDiagProbe.LogReject(reason, def);
            Debug.LogWarning($"[CombatObject] Spawn rejected: {def.name} invalid ({reason})");
            return null;
        }
        if (onExpireDepth > MaxOnExpireDepth)
        {
            var depthReason = $"OnExpire depth>{MaxOnExpireDepth}";
            CombatHitDiagProbe.LogReject(depthReason, def);
            Debug.LogWarning($"[CombatObject] OnExpire 链深超过 {MaxOnExpireDepth}；终止递归（def={def.name}）");
            return null;
        }

        var co = _pool.Count > 0 ? _pool.Pop() : new CombatObjectRuntime();
        co.Initialize(def, source, spawnPos, spawnRot);
        _active.Add(co);
        CombatHitDiagProbe.LogSpawn(def, source, spawnPos);
        return co;
    }

    /// <summary>每帧推进。Player 在 Update / FixedUpdate 调用。</summary>
    public void Tick(float deltaTime)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var co = _active[i];
            co.Tick(deltaTime, _scratch);
            if (!co.IsExpired) continue;

            // OnExpire 嵌套
            var nestedDef = co.Definition.Lifecycle.OnExpireSpawn;
            if (nestedDef != null)
            {
                Spawn(nestedDef, co.Source, co.CurrentWorldPos, co.SpawnWorldRot, 1);
            }

            _active.RemoveAt(i);
            _pool.Push(co);
        }
    }

    /// <summary>清空所有 Active（场景切换 / 死亡）。</summary>
    public void Clear()
    {
        for (var i = 0; i < _active.Count; i++)
        {
            _pool.Push(_active[i]);
        }
        _active.Clear();
    }
}
