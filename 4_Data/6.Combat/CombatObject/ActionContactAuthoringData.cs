using System;
using UnityEngine;

/// <summary>224.1 L1 — ActionContact 空间作者数据版本。零值表示旧 Preset/Override 路径。</summary>
public enum ActionContactAuthoringVersion : byte
{
    LegacyPresetOverride = 0,
    CombatObjectSingleSourceV1 = 1,
}

/// <summary>锚点绑定：是否在 Window Start 冻结，或每帧跟随。</summary>
public enum ContactAnchorBindingMode : byte
{
    StaticAtWindowStart = 0,
    FollowAnchor = 1,
}

/// <summary>采样扫掠策略；与 Binding 正交。</summary>
public enum ContactSweepPolicy : byte
{
    None = 0,
    BetweenSamples = 1,
}

/// <summary>Origin 是自动推荐还是作者显式指定。</summary>
public enum ContactOriginPolicy : byte
{
    Auto = 0,
    Explicit = 1,
}

/// <summary>是否继承 Anchor Transform 缩放。默认忽略。</summary>
public enum ContactAnchorScalePolicy : byte
{
    IgnoreAnchorScale = 0,
    MultiplyAnchorLossyScale = 1,
}

/// <summary>
/// Contact Anchor 引用。L1 仍以 SpawnSource 作为迁移期载体；
/// 不把 Spawned 专用枚举扩张进最终 Contact Anchor 模型。
/// </summary>
[Serializable]
public struct ContactAnchorReference
{
    public SpawnSource Source;

    public static ContactAnchorReference FromSpawnSource(SpawnSource source) =>
        new ContactAnchorReference { Source = source };

    public static ContactAnchorReference DefaultFollow =>
        FromSpawnSource(SpawnSource.SelfHandR);

    public static ContactAnchorReference DefaultStatic =>
        FromSpawnSource(SpawnSource.SelfRootBone);
}

/// <summary>
/// ActionContact 在 CombatObject 上的唯一空间真相。
/// UseExplicitData=false 时 Adapter 只读 Legacy Preset/Override，不把默认 enum 误判为新逻辑。
/// </summary>
[Serializable]
public struct ActionContactAuthoringData
{
    [Tooltip("关闭时不读取其它新字段；旧资产不得因默认值自动进入新路径。")]
    public bool UseExplicitData;

    public ActionContactAuthoringVersion Version;

    public ContactAnchorBindingMode BindingMode;
    public ContactSweepPolicy SweepPolicy;

    public ContactOriginPolicy OriginPolicy;
    public ContactAnchorReference Origin;

    public Vector3 LocalPosition;
    public Vector3 LocalEuler;
    public ContactAnchorScalePolicy ScalePolicy;

    [SerializeField] ContactAnchorReference _lastExplicitFollowOrigin;
    [SerializeField] ContactAnchorReference _lastExplicitStaticOrigin;

    public ContactAnchorReference LastExplicitFollowOrigin => _lastExplicitFollowOrigin;
    public ContactAnchorReference LastExplicitStaticOrigin => _lastExplicitStaticOrigin;

    public void RememberExplicitOrigin(ContactAnchorBindingMode binding, in ContactAnchorReference origin)
    {
        if (binding == ContactAnchorBindingMode.FollowAnchor)
        {
            _lastExplicitFollowOrigin = origin;
        }
        else
        {
            _lastExplicitStaticOrigin = origin;
        }
    }

    public static ActionContactAuthoringData CreateNewV1()
    {
        return new ActionContactAuthoringData
        {
            UseExplicitData = true,
            Version = ActionContactAuthoringVersion.CombatObjectSingleSourceV1,
            BindingMode = ContactAnchorBindingMode.FollowAnchor,
            SweepPolicy = ContactSweepPolicy.BetweenSamples,
            OriginPolicy = ContactOriginPolicy.Auto,
            Origin = ContactAnchorReference.DefaultFollow,
            LocalPosition = Vector3.zero,
            LocalEuler = Vector3.zero,
            ScalePolicy = ContactAnchorScalePolicy.IgnoreAnchorScale,
            _lastExplicitFollowOrigin = ContactAnchorReference.DefaultFollow,
            _lastExplicitStaticOrigin = ContactAnchorReference.DefaultStatic,
        };
    }
}
