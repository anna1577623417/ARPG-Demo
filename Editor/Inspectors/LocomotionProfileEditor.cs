#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// LocomotionProfile CustomEditor（160.2 L2–L3）—— Authority→Target 校验 / Sync / AutoFix。
/// </summary>
[CustomEditor(typeof(LocomotionProfile))]
public sealed class LocomotionProfileEditor : Editor
{
    SerializedProperty _enabledStates;
    SerializedProperty _features;
    SerializedProperty _bindings;
    SerializedProperty _tuning;
    SerializedProperty _validationSummary;

    void OnEnable()
    {
        _enabledStates = serializedObject.FindProperty("enabledStates");
        _features = serializedObject.FindProperty("features");
        _bindings = serializedObject.FindProperty("bindings");
        _tuning = serializedObject.FindProperty("tuning");
        _validationSummary = serializedObject.FindProperty("validationSummary");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var profile = (LocomotionProfile)target;
        var report = LocomotionProfileSyncAdapter.Validate(profile);
        RefreshValidationSummary(report);

        DrawValidationSection(profile, ref report);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("State Registry — Authority", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_enabledStates);
        EditorGUILayout.LabelField(
            "期望 Binding：" + LocomotionProfileSyncAdapter.FormatExpectedBindingBreakdown(profile),
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            "Strafe 子键 = 8 向 × WalkOnly/RunOnly（同方向两条不算重复）",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Feature Flags", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_features);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("State Bindings — Target", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_bindings, true);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Tuning", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_tuning);

        serializedObject.ApplyModifiedProperties();
    }

    void DrawValidationSection(LocomotionProfile profile, ref LocomotionProfileSyncAdapter.Report report)
    {
        EditorGUILayout.LabelField("Validation — Authority ↔ Bindings", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "资源：Binding 填 LocomotionAction（离散或 IsContinuousLocomotion）。旧 DiscreteAction/ContinuousClip 仅回落读取。\n" +
            "Sort by Enabled States：仅重排现有行，顺序与 Enabled States 下拉菜单声明序一致（Idle→Dead→Walk 组→…→TurnInPlaceDirected 在 Jump 组之后）。\n" +
            "Auto Fix：对齐 Enabled 全部行 + Strafe 8×2 + Turn 4 子键（去重/补缺/规范化），最后按菜单序排序。\n" +
            "仅排序请用 Sort by Enabled States。",
            MessageType.Info);

        if (report.IsClean)
        {
            EditorGUILayout.HelpBox("Authority 与 Bindings 一致。", MessageType.Info);
        }
        else
        {
            if (report.Duplicate.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Duplicate（Error）: " + string.Join(", ", report.Duplicate),
                    MessageType.Error);
            }

            if (report.MissingSimple.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Missing Simple: " + string.Join(", ", report.MissingSimple),
                    MessageType.Error);
            }

