using UnityEngine;

/// <summary>
/// 固定容量、每次采样复用的候选缓冲。Collider→Entity 映射、QueryPolicy 与同目标去重只在此处发生。
/// </summary>
public sealed class ContactCandidateBuffer
{
    readonly ContactCandidate[] _items;
    readonly int[] _targetIds;

    public int Count { get; private set; }
    public bool Saturated { get; private set; }
    public int RawCandidateCount { get; private set; }

    public ContactCandidateBuffer(int capacity = 64)
    {
        var safeCapacity = Mathf.Max(8, capacity);
        _items = new ContactCandidate[safeCapacity];
        _targetIds = new int[safeCapacity];
    }

    public ContactCandidate this[int index] => _items[index];
    public int Capacity => _items.Length;

    public void Clear()
    {
        Count = 0;
        Saturated = false;
        RawCandidateCount = 0;
    }

    public void MarkSaturated()
    {
        Saturated = true;
    }

    public void SortStable(Vector3 referencePosition)
    {
        for (var i = 1; i < Count; i++)
        {
            var item = _items[i];
            var targetId = _targetIds[i];
            var itemDistance = (item.Point - referencePosition).sqrMagnitude;
            var j = i - 1;
            while (j >= 0)
            {
                var previousDistance =
                    (_items[j].Point - referencePosition).sqrMagnitude;
                if (previousDistance < itemDistance
                    || (Mathf.Approximately(previousDistance, itemDistance)
                        && _targetIds[j] <= targetId))
                {
                    break;
                }

                _items[j + 1] = _items[j];
                _targetIds[j + 1] = _targetIds[j];
                j--;
            }

            _items[j + 1] = item;
            _targetIds[j + 1] = targetId;
        }
    }

    public bool TryAdd(
        Collider collider,
        Entity source,
        in ContactQueryPolicy query,
        Vector3 point,
        Vector3 normal)
    {
        RawCandidateCount++;
        if (collider == null)
        {
            return false;
        }

        var target = collider.GetComponentInParent<Entity>();
        if (target == null || !TargetProfileEvaluator.Passes(in query.Target, source, target))
        {
            return false;
        }

        var targetId = target.GetInstanceID();
        for (var i = 0; i < Count; i++)
        {
            if (_targetIds[i] == targetId)
            {
                return false;
            }
        }

        if (Count >= _items.Length)
        {
            Saturated = true;
            return false;
        }

        _targetIds[Count] = targetId;
        _items[Count] = new ContactCandidate(
            collider,
            target,
            point,
            normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up,
            ResolveBoneName(target, collider.transform));
        Count++;
        return true;
    }

    static string ResolveBoneName(Entity target, Transform hitTransform)
    {
        if (hitTransform == null)
        {
            return "Body";
        }

        var animator = target != null ? target.Animator : null;
        if (animator != null && animator.isHuman)
        {
            for (var t = hitTransform; t != null && t != target.transform; t = t.parent)
            {
                for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    if (animator.GetBoneTransform((HumanBodyBones)i) == t)
                    {
                        return ((HumanBodyBones)i).ToString();
                    }
                }
            }
        }

        return hitTransform.name;
    }
}
