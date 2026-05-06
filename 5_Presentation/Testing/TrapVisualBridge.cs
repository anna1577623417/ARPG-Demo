using UnityEngine;

/// <summary>
/// 陷阱表现桥接：仅监听触发事件驱动特效，不参与任何战斗逻辑。
/// </summary>
public sealed class TrapVisualBridge : MonoBehaviour
{
    [SerializeField] EffectTrigger trigger;
    [SerializeField] ParticleSystem activateVfx;
    [SerializeField] ParticleSystem triggerVfx;
    [SerializeField] AudioClip triggerSfx;
    [SerializeField] Animator animator;

    void OnEnable()
    {
        if (trigger == null)
        {
            return;
        }

        trigger.OnEffectApplied += OnEffectApplied;
        trigger.OnTriggerActivated += OnActivated;
        trigger.OnTriggerDeactivated += OnDeactivated;
    }

    void OnDisable()
    {
        if (trigger == null)
        {
            return;
        }

        trigger.OnEffectApplied -= OnEffectApplied;
        trigger.OnTriggerActivated -= OnActivated;
        trigger.OnTriggerDeactivated -= OnDeactivated;
    }

    void OnEffectApplied(IEntity target)
    {
        if (triggerVfx != null)
        {
            triggerVfx.Play();
        }

        if (triggerSfx != null)
        {
            AudioSource.PlayClipAtPoint(triggerSfx, transform.position);
        }
    }

    void OnActivated()
    {
        if (activateVfx != null)
        {
            activateVfx.Play();
        }

        if (animator != null)
        {
            animator.SetTrigger("Activate");
        }
    }

    void OnDeactivated()
    {
        if (activateVfx != null)
        {
            activateVfx.Stop();
        }

        if (animator != null)
        {
            animator.SetTrigger("Deactivate");
        }
    }
}