            if (report.MissingStrafe.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Missing Strafe 子键: " + string.Join(", ", report.MissingStrafe),
                    MessageType.Error);
            }

            if (report.MissingTurn.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Missing Turn 子键: " + string.Join(", ", report.MissingTurn),
                    MessageType.Error);
            }

            if (report.Unused.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Unused: " + string.Join(", ", report.Unused),
                    MessageType.Warning);
            }

            if (report.HasCountMismatch)
            {
                EditorGUILayout.HelpBox(
                    $"CountMismatch: 当前 {report.ActualBindingCount} 行，期望 {report.ExpectedBindingCount} 行 — 请点 Auto Fix。",
                    MessageType.Error);
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sync Bindings"))
        {
            Undo.RecordObject(profile, "Sync Locomotion Bindings");
            LocomotionProfileSyncAdapter.SyncSimpleBindings(profile, out _);
            LocomotionProfileEditorBindingSync.WriteBindingsToSerializedObject(serializedObject, profile);
            EditorUtility.SetDirty(profile);
            report = LocomotionProfileSyncAdapter.Validate(profile);
            RefreshValidationSummary(report);
        }

        if (GUILayout.Button("Auto Fix"))
        {
            Undo.RecordObject(profile, "Auto Fix Locomotion Bindings");
            var fix = LocomotionProfileSyncAdapter.AutoFix(profile);
            LocomotionProfileEditorBindingSync.WriteBindingsToSerializedObject(serializedObject, profile);
            if (fix.SanitizedEnabledStates && _enabledStates != null)
            {
                _enabledStates.intValue = (int)profile.EnabledStates;
            }

            serializedObject.Update();
            GUI.FocusControl(null);

            EditorUtility.SetDirty(profile);

            report = LocomotionProfileSyncAdapter.Validate(profile);
            RefreshValidationSummary(report);
            Repaint();
        }

        if (GUILayout.Button("Remove Unused…"))
        {
            if (report.Unused.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Remove Unused",
                    "当前没有 Unused Binding。",
                    "OK");
            }
            else if (EditorUtility.DisplayDialog(
                         "Remove Unused Bindings",
                         "将删除以下 Unused 行（含 Obsolete State）：\n\n"
                         + string.Join("\n", report.Unused)
                         + "\n\n此操作可 Ctrl+Z 撤销。",
                         "删除",
                         "取消"))
            {
                Undo.RecordObject(profile, "Remove Unused Locomotion Bindings");
                LocomotionProfileSyncAdapter.RemoveUnusedBindings(profile);
                LocomotionProfileEditorBindingSync.WriteBindingsToSerializedObject(serializedObject, profile);
                EditorUtility.SetDirty(profile);
                report = LocomotionProfileSyncAdapter.Validate(profile);
                RefreshValidationSummary(report);
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sort by Enabled States"))
        {
            Undo.RecordObject(profile, "Sort Locomotion Bindings");
            var sorted = LocomotionProfileSyncAdapter.SortBindingsByEnabledStates(profile);
            LocomotionProfileEditorBindingSync.WriteBindingsToSerializedObject(serializedObject, profile);
            serializedObject.Update();
            GUI.FocusControl(null);
            if (sorted)
            {
                EditorUtility.SetDirty(profile);
            }

            report = LocomotionProfileSyncAdapter.Validate(profile);
            RefreshValidationSummary(report);
            Repaint();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!profile.HasState(LocomotionStateFlag.StrafeLocomotion)))
        {
            if (GUILayout.Button("Expand Strafe Templates"))
            {
                Undo.RecordObject(profile, "Expand Strafe Templates");
                LocomotionProfileSyncAdapter.ExpandStrafeTemplates(profile);
                LocomotionProfileEditorBindingSync.WriteBindingsToSerializedObject(serializedObject, profile);
                EditorUtility.SetDirty(profile);
                report = LocomotionProfileSyncAdapter.Validate(profile);
                RefreshValidationSummary(report);
            }
        }

        using (new EditorGUI.DisabledScope(!profile.HasState(LocomotionStateFlag.TurnInPlaceDirected)))
        {
            if (GUILayout.Button("Expand Turn Templates"))
            {
                Undo.RecordObject(profile, "Expand Turn Templates");
                LocomotionProfileSyncAdapter.ExpandTurnTemplates(profile);
                LocomotionProfileEditorBindingSync.WriteBindingsToSerializedObject(serializedObject, profile);
                EditorUtility.SetDirty(profile);
                report = LocomotionProfileSyncAdapter.Validate(profile);
                RefreshValidationSummary(report);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (_validationSummary != null)
        {
            EditorGUILayout.LabelField("Cached Summary", EditorStyles.miniLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_validationSummary, GUIContent.none);
            EditorGUI.EndDisabledGroup();
        }
    }

    void RefreshValidationSummary(LocomotionProfileSyncAdapter.Report report)
    {
        if (_validationSummary == null)
        {
            return;
        }

        var summary = report.ToSummary();
        if (_validationSummary.stringValue != summary)
        {
            _validationSummary.stringValue = summary;
        }
    }
}
#endif
