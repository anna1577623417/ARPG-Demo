using UnityEngine;

/// <summary>
/// 动画事件分发器。
/// 挂在带 Animator 的 GameObject 上，由动画片段中的 Animation Event 调用。
/// 将 Unity 动画事件转化为 EventBus 事件，保持动画与逻辑的解耦。
///
/// 使用方式：
/// 1. 在 AnimationClip 编辑器中添加事件，函数名填 OnFootstep / OnHitFrame / OnAnimEnd 等
/// 2. 运行时 Animator 会在对应帧自动调用这些方法
/// 3. 方法内通过 Entity.EventBus 发布事件，供其他系统（音效、特效、伤害判定）监听
/// </summary>
public class Ani_Event_Dispatcher : MonoBehaviour
{
    private Entity _entity;

    private void Awake()
    {
        _entity = GetComponentInParent<Entity>();
    }

    /// <summary>脚步声事件（由动画片段中的 Animation Event 调用）。</summary>
    public void OnFootstep()
    {
        // 预留：发布脚步事件，音效系统监听
        // if (_entity != null)
        //     _entity.PublishEvent(new FootstepEvent(...));
    }

    /// <summary>攻击命中帧事件（用于伤害判定时机）。</summary>
    public void OnHitFrame()
    {
        // 预留：发布命中帧事件，伤害判定系统监听
        // if (_entity != null)
        //     _entity.PublishEvent(new HitFrameEvent(...));
    }

    /// <summary>动画播放结束事件（用于单次动画结束通知）。</summary>
    public void OnAnimEnd()
    {
        // 预留：发布动画结束事件
        // if (_entity != null)
        //     _entity.PublishEvent(new AnimEndEvent(...));
    }
}
