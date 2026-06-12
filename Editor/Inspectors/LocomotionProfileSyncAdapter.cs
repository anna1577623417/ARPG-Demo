#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// LocomotionProfile 专用 Authority→Target 校验 / 同步（160.2 L1–L3）。
/// Authority = <see cref="LocomotionProfile.EnabledStates"/>；Target = bindings[]。
/// </summary>
public static class LocomotionProfileSyncAdapter
{
    const string LogPrefix = "[LocoProfileSync]";

    public sealed class Report
    {
        public List<string> MissingSimple { get; } = new List<string>();
        public List<string> MissingStrafe { get; } = new List<string>();
        public List<string> MissingTurn { get; } = new List<string>();
        public List<string> Duplicate { get; } = new List<string>();
        public List<string> Unused { get; } = new List<string>();

        public int ExpectedBindingCount { get; set; }
        public int ActualBindingCount { get; set; }
        public bool HasCountMismatch => ActualBindingCount != ExpectedBindingCount;

        public bool HasMissing =>
            MissingSimple.Count > 0 || MissingStrafe.Count > 0 || MissingTurn.Count > 0;

        public bool HasErrors => Duplicate.Count > 0 || HasCountMismatch || HasMissing;
        public bool HasWarnings => Unused.Count > 0;
        public bool IsClean => !HasErrors && !HasWarnings;

        public string ToSummary()
        {
            if (IsClean)
            {
                return "OK — Authority 与 Bindings 一致。";
            }

            var sb = new StringBuilder(256);
            if (MissingSimple.Count > 0)
            {
                sb.Append("Missing(Simple): ").Append(string.Join(", ", MissingSimple)).Append('\n');
            }

            if (MissingStrafe.Count > 0)
            {
                sb.Append("Missing(Strafe): ").Append(string.Join(", ", MissingStrafe)).Append('\n');
            }

            if (MissingTurn.Count > 0)
            {
                sb.Append("Missing(Turn): ").Append(string.Join(", ", MissingTurn)).Append('\n');
            }

            if (Duplicate.Count > 0)
            {
                sb.Append("Duplicate: ").Append(string.Join(", ", Duplicate)).Append('\n');
            }

            if (Unused.Count > 0)
            {
                sb.Append("Unused: ").Append(string.Join(", ", Unused)).Append('\n');
            }

            if (HasCountMismatch)
            {
                sb.Append($"CountMismatch: {ActualBindingCount} / {ExpectedBindingCount}");
            }

            return sb.ToString().TrimEnd('\n');
        }
    }

    /// <summary>Authority 展开后的期望 Binding 行数（随 Enabled States 变化）。</summary>
    public static int GetExpectedBindingCount(LocomotionProfile profile) =>
        profile == null ? 0 : BuildAuthorityBindingKeys(profile.EnabledStates).Count;

    /// <summary>期望行数拆分（Simple + Turn 4 + Strafe 8×2）。</summary>
    public static string FormatExpectedBindingBreakdown(LocomotionProfile profile)
    {
        if (profile == null)
        {
            return "0";
        }

        var enabled = profile.EnabledStates;
        var simple = 0;
        var turn = 0;
        var strafe = 0;
        foreach (var flag in LocomotionStateFlagExtensions.InspectorMenuOrder)
        {
            if (!flag.IsEnabledIn(enabled))
            {
                continue;
            }

            var id = flag.ToId();
            if (id == LocomotionStateId.StrafeLocomotion)
            {
                strafe = StrafeDirections.Length * StrafeRunRequirements.Length;
            }
            else if (id == LocomotionStateId.TurnInPlaceDirected)
            {
                turn = TurnDirections.Length;
            }
            else if (id != LocomotionStateId.None && !IsCompositeAuthority(id))
            {
                simple++;
            }
        }

        var total = simple + turn + strafe;
        return $"Simple {simple} + Turn {turn} + Strafe {strafe} = {total}";
    }

    readonly struct SimpleKey : IEquatable<SimpleKey>
    {
        public readonly LocomotionStateId State;
        public SimpleKey(LocomotionStateId state) => State = state;
        public bool Equals(SimpleKey other) => State == other.State;
        public override bool Equals(object obj) => obj is SimpleKey other && Equals(other);
        public override int GetHashCode() => (int)State;
    }

    readonly struct StrafeKey : IEquatable<StrafeKey>
    {
        public readonly StrafeDirection8 Direction;
        public readonly LocomotionRunRequirement RunRequirement;
        public StrafeKey(StrafeDirection8 direction, LocomotionRunRequirement runRequirement)
        {
            Direction = direction;
            RunRequirement = runRequirement;
        }

        public bool Equals(StrafeKey other) =>
            Direction == other.Direction && RunRequirement == other.RunRequirement;

        public override bool Equals(object obj) => obj is StrafeKey other && Equals(other);

        public override int GetHashCode() => ((int)Direction << 4) ^ (int)RunRequirement;

        public override string ToString() => $"{Direction}/{RunRequirement}";
    }

