using UnityEngine;

/// <summary>
/// 174.2 — Curve Segment Library：12 段常用曲线工厂。
/// <para>策划在 Inspector "Apply Preset Curve" 下拉中选择对应段，一键填入任意曲线字段。</para>
/// <para>每段附"对应游戏 + 招式 + 应用层"参考，便于知识复用。</para>
/// </summary>
public static class MotionCurveLibrary
{
    public enum Segment : byte
    {
        /// <summary>0→1 直线渐入。位移衰减、权重渐恢复。</summary>
        LinearRamp = 0,

        /// <summary>1→0 反向直线。锁朝向→渐恢复（DMC 重击后摇）。</summary>
        ReverseRamp = 1,

        /// <summary>缓起（Cubic Ease In）。重击前摇蓄力。</summary>
        EaseInCubic = 2,

        /// <summary>缓收（Cubic Ease Out）。冲刺收尾减速。</summary>
        EaseOutCubic = 3,

        /// <summary>S 形（先慢后快再缓）。平滑突进。</summary>
        SCurve = 4,

        /// <summary>钟形 Bell。一闪而过的高重力（终结技砸落瞬间）。</summary>
        Bell = 5,

        /// <summary>升 + 持平 + 落。升龙 hover（绝区零 Ellen 空连）。</summary>
        ApexHold = 6,

        /// <summary>单帧峰值 Spike。终结技 Hitstop 放大瞬间。</summary>
        Spike = 7,

        /// <summary>单帧零值（持 1 段 0 → 恢复 1）。闪避无敌帧。</summary>
        InverseSpike = 8,

        /// <summary>双峰 Heart Beat。战神 Rage 二段冲击。</summary>
        HeartBeat = 9,

        /// <summary>恒 0。完全锁。</summary>
        ConstantZero = 10,

        /// <summary>恒 1。完全开。</summary>
        ConstantOne = 11,
    }

    /// <summary>生成指定段的曲线（t∈[0,1]，y∈视段而定）。</summary>
    public static AnimationCurve Make(Segment seg)
    {
        return seg switch
        {
            Segment.LinearRamp    => AnimationCurve.Linear(0f, 0f, 1f, 1f),
            Segment.ReverseRamp   => AnimationCurve.Linear(0f, 1f, 1f, 0f),
            Segment.EaseInCubic   => Cubic(0f, 0f, 1f, 1f, easeIn: true),
            Segment.EaseOutCubic  => Cubic(0f, 0f, 1f, 1f, easeIn: false),
            Segment.SCurve        => AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            Segment.Bell          => Bell(),
            Segment.ApexHold      => ApexHold(),
            Segment.Spike         => Spike(),
            Segment.InverseSpike  => InverseSpike(),
            Segment.HeartBeat     => HeartBeat(),
            Segment.ConstantZero  => AnimationCurve.Constant(0f, 1f, 0f),
            Segment.ConstantOne   => AnimationCurve.Constant(0f, 1f, 1f),
            _                     => AnimationCurve.Constant(0f, 1f, 1f),
        };
    }

    /// <summary>用例文档：对应游戏 + 招式 + 应用层。供 Inspector Tooltip 显示。</summary>
    public static string GetUseCase(Segment seg) => seg switch
    {
        Segment.LinearRamp    => "渐入：MoveInputWeight 0→1 恢复（DMC 收刀后段）",
        Segment.ReverseRamp   => "渐衰：FacingInputWeight 1→0 锁朝向（蓄力前摇）",
        Segment.EaseInCubic   => "缓起：Anim Speed 加速（重击突进起手）",
        Segment.EaseOutCubic  => "缓收：Z 位移减速（冲刺到达收尾）",
        Segment.SCurve        => "S 形：Y 平滑突进 / Yaw 平滑旋转（法环回旋斩）",
        Segment.Bell          => "钟形：GravityWeight 终结技砸落瞬时强重力",
        Segment.ApexHold      => "升龙 Hover：Y Apex Hold（绝区零 Ellen）",
        Segment.Spike         => "单帧峰值：Hitstop 放大震屏（终结命中瞬间）",
        Segment.InverseSpike  => "单帧零：闪避无敌帧 / 朝向冻结",
        Segment.HeartBeat     => "双峰：战神 Rage 二段冲击",
        Segment.ConstantZero  => "全锁：MoveInputWeight=0 完全锁移动",
        Segment.ConstantOne   => "全开：默认值（V1 行为）",
        _ => string.Empty,
    };

    // ─── 内部曲线构造 ────────────────────────────────────────────

    static AnimationCurve Cubic(float t0, float v0, float t1, float v1, bool easeIn)
    {
        var c = new AnimationCurve();
        if (easeIn)
        {
            c.AddKey(new Keyframe(t0, v0, 0f, 0f));
            c.AddKey(new Keyframe(t1, v1, 2f, 0f));
        }
        else
        {
            c.AddKey(new Keyframe(t0, v0, 0f, 2f));
            c.AddKey(new Keyframe(t1, v1, 0f, 0f));
        }
        return c;
    }

    static AnimationCurve Bell()
    {
        var c = new AnimationCurve();
        c.AddKey(new Keyframe(0f, 0f, 0f, 0f));
        c.AddKey(new Keyframe(0.5f, 1f, 0f, 0f));
        c.AddKey(new Keyframe(1f, 0f, 0f, 0f));
        return c;
    }

    static AnimationCurve ApexHold()
    {
        var c = new AnimationCurve();
        c.AddKey(new Keyframe(0f, 0f, 0f, 0f));
        c.AddKey(new Keyframe(0.25f, 1f, 0f, 0f));
        c.AddKey(new Keyframe(0.75f, 1f, 0f, 0f));
        c.AddKey(new Keyframe(1f, 0f, 0f, 0f));
        return c;
    }

    static AnimationCurve Spike()
    {
        var c = new AnimationCurve();
        c.AddKey(new Keyframe(0f, 0f, 0f, 0f));
        c.AddKey(new Keyframe(0.48f, 0f, 0f, 0f));
        c.AddKey(new Keyframe(0.50f, 1f, 0f, 0f));
        c.AddKey(new Keyframe(0.52f, 0f, 0f, 0f));
        c.AddKey(new Keyframe(1f, 0f, 0f, 0f));
        return c;
    }

    static AnimationCurve InverseSpike()
    {
        var c = new AnimationCurve();
        c.AddKey(new Keyframe(0f, 1f, 0f, 0f));
        c.AddKey(new Keyframe(0.20f, 1f, 0f, 0f));
        c.AddKey(new Keyframe(0.21f, 0f, 0f, 0f));
        c.AddKey(new Keyframe(0.50f, 0f, 0f, 0f));
        c.AddKey(new Keyframe(0.51f, 1f, 0f, 0f));
        c.AddKey(new Keyframe(1f, 1f, 0f, 0f));
        return c;
    }

    static AnimationCurve HeartBeat()
    {
        var c = new AnimationCurve();
        c.AddKey(new Keyframe(0f, 0f, 0f, 0f));
        c.AddKey(new Keyframe(0.25f, 1f, 0f, 0f));
        c.AddKey(new Keyframe(0.40f, 0.2f, 0f, 0f));
        c.AddKey(new Keyframe(0.65f, 1f, 0f, 0f));
        c.AddKey(new Keyframe(1f, 0f, 0f, 0f));
        return c;
    }
}
