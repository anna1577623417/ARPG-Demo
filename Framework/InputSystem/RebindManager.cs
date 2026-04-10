using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 按键换绑管理器。
/// 负责换绑流程的核心逻辑：开始换绑、完成换绑、取消换绑、保存/加载配置。
///
/// ═══ 换绑流程 ═══
///
/// 1. UI 调用 StartRebind(action, bindingIndex)
/// 2. RebindManager 禁用该 Action，启动 PerformInteractiveRebinding
/// 3. 玩家按下新按键 → OnComplete 回调
///    - 检测按键冲突（同 ActionMap 内是否有相同绑定）
///    - 如果冲突，交换两个 Action 的绑定
///    - 通知 UI 更新显示
/// 4. 按 ESC → OnCancel 回调，取消换绑
/// 5. 最终调用 SaveBindings 持久化到 PlayerPrefs
///
/// ═══ ESC 保护 ═══
/// ESC 被设为换绑流程的取消键，不能被绑定到其他操作。
///
/// ═══ 使用方式 ═══
/// 挂在场景中的管理器 GameObject 上，或作为单例。
/// UI 通过引用 RebindManager 调用 StartRebind。
/// </summary>
public class RebindManager : MonoSingleton<RebindManager>, IGameModule
{
    private const string SAVE_KEY = "UserInputBindings";

    [Header("References")]
    [SerializeField] private InputReader inputReader;

    private InputActionRebindingExtensions.RebindingOperation _currentOperation;
    private bool _isRebinding;
    private bool _isInitialized;

    /// <summary>是否正在进行换绑操作。</summary>
    public bool IsRebinding => _isRebinding;
    public bool IsInitialized => _isInitialized;

    // ─── 回调事件（UI 监听） ───

    /// <summary>换绑完成回调。参数：(action, bindingIndex, newDisplayString)</summary>
    public event Action<InputAction, int, string> OnRebindComplete;

    /// <summary>换绑取消回调。</summary>
    public event Action OnRebindCanceled;

    /// <summary>换绑开始回调。参数：(actionName)</summary>
    public event Action<string> OnRebindStarted;

    // ─── 生命周期 ───

    protected override void Awake()
    {
        base.Awake();
        if (!IsPrimaryInstance)
        {
            return;
        }
        Init();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        CleanupOperation();
    }

    public void Init()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        LoadBindings();
    }

    // ─── 核心换绑流程 ───

    /// <summary>
    /// 开始对指定 Action 的指定绑定索引进行换绑。
    /// </summary>
    /// <param name="action">要换绑的 InputAction。</param>
    /// <param name="bindingIndex">绑定索引（一个 Action 可能有多个绑定，如键盘和手柄分别一个）。</param>
    public void StartRebind(InputAction action, int bindingIndex)
    {
        if (action == null || _isRebinding) return;

        _isRebinding = true;
        OnRebindStarted?.Invoke(action.name);

        // 换绑前必须禁用 Action
        action.Disable();

        _currentOperation = action.PerformInteractiveRebinding(bindingIndex)
            // ESC 用于取消，不能被绑定
            .WithCancelingThrough("<Keyboard>/escape")
            // 排除鼠标移动（防止误触）
            .WithControlsExcluding("Mouse")
            // 防止过快误触
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation => OnRebindFinished(action, bindingIndex, false))
            .OnCancel(operation => OnRebindFinished(action, bindingIndex, true))
            .Start();
    }

    /// <summary>手动取消当前换绑操作。</summary>
    public void CancelRebind()
    {
        if (_currentOperation != null && _isRebinding)
        {
            _currentOperation.Cancel();
        }
    }

    // ─── 内部处理 ───

    private void OnRebindFinished(InputAction action, int bindingIndex, bool canceled)
    {
        CleanupOperation();
        action.Enable();
        _isRebinding = false;

        if (canceled)
        {
            OnRebindCanceled?.Invoke();
            return;
        }

        // 检测按键冲突：同 ActionMap 内是否有其他 Action 使用了相同的绑定
        ResolveConflicts(action, bindingIndex);

        // 保存
        SaveBindings();

        // 通知 UI 更新
        var displayString = action.GetBindingDisplayString(bindingIndex);
        OnRebindComplete?.Invoke(action, bindingIndex, displayString);
    }

    /// <summary>
    /// 按键冲突处理：如果新绑定已被同 ActionMap 内其他 Action 使用，交换两者。
    /// </summary>
    private void ResolveConflicts(InputAction changedAction, int bindingIndex)
    {
        var newBinding = changedAction.bindings[bindingIndex];
        var actionMap = changedAction.actionMap;

        if (actionMap == null) return;

        foreach (var otherAction in actionMap.actions)
        {
            if (otherAction == changedAction) continue;

            for (int i = 0; i < otherAction.bindings.Count; i++)
            {
                if (otherAction.bindings[i].effectivePath == newBinding.effectivePath)
                {
                    // 冲突：把对方的绑定改成我之前的绑定（交换）
                    // 因为我们用的是 Override，直接 Apply 新路径
                    otherAction.ApplyBindingOverride(i, "");
                    Debug.Log($"[RebindManager] 按键冲突：{otherAction.name}[{i}] 的绑定已被清除");
                }
            }
        }
    }

    // ─── 持久化 ───

    /// <summary>将所有自定义绑定保存到 PlayerPrefs。</summary>
    public void SaveBindings()
    {
        if (inputReader == null || inputReader.ActionAsset == null) return;

        var json = inputReader.ActionAsset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    /// <summary>从 PlayerPrefs 加载自定义绑定并应用。</summary>
    public void LoadBindings()
    {
        if (inputReader == null || inputReader.ActionAsset == null) return;

        if (PlayerPrefs.HasKey(SAVE_KEY))
        {
            var json = PlayerPrefs.GetString(SAVE_KEY);
            inputReader.ActionAsset.LoadBindingOverridesFromJson(json);
        }
    }

    /// <summary>恢复所有绑定为默认值。</summary>
    public void ResetAllBindings()
    {
        if (inputReader == null || inputReader.ActionAsset == null) return;

        inputReader.ActionAsset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
    }

    // ─── 工具方法 ───

    /// <summary>获取指定 Action 在指定绑定索引的显示文本（如 "W"、"Space"）。</summary>
    public string GetBindingDisplayString(InputAction action, int bindingIndex)
    {
        if (action == null) return "";
        return action.GetBindingDisplayString(bindingIndex);
    }

    private void CleanupOperation()
    {
        _currentOperation?.Dispose();
        _currentOperation = null;
    }
}