    readonly struct TurnKey : IEquatable<TurnKey>
    {
        public readonly TurnDirection4 Direction;
        public TurnKey(TurnDirection4 direction) => Direction = direction;
        public bool Equals(TurnKey other) => Direction == other.Direction;
        public override bool Equals(object obj) => obj is TurnKey other && Equals(other);
        public override int GetHashCode() => (int)Direction;
        public override string ToString() => Direction.ToString();
    }

    readonly struct BindingRowKey : IEquatable<BindingRowKey>
    {
        public readonly LocomotionStateId State;
        public readonly StrafeDirection8 StrafeDirection;
        public readonly TurnDirection4 TurnDirection;
        public readonly LocomotionRunRequirement RunRequirement;

        public BindingRowKey(LocomotionStateBinding binding)
        {
            State = binding.State;
            StrafeDirection = binding.StrafeDirection;
            TurnDirection = binding.TurnDirection;
            RunRequirement = binding.RunRequirement;
        }

        public bool Equals(BindingRowKey other) =>
            State == other.State
            && StrafeDirection == other.StrafeDirection
            && TurnDirection == other.TurnDirection
            && RunRequirement == other.RunRequirement;

        public override bool Equals(object obj) => obj is BindingRowKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)State;
                hash = (hash * 397) ^ (int)StrafeDirection;
                hash = (hash * 397) ^ (int)TurnDirection;
                hash = (hash * 397) ^ (int)RunRequirement;
                return hash;
            }
        }

        public string ToDisplayString()
        {
            if (State == LocomotionStateId.StrafeLocomotion)
            {
                return $"Strafe {StrafeDirection}/{RunRequirement}";
            }

            if (State == LocomotionStateId.TurnInPlaceDirected)
            {
                return $"Turn {TurnDirection}";
            }

            if (IsSimpleRow(this))
            {
                return State.ToString();
            }

            return $"{State} (dir={StrafeDirection}, turn={TurnDirection}, run={RunRequirement})";
        }
    }

    static readonly SimpleKeyComparer SimpleComparer = new SimpleKeyComparer();
    static readonly StrafeKeyComparer StrafeComparer = new StrafeKeyComparer();
    static readonly TurnKeyComparer TurnComparer = new TurnKeyComparer();
    static readonly BindingRowKeyComparer BindingRowComparer = new BindingRowKeyComparer();

    static readonly StrafeDirection8[] StrafeDirections =
    {
        StrafeDirection8.Forward,
        StrafeDirection8.ForwardLeft,
        StrafeDirection8.ForwardRight,
        StrafeDirection8.Backward,
        StrafeDirection8.BackwardLeft,
        StrafeDirection8.BackwardRight,
        StrafeDirection8.Left,
        StrafeDirection8.Right,
    };

    static readonly LocomotionRunRequirement[] StrafeRunRequirements =
    {
        LocomotionRunRequirement.WalkOnly,
        LocomotionRunRequirement.RunOnly,
    };

    static readonly TurnDirection4[] TurnDirections =
    {
        TurnDirection4.Left90,
        TurnDirection4.Right90,
        TurnDirection4.Left180,
        TurnDirection4.Right180,
    };

    public static Report Validate(LocomotionProfile profile)
    {
        var report = new Report();
        if (profile == null)
        {
            return report;
        }

        AnalyzeBindingsAgainstAuthority(profile.EnabledStates, profile.EditorGetBindingsCopy(), report);
        return report;
    }

    public static string BuildSummary(LocomotionProfile profile) => Validate(profile).ToSummary();

    /// <summary>AutoFix（Reconcile）结果摘要。</summary>
    public struct AutoFixResult
    {
        public int AddedRows;
        public int AddedSimple;
        public int AddedStrafe;
        public int AddedTurn;
        public int RemovedUnused;
        public int RemovedDuplicates;
        public int RemovedInvalid;
        public int NormalizedRows;
        public int ClearedIllegalAssets;
        public bool SanitizedEnabledStates;
        public bool Reordered;
        public int ExpectedCount;
        public bool Converged;

        public bool AnyChange =>
            AddedRows > 0 || RemovedUnused > 0 || RemovedDuplicates > 0 || RemovedInvalid > 0
            || NormalizedRows > 0 || ClearedIllegalAssets > 0 || SanitizedEnabledStates || Reordered;
    }

    /// <summary>
    /// 一键 Reconcile：剥离废弃 Enabled 位 → 删非 Authority 行 → 去重 → 补全 → 规范化 → 按菜单序重排。
    /// </summary>
    public static AutoFixResult AutoFix(LocomotionProfile profile) => ReconcileBindings(profile);

    public static AutoFixResult ReconcileBindings(LocomotionProfile profile)
    {
        var result = new AutoFixResult();
        if (profile == null)
        {
            return result;
        }

        result.SanitizedEnabledStates = profile.EditorSanitizeEnabledStates();

        var enabled = profile.EnabledStates;
        var authorityKeys = BuildAuthorityBindingKeys(enabled);
        var authoritySet = new HashSet<BindingRowKey>(authorityKeys, BindingRowComparer);
        result.ExpectedCount = authorityKeys.Count;

        var list = new List<LocomotionStateBinding>(profile.EditorGetBindingsCopy());
        var orderBefore = CaptureOrderSignature(list);

        for (var pass = 0; pass < 3; pass++)
        {
            result.RemovedUnused += RemoveRowsNotInAuthority(list, authoritySet);
            result.RemovedInvalid += RemoveInvalidCompositeRows(list);
            result.RemovedDuplicates += RemoveDuplicateRowsKeepBestAsset(list);

            for (var i = 0; i < authorityKeys.Count; i++)
            {
                if (TryFindBindingIndex(list, authorityKeys[i], out _))
                {
                    continue;
                }

                list.Add(CreateDefaultForKey(authorityKeys[i]));
                CountAddedRow(ref result, authorityKeys[i]);
            }

            if (list.Count == authorityKeys.Count && !HasDuplicateKeys(list))
            {
                break;
            }
        }

        result.NormalizedRows = NormalizeAuthorityRows(list, authoritySet, out var clearedAssets);
        result.ClearedIllegalAssets = clearedAssets;

        result.RemovedDuplicates += RemoveDuplicateRowsKeepBestAsset(list);
        result.RemovedInvalid += RemoveInvalidCompositeRows(list);
        result.RemovedUnused += RemoveRowsNotInAuthority(list, authoritySet);

        for (var i = 0; i < authorityKeys.Count; i++)
        {
            if (TryFindBindingIndex(list, authorityKeys[i], out _))
            {
                continue;
            }

            list.Add(CreateDefaultForKey(authorityKeys[i]));
            CountAddedRow(ref result, authorityKeys[i]);
        }

        result.Reordered = RebuildBindingsInAuthorityOrder(list, enabled);
        if (!result.Reordered)
        {
            result.Reordered = orderBefore != CaptureOrderSignature(list);
        }

        profile.EditorSetBindings(list.ToArray());
        result.Converged = Validate(profile).IsClean;

        Log($"Reconcile simple+{result.AddedSimple} strafe+{result.AddedStrafe} turn+{result.AddedTurn} " +
            $"unused-{result.RemovedUnused} dup-{result.RemovedDuplicates} invalid-{result.RemovedInvalid} " +
            $"norm={result.NormalizedRows} count={list.Count}/{result.ExpectedCount} converged={result.Converged}");

        return result;
    }

    static void CountAddedRow(ref AutoFixResult result, BindingRowKey key)
    {
        result.AddedRows++;
        if (key.State == LocomotionStateId.StrafeLocomotion)
        {
            result.AddedStrafe++;
        }
        else if (key.State == LocomotionStateId.TurnInPlaceDirected)
        {
            result.AddedTurn++;
        }
        else
        {
            result.AddedSimple++;
        }
    }

    /// <summary>
    /// 仅按 Enabled States 菜单顺序重排现有 Binding 行；不增删、不规范化、不碰 Clip/Action。
    /// </summary>
    /// <returns>顺序是否发生变化。</returns>
    public static bool SortBindingsByEnabledStates(LocomotionProfile profile)
    {
        if (profile == null)
        {
            return false;
        }

        var list = new List<LocomotionStateBinding>(profile.EditorGetBindingsCopy());
        if (list.Count <= 1)
        {
            return false;
        }

        // 按菜单全序重排现有行（不要求该 Flag 已勾选；与 AutoFix 的「仅 enabled 槽位」区分）。
        var reordered = RebuildBindingsInAuthorityOrder(list, profile.EnabledStates, onlyEnabledSlots: false);
        if (!reordered)
        {
            return false;
        }

        profile.EditorSetBindings(list.ToArray());
        Log("SortByEnabledStates reordered bindings by InspectorMenuOrder");
        return true;
    }

    /// <summary>Sync 主按钮：仅追加缺失 Simple 行；不碰 Strafe/Turn Composite。</summary>
    public static int SyncSimpleBindings(LocomotionProfile profile, out string detail)
    {
        detail = string.Empty;
        if (profile == null)
        {
            return 0;
        }

        var before = Validate(profile);
        var authority = CollectSimpleAuthority(profile.EnabledStates);
        var list = new List<LocomotionStateBinding>(profile.EditorGetBindingsCopy());
        var orderBefore = CaptureOrderSignature(list);
        var added = AuthorityTargetListSync.SyncAppendMissing(
            list,
            authority,
            CreateDefaultSimple,
            row => new SimpleKey(row.State),
            SimpleComparer);

        var reordered = RebuildBindingsInAuthorityOrder(list, profile.EnabledStates);
        if (added > 0 || reordered || CaptureOrderSignature(list) != orderBefore)
        {
            profile.EditorSetBindings(list.ToArray());
            detail = added > 0 ? string.Join(", ", before.MissingSimple) : string.Empty;
            Log(added > 0
                ? $"Sync added {added} binding(s): {detail}"
                : "Sync reordered bindings by Enabled States");
        }

        return added;
    }

    /// <summary>Authority Key 集合 vs 现有 Bindings（含 Strafe 方向×走跑、Turn 四向子键）。</summary>
    static void AnalyzeBindingsAgainstAuthority(
        LocomotionStateFlag enabled,
        IReadOnlyList<LocomotionStateBinding> bindings,
        Report report)
    {
        var authorityKeys = BuildAuthorityBindingKeys(enabled);
        var authoritySet = new HashSet<BindingRowKey>(authorityKeys, BindingRowComparer);
        report.ExpectedBindingCount = authorityKeys.Count;
        report.ActualBindingCount = bindings?.Count ?? 0;

        var counts = new Dictionary<BindingRowKey, int>(BindingRowComparer);
        if (bindings != null)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                var row = bindings[i];
                if (row.State == LocomotionStateId.None || row.State.IsObsoleteLocomotionState())
                {
                    continue;
                }

                var key = new BindingRowKey(row);
                if (counts.TryGetValue(key, out var count))
                {
                    counts[key] = count + 1;
                }
                else
                {
                    counts[key] = 1;
                }
            }
        }

        for (var i = 0; i < authorityKeys.Count; i++)
        {
            var key = authorityKeys[i];
            if (!counts.TryGetValue(key, out var count) || count == 0)
            {
                AppendMissingKey(report, key);
            }
        }

        foreach (var pair in counts)
        {
            if (!authoritySet.Contains(pair.Key))
            {
                var label = pair.Key.ToDisplayString();
                if (pair.Value > 1)
                {
                    label += $" ×{pair.Value}";
                }

                report.Unused.Add(label);
                continue;
            }

            if (pair.Value > 1)
            {
                report.Duplicate.Add($"{pair.Key.ToDisplayString()} ×{pair.Value}");
            }
        }
    }

    static void AppendMissingKey(Report report, BindingRowKey key)
    {
        if (key.State == LocomotionStateId.StrafeLocomotion)
        {
            report.MissingStrafe.Add($"{key.StrafeDirection}/{key.RunRequirement}");
            return;
        }

        if (key.State == LocomotionStateId.TurnInPlaceDirected)
        {
            report.MissingTurn.Add(key.TurnDirection.ToString());
            return;
        }

        report.MissingSimple.Add(key.State.ToString());
    }

    static bool HasDuplicateKeys(IReadOnlyList<LocomotionStateBinding> list)
    {
        var seen = new HashSet<BindingRowKey>(BindingRowComparer);
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].State == LocomotionStateId.None)
            {
                continue;
            }

            if (!seen.Add(new BindingRowKey(list[i])))
            {
                return true;
            }
        }

        return false;
    }

    static List<BindingRowKey> BuildAuthorityBindingKeys(LocomotionStateFlag enabled)
    {
        var keys = new List<BindingRowKey>();
        foreach (var flag in BuildAuthorityFlagOrder())
        {
            if (!flag.IsEnabledIn(enabled))
            {
                continue;
            }

            var id = flag.ToId();
            if (id == LocomotionStateId.None)
            {
                continue;
            }

            if (id == LocomotionStateId.StrafeLocomotion)
            {
                for (var d = 0; d < StrafeDirections.Length; d++)
                {
                    for (var r = 0; r < StrafeRunRequirements.Length; r++)
                    {
                        keys.Add(new BindingRowKey(new LocomotionStateBinding
                        {
                            State = LocomotionStateId.StrafeLocomotion,
                            StrafeDirection = StrafeDirections[d],
                            RunRequirement = StrafeRunRequirements[r],
                        }));
                    }
                }

                continue;
            }

            if (id == LocomotionStateId.TurnInPlaceDirected)
            {
                for (var t = 0; t < TurnDirections.Length; t++)
                {
                    keys.Add(new BindingRowKey(new LocomotionStateBinding
                    {
                        State = LocomotionStateId.TurnInPlaceDirected,
                        TurnDirection = TurnDirections[t],
                    }));
                }

                continue;
            }

            if (IsCompositeAuthority(id))
            {
                continue;
            }

            keys.Add(new BindingRowKey(new LocomotionStateBinding { State = id }));
        }

        return keys;
    }

    static int RemoveRowsNotInAuthority(
        List<LocomotionStateBinding> list,
        HashSet<BindingRowKey> authoritySet)
    {
        var removed = 0;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var row = list[i];
            if (row.State == LocomotionStateId.None
                || row.State.IsObsoleteLocomotionState()
                || !authoritySet.Contains(new BindingRowKey(row)))
            {
                list.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    static bool TryFindBindingIndex(List<LocomotionStateBinding> list, BindingRowKey key, out int index)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (new BindingRowKey(list[i]).Equals(key))
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    static LocomotionStateBinding CreateDefaultForKey(BindingRowKey key)
    {
        if (key.State == LocomotionStateId.StrafeLocomotion)
        {
            return CreateDefaultStrafe(new StrafeKey(key.StrafeDirection, key.RunRequirement));
        }

        if (key.State == LocomotionStateId.TurnInPlaceDirected)
        {
            return CreateDefaultTurn(new TurnKey(key.TurnDirection));
        }

        return CreateDefaultSimple(key.State);
    }

    static int NormalizeAuthorityRows(
        List<LocomotionStateBinding> list,
        HashSet<BindingRowKey> authoritySet,
        out int clearedIllegalAssets)
    {
        clearedIllegalAssets = 0;
        var normalized = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var key = new BindingRowKey(list[i]);
            if (!authoritySet.Contains(key))
            {
                continue;
            }

            var row = list[i];
            if (NormalizeBindingRow(ref row, key, out var cleared))
            {
                list[i] = row;
                normalized++;
            }

            clearedIllegalAssets += cleared;
        }

        return normalized;
    }

    static bool NormalizeBindingRow(ref LocomotionStateBinding row, BindingRowKey key, out int clearedAssets)
    {
        clearedAssets = 0;
        var changed = false;

        if (row.State != key.State)
        {
            row.State = key.State;
            changed = true;
        }

        if (row.StrafeDirection != key.StrafeDirection)
        {
            row.StrafeDirection = key.StrafeDirection;
            changed = true;
        }

        if (row.TurnDirection != key.TurnDirection)
        {
            row.TurnDirection = key.TurnDirection;
            changed = true;
        }

        if (row.RunRequirement != key.RunRequirement)
        {
            row.RunRequirement = key.RunRequirement;
            changed = true;
        }

        var fallback = GetDefaultFallback(row.State);
        if (row.FallbackState != fallback)
        {
            row.FallbackState = fallback;
            changed = true;
        }

        if (row.Speed < 0.001f)
        {
            row.Speed = 1f;
            changed = true;
        }

        if (row.TransitionDuration < 0.0001f)
        {
            row.TransitionDuration = 0.08f;
            changed = true;
        }

        if (row.LocomotionAction != null)
        {
            if (row.StripLegacyAssetRefsWhenLocomotionActionSet())
            {
                clearedAssets++;
                changed = true;
            }
        }
        else if (row.State.IsContinuous() && row.DiscreteAction != null)
        {
            row.DiscreteAction = null;
            clearedAssets++;
            changed = true;
        }
        else if (row.State.IsDiscrete() && row.ContinuousClip != null)
        {
            row.ContinuousClip = null;
            clearedAssets++;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// 按 <see cref="LocomotionStateFlagExtensions.InspectorMenuOrder"/> 重建列表。
    /// </summary>
    /// <param name="onlyEnabledSlots">
    /// true（AutoFix）：只处理 EnabledStates 已勾选的槽位，其余行留到末尾；
    /// false（Sort 按钮）：凡列表中存在的行都按菜单序插入。
    /// </param>
    static bool RebuildBindingsInAuthorityOrder(
        List<LocomotionStateBinding> list,
        LocomotionStateFlag enabled,
        bool onlyEnabledSlots = true)
    {
        if (list == null || list.Count == 0)
        {
            return false;
        }

        var sigBefore = CaptureOrderSignature(list);
        var remaining = new List<LocomotionStateBinding>(list);
        var ordered = new List<LocomotionStateBinding>(remaining.Count);

        foreach (var flag in BuildAuthorityFlagOrder())
        {
            if (onlyEnabledSlots && !flag.IsEnabledIn(enabled))
            {
                continue;
            }

            var id = flag.ToId();
            if (id == LocomotionStateId.None)
            {
                continue;
            }

            if (id == LocomotionStateId.StrafeLocomotion)
            {
                AppendStrafeRowsInOrder(remaining, ordered);
                continue;
            }

            if (id == LocomotionStateId.TurnInPlaceDirected)
            {
                AppendTurnRowsInOrder(remaining, ordered);
                continue;
            }

            TryTakeFirstSimpleRow(remaining, id, ordered);
        }

        if (remaining.Count > 0)
        {
            remaining.Sort((a, b) => CompareLeftoverRows(a, b, enabled));
            ordered.AddRange(remaining);
        }

        list.Clear();
        list.AddRange(ordered);
        return sigBefore != CaptureOrderSignature(list);
    }

    static void TryTakeFirstSimpleRow(
        List<LocomotionStateBinding> remaining,
        LocomotionStateId id,
        List<LocomotionStateBinding> ordered)
    {
        for (var i = 0; i < remaining.Count; i++)
        {
            if (remaining[i].State != id)
            {
                continue;
            }

            ordered.Add(remaining[i]);
            remaining.RemoveAt(i);
            return;
        }
    }

    static void AppendStrafeRowsInOrder(
        List<LocomotionStateBinding> remaining,
        List<LocomotionStateBinding> ordered)
    {
        for (var d = 0; d < StrafeDirections.Length; d++)
        {
            for (var r = 0; r < StrafeRunRequirements.Length; r++)
            {
                var dir = StrafeDirections[d];
                var run = StrafeRunRequirements[r];
                for (var i = 0; i < remaining.Count; i++)
                {
                    var row = remaining[i];
                    if (row.State != LocomotionStateId.StrafeLocomotion
                        || row.StrafeDirection != dir
                        || row.RunRequirement != run)
                    {
                        continue;
                    }

                    ordered.Add(row);
                    remaining.RemoveAt(i);
                    break;
                }
            }
        }
    }

    static void AppendTurnRowsInOrder(
        List<LocomotionStateBinding> remaining,
        List<LocomotionStateBinding> ordered)
    {
        for (var t = 0; t < TurnDirections.Length; t++)
        {
            var dir = TurnDirections[t];
            for (var i = 0; i < remaining.Count; i++)
            {
                var row = remaining[i];
                if (row.State != LocomotionStateId.TurnInPlaceDirected || row.TurnDirection != dir)
                {
                    continue;
                }

                ordered.Add(row);
                remaining.RemoveAt(i);
                break;
            }
        }
    }

    static int CompareLeftoverRows(
        LocomotionStateBinding a,
        LocomotionStateBinding b,
        LocomotionStateFlag enabled)
    {
        var orderA = GetLeftoverSortKey(a, enabled);
        var orderB = GetLeftoverSortKey(b, enabled);
        var cmp = orderA.CompareTo(orderB);
        return cmp != 0 ? cmp : ((int)a.State).CompareTo((int)b.State);
    }

    static int GetLeftoverSortKey(LocomotionStateBinding row, LocomotionStateFlag enabled)
    {
        if (row.State == LocomotionStateId.None)
        {
            return int.MaxValue;
        }

        if (IsObsoleteId(row.State))
        {
            return 900_000 + (int)row.State;
        }

        var flag = row.State.ToFlag();
        if (flag != LocomotionStateFlag.None && !IsObsoleteFlag(flag))
        {
            var order = BuildAuthorityFlagOrder();
            for (var i = 0; i < order.Count; i++)
            {
                if (order[i] == flag)
                {
                    return 800_000 + i;
                }
            }
        }

        return 850_000 + (int)row.State;
    }

    static List<LocomotionStateFlag> BuildAuthorityFlagOrder()
    {
        return new List<LocomotionStateFlag>(LocomotionStateFlagExtensions.InspectorMenuOrder);
    }

    static bool IsObsoleteFlag(LocomotionStateFlag flag)
    {
#pragma warning disable CS0618
        return flag == LocomotionStateFlag.Move
               || flag == LocomotionStateFlag.Air
               || flag == LocomotionStateFlag.Stop
               || flag == LocomotionStateFlag.RunStartLegacy
               || flag == LocomotionStateFlag.TurnInPlace
               || flag == LocomotionStateFlag.StrafeLeft
               || flag == LocomotionStateFlag.StrafeRight
               || flag == LocomotionStateFlag.BackWalk;
#pragma warning restore
    }

    static string CaptureOrderSignature(IReadOnlyList<LocomotionStateBinding> list)
    {
        if (list == null || list.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(list.Count * 16);
        for (var i = 0; i < list.Count; i++)
        {
            var key = new BindingRowKey(list[i]);
            sb.Append((int)key.State).Append(':')
                .Append((int)key.StrafeDirection).Append(':')
                .Append((int)key.TurnDirection).Append(':')
                .Append((int)key.RunRequirement).Append('|');
        }

        return sb.ToString();
    }

    /// <summary>Remove Unused：须由 Editor 先 DisplayDialog 确认。</summary>
    public static int RemoveUnusedBindings(LocomotionProfile profile)
    {
        if (profile == null)
        {
            return 0;
        }

        var list = new List<LocomotionStateBinding>(profile.EditorGetBindingsCopy());
        var before = list.Count;
        list.RemoveAll(row => IsUnusedRow(profile, row));
        var removed = before - list.Count;
        if (removed > 0)
        {
            profile.EditorSetBindings(list.ToArray());
            Log($"RemoveUnused removed {removed} binding(s)");
        }

        return removed;
    }

    public static int ExpandStrafeTemplates(LocomotionProfile profile)
    {
        if (profile == null || !profile.HasState(LocomotionStateFlag.StrafeLocomotion))
        {
            return 0;
        }

        var list = new List<LocomotionStateBinding>(profile.EditorGetBindingsCopy());
        RemoveInvalidCompositeRows(list);
        RemoveDuplicateRows(list);
        var added = ExpandStrafeTemplatesInternal(list);
        var reordered = RebuildBindingsInAuthorityOrder(list, profile.EnabledStates);
        if (added > 0 || reordered)
        {
            profile.EditorSetBindings(list.ToArray());
            Log($"ExpandStrafe added {added} template row(s) reordered={reordered}");
        }

        return added;
    }

    public static int ExpandTurnTemplates(LocomotionProfile profile)
    {
        if (profile == null || !profile.HasState(LocomotionStateFlag.TurnInPlaceDirected))
        {
            return 0;
        }

        var list = new List<LocomotionStateBinding>(profile.EditorGetBindingsCopy());
        RemoveInvalidCompositeRows(list);
        RemoveDuplicateRows(list);
        var added = ExpandTurnTemplatesInternal(list);
        var reordered = RebuildBindingsInAuthorityOrder(list, profile.EnabledStates);
        if (added > 0 || reordered)
        {
            profile.EditorSetBindings(list.ToArray());
            Log($"ExpandTurn added {added} template row(s) reordered={reordered}");
        }

        return added;
    }

    static bool IsUnusedRow(LocomotionProfile profile, LocomotionStateBinding row)
    {
        if (row.State.IsObsoleteLocomotionState())
        {
            return true;
        }

        var authoritySet = new HashSet<BindingRowKey>(
            BuildAuthorityBindingKeys(profile.EnabledStates),
            BindingRowComparer);
        return !authoritySet.Contains(new BindingRowKey(row));
    }

    static List<SimpleKey> CollectSimpleAuthority(LocomotionStateFlag enabled)
    {
        var list = new List<SimpleKey>();
        foreach (var flag in BuildAuthorityFlagOrder())
        {
            if (!flag.IsEnabledIn(enabled))
            {
                continue;
            }

            var id = flag.ToId();
            if (id == LocomotionStateId.None || IsCompositeAuthority(id))
            {
                continue;
            }

            list.Add(new SimpleKey(id));
        }

        return list;
    }

    static bool HasSimpleBinding(LocomotionStateBinding[] bindings, LocomotionStateId id)
    {
        for (var i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].State == id)
            {
                return true;
            }
        }

        return false;
    }

    static bool HasStrafeBinding(LocomotionStateBinding[] bindings, StrafeKey key)
    {
        for (var i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b.State == LocomotionStateId.StrafeLocomotion
                && b.StrafeDirection == key.Direction
                && b.RunRequirement == key.RunRequirement)
            {
                return true;
            }
        }

        return false;
    }

    static bool HasTurnBinding(LocomotionStateBinding[] bindings, TurnKey key)
    {
        for (var i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b.State == LocomotionStateId.TurnInPlaceDirected && b.TurnDirection == key.Direction)
            {
                return true;
            }
        }

        return false;
    }

    static int ExpandStrafeTemplatesInternal(List<LocomotionStateBinding> list)
    {
        var added = 0;
        for (var d = 0; d < StrafeDirections.Length; d++)
        {
            for (var r = 0; r < StrafeRunRequirements.Length; r++)
            {
                var key = new StrafeKey(StrafeDirections[d], StrafeRunRequirements[r]);
                if (HasStrafeBinding(list, key))
                {
                    continue;
                }

                list.Add(CreateDefaultStrafe(key));
                added++;
            }
        }

        return added;
    }

    static int ExpandTurnTemplatesInternal(List<LocomotionStateBinding> list)
    {
        var added = 0;
        for (var i = 0; i < TurnDirections.Length; i++)
        {
            var key = new TurnKey(TurnDirections[i]);
            if (HasTurnBinding(list, key))
            {
                continue;
            }

            list.Add(CreateDefaultTurn(key));
            added++;
        }

        return added;
    }

    static bool HasStrafeBinding(List<LocomotionStateBinding> list, StrafeKey key) =>
        HasStrafeBinding(list.ToArray(), key);

    static bool HasTurnBinding(List<LocomotionStateBinding> list, TurnKey key) =>
        HasTurnBinding(list.ToArray(), key);

    static int RemoveInvalidCompositeRows(List<LocomotionStateBinding> list)
    {
        var removed = 0;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var row = list[i];
            if (IsInvalidCompositeRow(row))
            {
                list.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    static bool IsInvalidCompositeRow(LocomotionStateBinding row)
    {
        if (row.State == LocomotionStateId.StrafeLocomotion)
        {
            return row.StrafeDirection == StrafeDirection8.None
                   || row.RunRequirement == LocomotionRunRequirement.Any;
        }

        if (row.State == LocomotionStateId.TurnInPlaceDirected)
        {
            return row.TurnDirection == TurnDirection4.None;
        }

        return false;
    }

    static int RemoveDuplicateRows(List<LocomotionStateBinding> list) =>
        RemoveDuplicateRowsKeepBestAsset(list);

    static int RemoveDuplicateRowsKeepBestAsset(List<LocomotionStateBinding> list)
    {
        var keepIndexByKey = new Dictionary<BindingRowKey, int>(BindingRowComparer);
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].State == LocomotionStateId.None)
            {
                continue;
            }

            var key = new BindingRowKey(list[i]);
            if (!keepIndexByKey.TryGetValue(key, out var keepIdx))
            {
                keepIndexByKey[key] = i;
                continue;
            }

            if (PreferBindingRow(list[i], list[keepIdx]))
            {
                keepIndexByKey[key] = i;
            }
        }

        var keep = new HashSet<int>(keepIndexByKey.Values);
        var removed = 0;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].State == LocomotionStateId.None)
            {
                continue;
            }

            if (!keep.Contains(i))
            {
                list.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    static bool PreferBindingRow(LocomotionStateBinding candidate, LocomotionStateBinding incumbent)
    {
        var candidateAssets = HasAssignedAssets(candidate);
        var incumbentAssets = HasAssignedAssets(incumbent);
        if (candidateAssets != incumbentAssets)
        {
            return candidateAssets;
        }

        return false;
    }

    static bool HasAssignedAssets(LocomotionStateBinding row) =>
        row.DiscreteAction != null || row.ContinuousClip != null;

    static LocomotionStateBinding CreateDefaultSimple(SimpleKey key) => CreateDefaultSimple(key.State);

    static LocomotionStateBinding CreateDefaultSimple(LocomotionStateId id)
    {
        return new LocomotionStateBinding
        {
            State = id,
            FallbackState = GetDefaultFallback(id),
            Speed = 1f,
            TransitionDuration = 0.08f,
            StrafeDirection = StrafeDirection8.None,
            TurnDirection = TurnDirection4.None,
            RunRequirement = LocomotionRunRequirement.Any,
        };
    }

    static LocomotionStateBinding CreateDefaultStrafe(StrafeKey key)
    {
        return new LocomotionStateBinding
        {
            State = LocomotionStateId.StrafeLocomotion,
            FallbackState = LocomotionStateId.Walk,
            Speed = 1f,
            TransitionDuration = 0.08f,
            StrafeDirection = key.Direction,
            TurnDirection = TurnDirection4.None,
            RunRequirement = key.RunRequirement,
        };
    }

    static LocomotionStateBinding CreateDefaultTurn(TurnKey key)
    {
        return new LocomotionStateBinding
        {
            State = LocomotionStateId.TurnInPlaceDirected,
            FallbackState = LocomotionStateId.Idle,
            Speed = 1f,
            TransitionDuration = 0.08f,
            StrafeDirection = StrafeDirection8.None,
            TurnDirection = key.Direction,
            RunRequirement = LocomotionRunRequirement.Any,
        };
    }

    static LocomotionStateId GetDefaultFallback(LocomotionStateId id)
    {
        switch (id)
        {
            case LocomotionStateId.JumpStart:
                return LocomotionStateId.AirJumpLoop;
            case LocomotionStateId.JumpLand:
            case LocomotionStateId.WalkEnd:
            case LocomotionStateId.RunEnd:
                return LocomotionStateId.Idle;
            case LocomotionStateId.WalkStart:
#pragma warning disable CS0618
            case LocomotionStateId.RunStartLegacy:
#pragma warning restore
                return LocomotionStateId.Walk;
            case LocomotionStateId.RunStart:
                return LocomotionStateId.Run;
            case LocomotionStateId.StrafeLocomotion:
                return LocomotionStateId.Walk;
            case LocomotionStateId.TurnInPlaceDirected:
                return LocomotionStateId.Idle;
            default:
                return LocomotionStateId.Idle;
        }
    }

    static bool IsCompositeAuthority(LocomotionStateId id) =>
        id == LocomotionStateId.StrafeLocomotion || id == LocomotionStateId.TurnInPlaceDirected;

    static bool IsSimpleRow(BindingRowKey key) =>
        key.StrafeDirection == StrafeDirection8.None
        && key.TurnDirection == TurnDirection4.None
        && key.RunRequirement == LocomotionRunRequirement.Any;

    static bool IsObsoleteId(LocomotionStateId id) => id.IsObsoleteLocomotionState();

    static void Log(string message)
    {
        if (!UnityEditor.EditorPrefs.GetBool("debugLocomotionProfileSync", false))
        {
            return;
        }

        Debug.Log($"{LogPrefix} {message}");
    }

    sealed class SimpleKeyComparer : IEqualityComparer<SimpleKey>
    {
        public bool Equals(SimpleKey x, SimpleKey y) => x.Equals(y);
        public int GetHashCode(SimpleKey obj) => obj.GetHashCode();
    }

    sealed class StrafeKeyComparer : IEqualityComparer<StrafeKey>
    {
        public bool Equals(StrafeKey x, StrafeKey y) => x.Equals(y);
        public int GetHashCode(StrafeKey obj) => obj.GetHashCode();
    }

    sealed class TurnKeyComparer : IEqualityComparer<TurnKey>
    {
        public bool Equals(TurnKey x, TurnKey y) => x.Equals(y);
        public int GetHashCode(TurnKey obj) => obj.GetHashCode();
    }

    sealed class BindingRowKeyComparer : IEqualityComparer<BindingRowKey>
    {
        public bool Equals(BindingRowKey x, BindingRowKey y) => x.Equals(y);
        public int GetHashCode(BindingRowKey obj) => obj.GetHashCode();
    }
}
#endif
