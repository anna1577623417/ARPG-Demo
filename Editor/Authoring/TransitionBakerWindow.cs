#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 178.1 W6 — Transition Database 离线烘焙器。
/// <para>扫描所有 ActionDataSO.MainClip → 对采样 (Clip, Clip) 计算 PoseCost → 写入 TransitionDatabaseSO。</para>
/// <para>菜单：GameMain → Animation → Transition Baker。</para>
/// </summary>
public sealed class TransitionBakerWindow : EditorWindow
{
    [MenuItem("GameMain/Animation/Transition Baker")]
    public static void Open() => GetWindow<TransitionBakerWindow>("Transition Baker");

    TransitionDatabaseSO m_targetDb;
    TransitionConfigSO m_config;
    GameObject m_skeletonRig;
    float m_threshold = 0.30f;
    Vector2 m_scroll;
    string m_lastReport;

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "扫描所有 ActionDataSO.MainClip → 对采样 (ClipA 末帧, ClipB 首帧) 计算 PoseCost → 写入 DB（仅 cost > 阈值）。\n" +
            "需要一个带 Animator 组件的角色 Rig GameObject 作为采样宿主。",
            MessageType.Info);

        m_targetDb = (TransitionDatabaseSO)EditorGUILayout.ObjectField(
            "Target Database", m_targetDb, typeof(TransitionDatabaseSO), false);
        m_config = (TransitionConfigSO)EditorGUILayout.ObjectField(
            "Config (推荐 Blend 公式)", m_config, typeof(TransitionConfigSO), false);
        m_skeletonRig = (GameObject)EditorGUILayout.ObjectField(
            "Skeleton Rig (Animator)", m_skeletonRig, typeof(GameObject), true);
        m_threshold = EditorGUILayout.Slider("PoseCost 阈值", m_threshold, 0f, 1f);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(m_targetDb == null || m_skeletonRig == null))
        {
            if (GUILayout.Button("Bake All", GUILayout.Height(28)))
            {
                BakeAll();
            }
        }

        if (!string.IsNullOrEmpty(m_lastReport))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bake Report", EditorStyles.boldLabel);
            m_scroll = EditorGUILayout.BeginScrollView(m_scroll, GUILayout.Height(220));
            EditorGUILayout.TextArea(m_lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    void BakeAll()
    {
        var animator = m_skeletonRig.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            EditorUtility.DisplayDialog("Transition Baker",
                "Skeleton Rig 必须有一个 Humanoid Animator。", "OK");
            return;
        }

        var clips = CollectAllClips();
        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog("Transition Baker", "未找到任何 ActionDataSO.MainClip。", "OK");
            return;
        }

        var minBlend = m_config != null ? m_config.MinRecommendedBlend : 0.03f;
        var maxBlend = m_config != null ? m_config.MaxRecommendedBlend : 0.20f;

        m_targetDb.EditorClearEntries();
        var totalPairs = clips.Count * clips.Count;
        var written = 0;

        try
        {
            var pairIdx = 0;
            for (var i = 0; i < clips.Count; i++)
            {
                for (var j = 0; j < clips.Count; j++)
                {
                    pairIdx++;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Transition Baker",
                            $"Sampling {clips[i].name} → {clips[j].name}  ({pairIdx}/{totalPairs})",
                            pairIdx / (float)totalPairs))
                    {
                        break;
                    }

                    var poseA = SamplePose(animator, clips[i], 1f);
                    var poseB = SamplePose(animator, clips[j], 0f);
                    var cost = PoseCostCalculator.ComputeCost(in poseA, in poseB);
                    if (cost < m_threshold) continue;

                    var recommended = PoseCostCalculator.RecommendedBlend(cost, minBlend, maxBlend);
                    m_targetDb.EditorAddEntry(new TransitionDatabaseSO.Entry
                    {
                        FromClip = clips[i],
                        ToClip = clips[j],
                        PoseCost = cost,
                        RecommendedBlend = recommended,
                        PhaseMatchSafe = false,
                        FootPhase = -1f,
                    });
                    written++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        m_targetDb.EditorSetBakeStats(
            System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            totalPairs,
            written);
        EditorUtility.SetDirty(m_targetDb);
        AssetDatabase.SaveAssets();

        var sb = new StringBuilder();
        sb.AppendLine($"[Transition Baker] {System.DateTime.Now:HH:mm}");
        sb.AppendLine($"Clips scanned: {clips.Count}");
        sb.AppendLine($"Total pairs: {totalPairs}");
        sb.AppendLine($"Written (cost > {m_threshold:F2}): {written}");
        sb.AppendLine($"Storage saved: ~{written * 40} bytes (估算)");
        m_lastReport = sb.ToString();
        Debug.Log(m_lastReport);
    }

    static List<AnimationClip> CollectAllClips()
    {
        var clips = new HashSet<AnimationClip>();
        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var action = AssetDatabase.LoadAssetAtPath<ActionDataSO>(path);
            if (action != null && action.MainClip != null) clips.Add(action.MainClip);
        }
        return new List<AnimationClip>(clips);
    }

    static PoseSample SamplePose(Animator animator, AnimationClip clip, float normalizedTime)
    {
        // Unity SampleAnimation 在 Editor 中可用
        var t = Mathf.Clamp01(normalizedTime) * clip.length;
        clip.SampleAnimation(animator.gameObject, t);

        var rotCount = (int)HumanBodyBones.LastBone;
        var rots = new Quaternion[rotCount];
        for (var b = 0; b < rotCount; b++)
        {
            var bone = animator.GetBoneTransform((HumanBodyBones)b);
            rots[b] = bone != null ? bone.localRotation : Quaternion.identity;
        }

        var hipBone = animator.GetBoneTransform(HumanBodyBones.Hips);
        var hipPos = hipBone != null ? hipBone.position : animator.transform.position;

        return new PoseSample
        {
            LocalRot = rots,
            HipPos = hipPos,
            HipVel = Vector3.zero, // 简化：差分留作 W6+ 增强
            FootPhase = -1f,
        };
    }
}
#endif
