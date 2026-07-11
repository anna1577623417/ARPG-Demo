using UnityEngine;

/// <summary>
/// 216.3 M1 L2 — 将 <see cref="HitClip.Origin"/>（SpawnSource）+ 本地偏移解析为世界原点。
/// <para>优先 Humanoid 骨骼（复用 <see cref="CombatSpawnBoneResolver"/>）；无骨骼 / 非人形回落到实体根。</para>
/// <para>放在 Gameplay 层：4_Data 的 HitClip 不得依赖 Entity/Transform 解析。</para>
/// </summary>
public static class HitClipOriginResolver
{
    public static void Resolve(Entity source, in HitClip clip, out Vector3 pos, out Quaternion rot)
    {
        var root = source != null ? source.transform : null;
        var basePos = root != null ? root.position : Vector3.zero;
        var baseRot = root != null ? root.rotation : Quaternion.identity;

        if (CombatSpawnBoneResolver.TryGetBoneTransform(source, clip.Origin, out var bone) && bone != null)
        {
            basePos = bone.position;
            baseRot = bone.rotation;
        }

        pos = basePos + baseRot * clip.OriginOffset;
        rot = baseRot * Quaternion.Euler(clip.OriginEuler);
    }
}
