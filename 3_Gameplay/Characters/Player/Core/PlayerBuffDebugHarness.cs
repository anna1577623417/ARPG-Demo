using UnityEngine;

/// <summary>
/// Phase 3 验证组件：手动施加/移除 Buff，并打印攻击力变化。
/// </summary>
public sealed class PlayerBuffDebugHarness : MonoBehaviour
{
    [SerializeField] Player player;
    [SerializeField] BuffDefinitionSO attackBuff;
    [SerializeField] KeyCode applyKey = KeyCode.F6;
    [SerializeField] KeyCode removeKey = KeyCode.F7;

    BuffInstance _active;
    bool _hasActive;

    void Reset()
    {
        if (player == null)
        {
            player = GetComponent<Player>();
        }
    }

    void Update()
    {
        if (player == null || attackBuff == null)
        {
            return;
        }

        if (Input.GetKeyDown(applyKey))
        {
            _active = player.Buffs.Apply(attackBuff, this);
            _hasActive = _active.RuntimeId != 0;
            Debug.Log($"[BuffDebug] Apply id={_active.RuntimeId} atk={player.Stats.Get(StatType.AttackPower):F2}", player);
        }

        if (Input.GetKeyDown(removeKey) && _hasActive)
        {
            var removed = player.Buffs.Remove(_active);
            _hasActive = false;
            Debug.Log($"[BuffDebug] Remove ok={removed} atk={player.Stats.Get(StatType.AttackPower):F2}", player);
        }
    }
}
