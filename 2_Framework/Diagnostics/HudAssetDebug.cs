using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// HUD 诊断用 — 解析 SO 资产路径 / 文件夹（Editor Play 下可精准定位配表目录）。
/// </summary>
public static class HudAssetDebug
{
    public static string GetPath(Object asset)
    {
        if (asset == null)
        {
            return string.Empty;
        }

#if UNITY_EDITOR
        return AssetDatabase.GetAssetPath(asset) ?? string.Empty;
#else
        return asset.name;
#endif
    }

    public static string GetFolder(Object asset)
    {
        var path = GetPath(asset);
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var lastSlash = path.LastIndexOf('/');
        return lastSlash > 0 ? path.Substring(0, lastSlash) : path;
    }
}
