using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 统一游戏模块初始化接口。
/// 规则：
/// 1) 系统级模块实现该接口并由 GameBootstrapper 集中 Init；
/// 2) 非模块实例类在自身 Awake 内部调用 Init。
/// </summary>
public interface IGameModule
{
    bool IsInitialized { get; }
    void Init();
}

/// <summary>
/// 游戏启动器：集中初始化所有 IGameModule。
/// </summary>
public class GameBootstrapper : MonoSingleton<GameBootstrapper>
{
    [SerializeField] private bool autoFindSceneModules = true;
    [SerializeField] private List<MonoBehaviour> modules = new List<MonoBehaviour>(8);

    private readonly List<IGameModule> _runtimeModules = new List<IGameModule>(16);
    private bool _isInitialized;

    protected override void Awake()
    {
        base.Awake();
        if (!IsPrimaryInstance)
        {
            return;
        }
        Init();
    }

    public void Init()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        CollectModules();

        for (var i = 0; i < _runtimeModules.Count; i++)
        {
            var module = _runtimeModules[i];
            if (module == null || module.IsInitialized)
            {
                continue;
            }

            module.Init();
        }
    }

    private void CollectModules()
    {
        _runtimeModules.Clear();

        for (var i = 0; i < modules.Count; i++)
        {
            if (modules[i] is IGameModule module)
            {
                _runtimeModules.Add(module);
            }
        }

        if (!autoFindSceneModules)
        {
            return;
        }

        var found = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < found.Length; i++)
        {
            if (found[i] == null || found[i] == this)
            {
                continue;
            }

            if (found[i] is not IGameModule module)
            {
                continue;
            }

            if (_runtimeModules.Contains(module))
            {
                continue;
            }

            _runtimeModules.Add(module);
        }
    }
}