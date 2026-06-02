#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 单轨 SkillRoute 范式 Demo 生成器 — Tools/SkillRoute/Generate Paradigm Demos (Ver4.6)。
/// </summary>
public static class SkillParadigmDemoGenerator
{
    const string Root = "Assets/GameMain/Data/_Skills/Paradigms";

    struct TrSpec
    {
        public StageTransitionTrigger Trigger;
        public float Open;
        public float Close;
        public SkillStageDefinition Next;
        public SkillTransitionCondition[] Conditions;
    }

    [MenuItem("Tools/SkillRoute/Generate Paradigm Demos (Ver4.6)")]
    public static void GenerateAll()
    {
        EnsureDir(Root);
        EnsureDir($"{Root}/Stages");
        EnsureDir($"{Root}/Routes");
        EnsureDir($"{Root}/Entries");

        var actQ1 = FindAction("Sword_Attack") ?? CreatePlaceholderAction($"{Root}/Stages/LeeSin_Q1_Action.asset");
        var actQ2 = FindAction("Melee_Hook") ?? CreatePlaceholderAction($"{Root}/Stages/LeeSin_Q2_Action.asset");
        var actKayn1 = FindAction("Sword_Dash") ?? CreatePlaceholderAction($"{Root}/Stages/Kayn_Dash_Action.asset");
        var actKayn2 = FindAction("Roll") ?? CreatePlaceholderAction($"{Root}/Stages/Kayn_Spin_Action.asset");
        var actLux0 = CreatePlaceholderAction($"{Root}/Stages/Lux_Throw_Action.asset");
        var actLux1 = CreatePlaceholderAction($"{Root}/Stages/Lux_Detonate_Action.asset");
        var actDash = FindAction("Sword_Dash") ?? CreatePlaceholderAction($"{Root}/Stages/Dash_Fwd_Action.asset");
        var actDodge = FindAction("Roll") ?? CreatePlaceholderAction($"{Root}/Stages/Dodge_Action.asset");
        var actLm = FindAction("Sword_Attack") ?? CreatePlaceholderAction($"{Root}/Stages/LM_Normal_Action.asset");
        var actRm = FindAction("Melee_Hook") ?? CreatePlaceholderAction($"{Root}/Stages/RM_Derive_Action.asset");

        var stQ2 = CreateStage($"{Root}/Stages/LeeSin_Q2_Stage.asset", actQ2, null);
        var stQ1 = CreateStage($"{Root}/Stages/LeeSin_Q1_Stage.asset", actQ1, null);
        var routeLeeSin = CreateMultiStage($"{Root}/Routes/LeeSin_Q_Route.asset", "LeeSin Q", new[] { stQ1, stQ2 },
            MultiStageChainMode.HitToAdvance, 5f, RouteCooldownPolicy.OnRouteEnd);

        var stKayn2 = CreateStage($"{Root}/Stages/Kayn_Q2_Stage.asset", actKayn2, null);
        var stKayn1 = CreateStage($"{Root}/Stages/Kayn_Q1_Stage.asset", actKayn1, new TrSpec
        {
            Trigger = StageTransitionTrigger.Auto,
            Open = 1f,
            Close = 1f,
            Next = stKayn2,
        });
        var routeKayn = CreateMultiStage($"{Root}/Routes/Kayn_Q_Route.asset", "Kayn Q", new[] { stKayn1, stKayn2 },
            MultiStageChainMode.AutoInRoute, 0f, RouteCooldownPolicy.OnLastStageEnd);

        var stLux1 = CreateStage($"{Root}/Stages/Lux_E1_Stage.asset", actLux1, null);
        var stLux0 = CreateStage($"{Root}/Stages/Lux_E0_Stage.asset", actLux0, null);
        var routeLux = CreateMultiStage($"{Root}/Routes/Lux_E_Route.asset", "Lux E", new[] { stLux0, stLux1 },
            MultiStageChainMode.PressToAdvance, 5f, RouteCooldownPolicy.OnRouteEnd,
            MultiStageArmTrigger.OnFirstStageComplete);

        var routeDash = CreateNormal($"{Root}/Routes/Shift_DashForward_Route.asset", "Dash Fwd", actDash);
        var routeDodge = CreateNormal($"{Root}/Routes/Shift_Dodge_Route.asset", "Dodge", actDodge);
        var shiftGroup = SkillGroupFourDirEditorUtil.CreateOrUpdateFourDirGroup(
            $"{Root}/Routes/Group_Shift_Directional.asset",
            "Shift Directional",
            1f,
            routeDash,
            routeDodge,
            routeDodge,
            routeDodge);

        var routeLm = CreateNormal($"{Root}/Routes/LM_Normal_Route.asset", "LM", actLm);
        var routeDer = CreateDerivative($"{Root}/Routes/RM_Derivative_Route.asset", routeLm, actRm);

        var entryQLeeSin = BindEntry($"{Root}/Entries/Entry_Q_LeeSin.asset", SkillEntrySlot.Q, routeLeeSin);
        var entryQKayn = BindEntry($"{Root}/Entries/Entry_Q_Kayn.asset", SkillEntrySlot.Q, routeKayn);
        var entryQLux = BindEntry($"{Root}/Entries/Entry_Q_Lux.asset", SkillEntrySlot.Q, routeLux);
        var entryShift = BindEntryShiftWithGroup($"{Root}/Entries/Entry_Shift.asset", shiftGroup, routeDodge);
        var entryLm = BindEntryNormal($"{Root}/Entries/Entry_LM.asset", SkillEntrySlot.LM, routeLm);
        var entryRm = BindEntryDerivative($"{Root}/Entries/Entry_RM.asset", routeDer);

        var loadout = CreateLoadout($"{Root}/Paradigm_Player_Loadout.asset", new[]
        {
            (SkillEntrySlot.LM, entryLm, "LMB"),
            (SkillEntrySlot.RM, entryRm, "RMB"),
            (SkillEntrySlot.Q, entryQLeeSin, "Q"),
            (SkillEntrySlot.Shift, entryShift, "Shift"),
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = loadout;
        Debug.Log(
            "[Paradigm] Generated. Swap Q entry reference (LeeSin/Kayn/Lux) on Paradigm_Player_Loadout for each test.\n" +
            "Lux: call SkillEntries.NotifySkillAnchorReady() when anchor lands (or wire SpawnAreaAnchorProjectile).",
            loadout);
    }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
        {
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    static ActionDataSO FindAction(string token)
    {
        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        for (var i = 0; i < guids.Length; i++)
        {
            var p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (p.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AssetDatabase.LoadAssetAtPath<ActionDataSO>(p);
            }
        }
        return null;
    }

    static ActionDataSO CreatePlaceholderAction(string path)
    {
        var a = AssetDatabase.LoadAssetAtPath<ActionDataSO>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<ActionDataSO>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    static SkillStageDefinition CreateStage(string path, ActionDataSO action, TrSpec? tr)
    {
        var s = AssetDatabase.LoadAssetAtPath<SkillStageDefinition>(path);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<SkillStageDefinition>();
            AssetDatabase.CreateAsset(s, path);
        }
        var so = new SerializedObject(s);
        so.FindProperty("action").objectReferenceValue = action;
        var tprop = so.FindProperty("transitions");
        if (tr == null)
        {
            tprop.arraySize = 0;
        }
        else
        {
            tprop.arraySize = 1;
            var el = tprop.GetArrayElementAtIndex(0);
            var t = tr.Value;
            el.FindPropertyRelative("trigger").enumValueIndex = (int)t.Trigger;
            el.FindPropertyRelative("openTime").floatValue = t.Open;
            el.FindPropertyRelative("closeTime").floatValue = t.Close;
            el.FindPropertyRelative("nextStage").objectReferenceValue = t.Next;
            WriteConditions(el.FindPropertyRelative("conditions"), t.Conditions);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return s;
    }

    static void WriteConditions(SerializedProperty arr, SkillTransitionCondition[] conds)
    {
        if (conds == null || conds.Length == 0)
        {
            arr.arraySize = 0;
            return;
        }
        arr.arraySize = conds.Length;
        for (var i = 0; i < conds.Length; i++)
        {
            var c = conds[i];
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("kind").enumValueIndex = (int)c.Kind;
            el.FindPropertyRelative("requiredHitCount").intValue = c.RequiredHitCount;
            el.FindPropertyRelative("targetRule").enumValueIndex = (int)c.TargetRule;
            el.FindPropertyRelative("requireInputAgain").boolValue = c.RequireInputAgain;
            el.FindPropertyRelative("expectedSlot").enumValueIndex = (int)c.ExpectedSlot;
            el.FindPropertyRelative("minElapsedSeconds").floatValue = c.MinElapsedSeconds;
            el.FindPropertyRelative("requiredMechanicTags").ulongValue = c.RequiredMechanicTags;
        }
    }

    static MultiStageRouteDefinition CreateMultiStage(
        string path,
        string name,
        SkillStageDefinition[] stages,
        MultiStageChainMode chainMode,
        float armSec,
        RouteCooldownPolicy cdPolicy,
        MultiStageArmTrigger armTrigger = MultiStageArmTrigger.OnFirstStageComplete)
    {
        var r = AssetDatabase.LoadAssetAtPath<MultiStageRouteDefinition>(path);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<MultiStageRouteDefinition>();
            AssetDatabase.CreateAsset(r, path);
        }
        var so = new SerializedObject(r);
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("chainMode").enumValueIndex = (int)chainMode;
        so.FindProperty("stageChainArmSeconds").floatValue = armSec;
        so.FindProperty("armNextStageTrigger").enumValueIndex = (int)armTrigger;
        so.FindProperty("startCooldownOnArmTimeout").boolValue = true;
        so.FindProperty("cooldownPolicy").enumValueIndex = (int)cdPolicy;
        var st = so.FindProperty("stages");
        st.arraySize = stages.Length;
        for (var i = 0; i < stages.Length; i++)
        {
            st.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return r;
    }

    static NormalRouteDefinition CreateNormal(string path, string name, ActionDataSO action)
    {
        var stagePath = path.Replace("/Routes/", "/Stages/").Replace("_Route.asset", "_Stage.asset");
        var stage = CreateStage(stagePath, action, null);
        var r = AssetDatabase.LoadAssetAtPath<NormalRouteDefinition>(path);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<NormalRouteDefinition>();
            AssetDatabase.CreateAsset(r, path);
        }
        var so = new SerializedObject(r);
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("stages").arraySize = 1;
        so.FindProperty("stages").GetArrayElementAtIndex(0).objectReferenceValue = stage;
        so.ApplyModifiedPropertiesWithoutUndo();
        return r;
    }

    static SkillEntryDefinition BindEntryShiftWithGroup(string path, SkillGroupDefinition group, NormalRouteDefinition n)
    {
        var e = AssetDatabase.LoadAssetAtPath<SkillEntryDefinition>(path);
        if (e == null)
        {
            e = ScriptableObject.CreateInstance<SkillEntryDefinition>();
            AssetDatabase.CreateAsset(e, path);
        }

        var so = new SerializedObject(e);
        so.FindProperty("slot").enumValueIndex = (int)SkillEntrySlot.Shift;
        so.ApplyModifiedPropertiesWithoutUndo();
        SkillGroupFourDirEditorUtil.BindEntryPrimaryGroup(e, group, n);
        return e;
    }

    static DerivativeRouteDefinition CreateDerivative(string path, SkillRouteDefinition parent, ActionDataSO action)
    {
        var stagePath = path.Replace("/Routes/", "/Stages/").Replace("_Route.asset", "_Stage.asset");
        var stage = CreateStage(stagePath, action, null);
        var r = AssetDatabase.LoadAssetAtPath<DerivativeRouteDefinition>(path);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<DerivativeRouteDefinition>();
            AssetDatabase.CreateAsset(r, path);
        }
        var so = new SerializedObject(r);
        so.FindProperty("parentRoute").objectReferenceValue = parent;
        so.FindProperty("triggerSlot").enumValueIndex = (int)SkillEntrySlot.RM;
        so.FindProperty("stages").arraySize = 1;
        so.FindProperty("stages").GetArrayElementAtIndex(0).objectReferenceValue = stage;
        so.ApplyModifiedPropertiesWithoutUndo();
        return r;
    }

    static SkillEntryDefinition BindEntry(string path, SkillEntrySlot slot, MultiStageRouteDefinition ms) =>
        BindEntryCore(path, slot, ms, null);

    static SkillEntryDefinition BindEntryNormal(string path, SkillEntrySlot slot, NormalRouteDefinition n) =>
        BindEntryCore(path, slot, null, n);

    static SkillEntryDefinition BindEntryDerivative(string path, DerivativeRouteDefinition[] ders)
    {
        var e = AssetDatabase.LoadAssetAtPath<SkillEntryDefinition>(path);
        if (e == null)
        {
            e = ScriptableObject.CreateInstance<SkillEntryDefinition>();
            AssetDatabase.CreateAsset(e, path);
        }
        var so = new SerializedObject(e);
        so.FindProperty("slot").enumValueIndex = (int)SkillEntrySlot.RM;
        var arr = so.FindProperty("derivativeRoutes");
        arr.arraySize = ders.Length;
        for (var i = 0; i < ders.Length; i++)
        {
            arr.GetArrayElementAtIndex(i).objectReferenceValue = ders[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return e;
    }

    static SkillEntryDefinition BindEntryDerivative(string path, DerivativeRouteDefinition d) =>
        BindEntryDerivative(path, new[] { d });

    static SkillEntryDefinition BindEntryCore(
        string path,
        SkillEntrySlot slot,
        MultiStageRouteDefinition ms,
        NormalRouteDefinition n)
    {
        var e = AssetDatabase.LoadAssetAtPath<SkillEntryDefinition>(path);
        if (e == null)
        {
            e = ScriptableObject.CreateInstance<SkillEntryDefinition>();
            AssetDatabase.CreateAsset(e, path);
        }

        var so = new SerializedObject(e);
        so.FindProperty("slot").enumValueIndex = (int)slot;
        so.FindProperty("multiStageRoute").objectReferenceValue = ms;
        so.FindProperty("normalRoute").objectReferenceValue = n;
        so.ApplyModifiedPropertiesWithoutUndo();
        return e;
    }

    static SkillEntryLoadoutSO CreateLoadout(string path, (SkillEntrySlot, SkillEntryDefinition, string)[] rows)
    {
        var l = AssetDatabase.LoadAssetAtPath<SkillEntryLoadoutSO>(path);
        if (l == null)
        {
            l = ScriptableObject.CreateInstance<SkillEntryLoadoutSO>();
            AssetDatabase.CreateAsset(l, path);
        }
        var so = new SerializedObject(l);
        var b = so.FindProperty("bindings");
        b.arraySize = rows.Length;
        for (var i = 0; i < rows.Length; i++)
        {
            var el = b.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("slot").enumValueIndex = (int)rows[i].Item1;
            el.FindPropertyRelative("entry").objectReferenceValue = rows[i].Item2;
            el.FindPropertyRelative("hudKeyLabel").stringValue = rows[i].Item3;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return l;
    }
}
#endif
