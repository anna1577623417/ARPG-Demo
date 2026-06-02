#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 124.1 Combat-Flow MVP 资产生成 — 四向翻滚 / 地面&空中 Combo / 派生 / Graph 边。
/// 菜单：Tools/SkillRoute/Generate Combat Flow MVP (124.1)
/// </summary>
public static class CombatFlowMvpGenerator
{
    const string Root = "Assets/GameMain/Data/_Skills/CombatFlow_MVP";
    const string C1Root = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill";

    [MenuItem("Tools/SkillRoute/Generate Combat Flow MVP (124.1)")]
    public static void Generate()
    {
        EnsureDir(Root);
        EnsureDir($"{Root}/Stages");
        EnsureDir($"{Root}/Routes");
        EnsureDir($"{Root}/Entries");

        var rollAction = FindAction("General_Armature_Roll") ?? FindAction("Roll");
        var hookAction = FindAction("Melee_Hook") ?? FindAction("Sword_Attack");
        var comboA = LoadRoute($"{C1Root}/Route_Normal_A.asset");
        var comboB = LoadRoute($"{C1Root}/Route_Normal_B.asset");
        var comboContainer = LoadRoute($"{C1Root}/Route_Combo_LM.asset") as ComboRouteDefinition;
        var entryLm = LoadEntry($"{C1Root}/Entry_LM.asset");
        var loadout = LoadLoadout($"{C1Root}/Loadout_Skill_C1.asset");

        if (rollAction == null || comboA == null || comboB == null || comboContainer == null || entryLm == null || loadout == null)
        {
            Debug.LogError("[CombatFlow MVP] 缺少 Roll Action 或 C1 Combo/Entry/Loadout，请先确保 C1_Default_Skill 资产完整。");
            return;
        }

        var dodgeFwd = CreateNormal($"{Root}/Routes/Route_Dodge_Forward.asset", "Dodge Forward", rollAction);
        var dodgeBack = CreateNormal($"{Root}/Routes/Route_Dodge_Backward.asset", "Dodge Back", rollAction);
        var dodgeLeft = CreateNormal($"{Root}/Routes/Route_Dodge_Left.asset", "Dodge Left", rollAction);
        var dodgeRight = CreateNormal($"{Root}/Routes/Route_Dodge_Right.asset", "Dodge Right", rollAction);

        var airA = CreateNormal($"{Root}/Routes/Route_AirCombo_A.asset", "Air Combo A", comboA.FirstStage()?.Action ?? rollAction);
        var airB = CreateNormal($"{Root}/Routes/Route_AirCombo_B.asset", "Air Combo B", comboB.FirstStage()?.Action ?? rollAction);
        var airCombo = CreateCombo($"{Root}/Routes/Route_AirCombo_LM.asset", "Air Combo LM", new[] { airA, airB });

        var launcher = CreateNormal($"{Root}/Routes/Route_Launcher_Derive.asset", "Launcher", hookAction);
        var derive = CreateDerivative($"{Root}/Routes/Route_Derivative_Launcher.asset", comboA, launcher, SkillEntrySlot.RM);

        var graph = CreateGraph(
            $"{Root}/Combat_Graph_MVP.asset",
            new (string from, string to, SkillRouteDefinition route, SkillEntrySlot slot, InputSemanticType sem, MoveDirection8 dir, int pri)[]
            {
                ("Idle", "DodgeFwd", dodgeFwd, SkillEntrySlot.Space, InputSemanticType.Directional, MoveDirection8.Forward, 0),
                ("Idle", "DodgeBack", dodgeBack, SkillEntrySlot.Space, InputSemanticType.Directional, MoveDirection8.Backward, 1),
                ("Idle", "DodgeLeft", dodgeLeft, SkillEntrySlot.Space, InputSemanticType.Directional, MoveDirection8.Left, 2),
                ("Idle", "DodgeRight", dodgeRight, SkillEntrySlot.Space, InputSemanticType.Directional, MoveDirection8.Right, 3),
            },
            new[]
            {
                dodgeFwd, dodgeBack, dodgeLeft, dodgeRight,
                airA, airB, airCombo, launcher, derive, comboA, comboB, comboContainer,
            });

        PatchEntryLm(entryLm, airCombo);
        var entrySpace = BindEntryDirectional($"{Root}/Entries/Entry_Space.asset", dodgeFwd, dodgeBack, dodgeLeft, dodgeRight);
        var entryRm = BindEntryDerivative($"{Root}/Entries/Entry_RM_Derive.asset", derive);

        PatchLoadout(loadout, entrySpace, entryRm);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = graph;
        Debug.Log(
            "[CombatFlow MVP] 生成完成。\n" +
            "1. 将 Combat_Graph_MVP 拖到 Player → Combat Flow Graph\n" +
            "2. 开启 Player.DebugSkillRoute\n" +
            "3. Play：WASD+Space 翻滚；LM 地面 ABC；跳起 LM 空中 AB；命中后 RM 派生",
            graph);
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

    static SkillRouteDefinition LoadRoute(string path) =>
        AssetDatabase.LoadAssetAtPath<SkillRouteDefinition>(path);

    static SkillEntryDefinition LoadEntry(string path) =>
        AssetDatabase.LoadAssetAtPath<SkillEntryDefinition>(path);

    static SkillEntryLoadoutSO LoadLoadout(string path) =>
        AssetDatabase.LoadAssetAtPath<SkillEntryLoadoutSO>(path);

    static SkillStageDefinition CreateStage(string path, ActionDataSO action)
    {
        var s = AssetDatabase.LoadAssetAtPath<SkillStageDefinition>(path);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<SkillStageDefinition>();
            AssetDatabase.CreateAsset(s, path);
        }
        var so = new SerializedObject(s);
        so.FindProperty("action").objectReferenceValue = action;
        so.FindProperty("transitions").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        return s;
    }

