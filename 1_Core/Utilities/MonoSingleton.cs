using UnityEngine;

/// <summary>
/// 场景对象单例基类。
/// 适用于需要挂载到 GameObject、参与 Unity 生命周期的系统。
/// </summary>
public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T s_instance;
    private static bool s_isQuitting;

    public static T Instance
    {
        get
        {
            if (s_isQuitting)
            {
                return null;
            }

            if (s_instance == null)
            {
                s_instance = FindFirstObjectByType<T>();

                if (s_instance == null)
                {
                    var obj = new GameObject(typeof(T).Name);
                    s_instance = obj.AddComponent<T>();
                }
            }

            return s_instance;
        }
    }

    protected bool IsPrimaryInstance => ReferenceEquals(s_instance, this);

    /// <summary>
    /// 是否跨场景保留。默认保留，可在子类覆写。
    /// </summary>
    protected virtual bool PersistAcrossScenes => true;

    protected virtual void Awake()
    {
        if (s_instance == null)
        {
            s_instance = this as T;
            if (PersistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (!ReferenceEquals(s_instance, this))
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (ReferenceEquals(s_instance, this))
        {
            s_instance = null;
        }
    }

    protected virtual void OnApplicationQuit()
    {
        s_isQuitting = true;
    }
}