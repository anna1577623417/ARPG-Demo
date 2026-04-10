using System;
using System.Threading;

/// <summary>
/// 纯 C# 单例基类（不依赖 MonoBehaviour）。
/// 适用于工具类、配置缓存、计算服务等非场景对象。
/// </summary>
public abstract class Singleton<T> where T : class, new()
{
    private static readonly Lazy<T> s_instance =
        new Lazy<T>(() => new T(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static T Instance => s_instance.Value;
}