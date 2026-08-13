using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Unity PlayerLoop 与纯 World Service 的唯一桥。</summary>
public sealed class SpawnedCombatWorldHost : MonoBehaviour
{
    static SpawnedCombatWorldHost s_instance;

    SpawnedCombatWorldService _world;
    bool _quitting;

    public static ICombatSpawnPort Port =>
        s_instance != null ? s_instance._world : null;

    public static SpawnedCombatWorldService World =>
        s_instance != null ? s_instance._world : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (s_instance != null)
        {
            return;
        }

        var go = new GameObject("[SpawnedCombatWorld]");
        DontDestroyOnLoad(go);
        s_instance = go.AddComponent<SpawnedCombatWorldHost>();
    }

    void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        var scene = SceneManager.GetActiveScene();
        _world = new SpawnedCombatWorldService(
            1,
            scene.handle,
            candidateSink: SpawnedCombatOutcomePipeline.Shared);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    void Update()
    {
        _world?.Tick(Time.deltaTime);
    }

    void OnActiveSceneChanged(Scene previous, Scene next)
    {
        _world?.ChangeWorld(next.handle);
    }

    void OnApplicationQuit()
    {
        _quitting = true;
        _world?.Dispose();
    }

    void OnDestroy()
    {
        if (s_instance != this)
        {
            return;
        }

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (!_quitting)
        {
            _world?.Dispose();
        }

        s_instance = null;
    }
}
