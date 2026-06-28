#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 178.1 — Transition 资产工厂：一键产 Matrix / Config / Database 三件套并填默认值。
/// </summary>
public static class TransitionMatrixDefaultFiller
{
    const string Folder = "Assets/GameMain/4_Data/Animation/Presets";

    [MenuItem("GameMain/Animation/Create All Transition Assets")]
    public static void CreateAll()
    {
        EnsureFolder(Folder);
        CreateMatrix();
        CreateConfig();
        CreateDatabase();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Transition] All assets created under {Folder}");
    }

    [MenuItem("GameMain/Animation/Create Transition Matrix (with defaults)")]
    public static void CreateMatrix()
    {
        EnsureFolder(Folder);
        var path = $"{Folder}/TransitionTagMatrix_Default.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TransitionTagMatrixSO>(path);
        var matrix = existing != null ? existing : ScriptableObject.CreateInstance<TransitionTagMatrixSO>();

        FillDefaults(matrix);

        if (existing == null)
        {
            AssetDatabase.CreateAsset(matrix, path);
        }
        EditorUtility.SetDirty(matrix);
    }

    [MenuItem("GameMain/Animation/Create Transition Config")]
    public static void CreateConfig()
    {
        EnsureFolder(Folder);
        var path = $"{Folder}/TransitionConfig_Default.asset";
        if (AssetDatabase.LoadAssetAtPath<TransitionConfigSO>(path) != null) return;
        var c = ScriptableObject.CreateInstance<TransitionConfigSO>();
        AssetDatabase.CreateAsset(c, path);
    }

    [MenuItem("GameMain/Animation/Create Transition Database (empty)")]
    public static void CreateDatabase()
    {
        EnsureFolder(Folder);
        var path = $"{Folder}/TransitionDatabase_Default.asset";
        if (AssetDatabase.LoadAssetAtPath<TransitionDatabaseSO>(path) != null) return;
        var db = ScriptableObject.CreateInstance<TransitionDatabaseSO>();
        AssetDatabase.CreateAsset(db, path);
    }

    static void FillDefaults(TransitionTagMatrixSO matrix)
    {
        var tbl = TransitionTagMatrixSO.DefaultBlendTable;
        for (var i = 0; i < TransitionTagMatrixSO.TagCount; i++)
        {
            for (var j = 0; j < TransitionTagMatrixSO.TagCount; j++)
            {
                var fromTag = (TransitionTag)i;
                var toTag = (TransitionTag)j;
                var blend = tbl[i, j];

                var mode = TransitionMode.CrossFade;
                if (blend <= 0.0001f && fromTag == TransitionTag.Hit)
                {
                    mode = TransitionMode.Snap;
                }
                else if (TransitionTagMatrixSO.IsPhaseMatchPair(fromTag, toTag))
                {
                    mode = TransitionMode.PhaseMatch;
                }

                matrix.SetEntry(fromTag, toTag, new TransitionTagMatrixSO.Entry
                {
                    BlendDuration = blend,
                    Mode = mode,
                    EaseCurve = null,
                    DesignerNote = string.Empty,
                });
            }
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var leaf = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