    static NormalRouteDefinition CreateNormal(string path, string name, ActionDataSO action)
    {
        var stagePath = path.Replace("/Routes/", "/Stages/").Replace("_Route.asset", "_Stage.asset");
        var stage = CreateStage(stagePath, action);
        var r = AssetDatabase.LoadAssetAtPath<NormalRouteDefinition>(path);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<NormalRouteDefinition>();
            AssetDatabase.CreateAsset(r, path);
        }
        var so = new SerializedObject(r);
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("showOnHud").boolValue = false;
        so.FindProperty("stages").arraySize = 1;
        so.FindProperty("stages").GetArrayElementAtIndex(0).objectReferenceValue = stage;
        so.ApplyModifiedPropertiesWithoutUndo();
        return r;
    }

    static ComboRouteDefinition CreateCombo(string path, string name, SkillRouteDefinition[] chain)
    {
        var r = AssetDatabase.LoadAssetAtPath<ComboRouteDefinition>(path);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<ComboRouteDefinition>();
            AssetDatabase.CreateAsset(r, path);
        }
        var so = new SerializedObject(r);
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("showOnHud").boolValue = false;
        so.FindProperty("baseCooldownSeconds").floatValue = 0f;
        so.FindProperty("cooldownPolicy").enumValueIndex = (int)RouteCooldownPolicy.OnLastSubRouteEnd;
        var ch = so.FindProperty("comboChain");
        ch.arraySize = chain.Length;
        for (var i = 0; i < chain.Length; i++)
        {
            ch.GetArrayElementAtIndex(i).objectReferenceValue = chain[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return r;
    }

    static DerivativeRouteDefinition CreateDerivative(
        string path,
        SkillRouteDefinition parent,
        NormalRouteDefinition child,
        SkillEntrySlot triggerSlot)
    {
        var r = AssetDatabase.LoadAssetAtPath<DerivativeRouteDefinition>(path);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<DerivativeRouteDefinition>();
            AssetDatabase.CreateAsset(r, path);
        }
        var so = new SerializedObject(r);
        so.FindProperty("parentRoute").objectReferenceValue = parent;
        so.FindProperty("triggerSlot").enumValueIndex = (int)triggerSlot;
        so.FindProperty("stages").arraySize = 1;
        so.FindProperty("stages").arraySize = 1;
        so.FindProperty("stages").GetArrayElementAtIndex(0).objectReferenceValue = child?.FirstStage();
        so.ApplyModifiedPropertiesWithoutUndo();
        return r;
    }

    static CombatGraphAsset CreateGraph(
        string path,
        (string from, string to, SkillRouteDefinition route, SkillEntrySlot slot, InputSemanticType sem, MoveDirection8 dir, int pri)[] edges,
        SkillRouteDefinition[] registered)
    {
        var g = AssetDatabase.LoadAssetAtPath<CombatGraphAsset>(path);
        if (g == null)
        {
            g = ScriptableObject.CreateInstance<CombatGraphAsset>();
            AssetDatabase.CreateAsset(g, path);
        }
        var so = new SerializedObject(g);
        so.FindProperty("idleNodeId").stringValue = "Idle";
        WriteGraphEdges(so.FindProperty("edges"), edges);
        var reg = so.FindProperty("registeredRoutes");
        reg.arraySize = registered.Length;
        for (var i = 0; i < registered.Length; i++)
        {
            reg.GetArrayElementAtIndex(i).objectReferenceValue = registered[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return g;
    }

    static void WriteGraphEdges(
        SerializedProperty arr,
        (string from, string to, SkillRouteDefinition route, SkillEntrySlot slot, InputSemanticType sem, MoveDirection8 dir, int pri)[] edges)
    {
        arr.arraySize = edges.Length;
        for (var i = 0; i < edges.Length; i++)
        {
            var e = edges[i];
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("fromNodeId").stringValue = e.from;
            el.FindPropertyRelative("toNodeId").stringValue = e.to;
            el.FindPropertyRelative("targetRoute").objectReferenceValue = e.route;
            el.FindPropertyRelative("triggerSlot").enumValueIndex = (int)e.slot;
            el.FindPropertyRelative("triggerSemantic").enumValueIndex = (int)e.sem;
            el.FindPropertyRelative("priority").intValue = e.pri;
            WriteConditions(el.FindPropertyRelative("conditions"), new[] { SkillTransitionCondition.WithMoveDirection(e.dir) });
        }
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
            el.FindPropertyRelative("requiredMoveDirection").enumValueIndex = (int)c.RequiredMoveDirection;
        }
    }

    static void PatchEntryLm(SkillEntryDefinition entry, ComboRouteDefinition airCombo)
    {
        var so = new SerializedObject(entry);
        so.FindProperty("airComboRoute").objectReferenceValue = airCombo;
        var der = so.FindProperty("derivativeRoutes");
        if (der.arraySize == 0)
        {
            var derive = AssetDatabase.LoadAssetAtPath<DerivativeRouteDefinition>(
                $"{Root}/Routes/Route_Derivative_Launcher.asset");
            if (derive != null)
            {
                der.arraySize = 1;
                der.GetArrayElementAtIndex(0).objectReferenceValue = derive;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static SkillEntryDefinition BindEntryDirectional(
        string entryPath,
        NormalRouteDefinition fwd,
        NormalRouteDefinition back,
        NormalRouteDefinition left,
        NormalRouteDefinition right)
    {
        var groupPath = $"{C1Root}/SkillUnit/Group_Space_CombatFlow_MVP.asset";
        EnsureDir($"{C1Root}/SkillUnit");
        var group = SkillGroupFourDirEditorUtil.CreateOrUpdateFourDirGroup(
            groupPath,
            "Space Dodge",
            1f,
            fwd,
            back,
            left,
            right);

        var e = AssetDatabase.LoadAssetAtPath<SkillEntryDefinition>(entryPath);
        if (e == null)
        {
            e = ScriptableObject.CreateInstance<SkillEntryDefinition>();
            AssetDatabase.CreateAsset(e, entryPath);
        }

        var so = new SerializedObject(e);
        so.FindProperty("slot").enumValueIndex = (int)SkillEntrySlot.Space;
        so.ApplyModifiedPropertiesWithoutUndo();
        SkillGroupFourDirEditorUtil.BindEntryPrimaryGroup(e, group);
        return e;
    }

    static SkillEntryDefinition BindEntryDerivative(string path, DerivativeRouteDefinition derive)
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
        arr.arraySize = 1;
        arr.GetArrayElementAtIndex(0).objectReferenceValue = derive;
        so.ApplyModifiedPropertiesWithoutUndo();
        return e;
    }

    static void PatchLoadout(SkillEntryLoadoutSO loadout, SkillEntryDefinition space, SkillEntryDefinition rm)
    {
        var so = new SerializedObject(loadout);
        var b = so.FindProperty("bindings");
        var list = new System.Collections.Generic.List<(SkillEntrySlot, SkillEntryDefinition, string)>();
        for (var i = 0; i < b.arraySize; i++)
        {
            var el = b.GetArrayElementAtIndex(i);
            list.Add((
                (SkillEntrySlot)el.FindPropertyRelative("slot").enumValueIndex,
                el.FindPropertyRelative("entry").objectReferenceValue as SkillEntryDefinition,
                el.FindPropertyRelative("hudKeyLabel").stringValue));
        }

        void Upsert(SkillEntrySlot slot, SkillEntryDefinition entry, string label)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Item1 == slot)
                {
                    list[i] = (slot, entry, label);
                    return;
                }
            }
            list.Add((slot, entry, label));
        }

        Upsert(SkillEntrySlot.Space, space, "Space");
        Upsert(SkillEntrySlot.RM, rm, "RM");

        b.arraySize = list.Count;
        for (var i = 0; i < list.Count; i++)
        {
            var el = b.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("slot").enumValueIndex = (int)list[i].Item1;
            el.FindPropertyRelative("entry").objectReferenceValue = list[i].Item2;
            el.FindPropertyRelative("hudKeyLabel").stringValue = list[i].Item3;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
