#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ComboRouteDefinition))]
public sealed class ComboRouteDefinitionEditor : UnityEditor.Editor
{
    SerializedProperty _comboChain;
    SerializedProperty _comboTransitions;
    SerializedProperty _sessionReset;
    SerializedProperty _allowEarlyBuffer;
    SerializedProperty _cooldownPolicy;
    SerializedProperty _baseCd;

    void OnEnable()
    {
        _comboChain = serializedObject.FindProperty("comboChain");
        _comboTransitions = serializedObject.FindProperty("comboTransitions");
        _sessionReset = serializedObject.FindProperty("comboSessionResetTime");
        _allowEarlyBuffer = serializedObject.FindProperty("allowEarlyBufferInput");
        _cooldownPolicy = serializedObject.FindProperty("cooldownPolicy");
        _baseCd = serializedObject.FindProperty("baseCooldownSeconds");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "【116.1 Transition 边】N 节点 → N-1 条边；Session Reset 同步 Resolver。\n" +
            "【结算】容器推荐：Cooldown = On Last SubRoute End；Resource = On First SubRoute Start。\n" +
            "SubRoute 的 BaseCooldown=0，勿与容器重复扣费/CD。",
            MessageType.Info);

        EditorGUILayout.PropertyField(_sessionReset, new GUIContent("Combo Session Reset Time"));
        EditorGUILayout.PropertyField(_allowEarlyBuffer);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Container Cooldown / Cost", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_cooldownPolicy, new GUIContent("Cooldown Policy"));
        EditorGUILayout.PropertyField(_baseCd, new GUIContent("Base Cooldown (整套连招总 CD)"));

        DrawDefaultInspectorExcluding(
            "comboChain", "comboTransitions", "comboSessionResetTime", "allowEarlyBufferInput",
            "linkInputWindows", "fallbackToFirstOnExpire", "cooldownPolicy", "baseCooldownSeconds");

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Combo Chain (Nodes + Transitions)", EditorStyles.boldLabel);

        var combo = (ComboRouteDefinition)target;
        var chainLen = _comboChain.arraySize;

        for (var i = 0; i < chainLen; i++)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Node {i}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_comboChain.GetArrayElementAtIndex(i), new GUIContent("SubRoute"));

            if (i < chainLen - 1)
            {
                var fromName = GetRouteName(_comboChain.GetArrayElementAtIndex(i));
                var toName = GetRouteName(_comboChain.GetArrayElementAtIndex(i + 1));
                EditorGUILayout.LabelField($"Transition → {fromName}  →  {toName}", EditorStyles.miniLabel);

                EnsureTransitionElement(i);
                var trans = _comboTransitions.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(trans.FindPropertyRelative("minInputTime"), new GUIContent("Min Input Time"));
                EditorGUILayout.PropertyField(trans.FindPropertyRelative("maxInputTime"), new GUIContent("Max Input Time (0=Session)"));
                EditorGUILayout.PropertyField(trans.FindPropertyRelative("requireHit"), new GUIContent("Require Hit"));
                EditorGUILayout.PropertyField(trans.FindPropertyRelative("allowInputBuffer"), new GUIContent("Allow Buffer"));
            }
            else
            {
                EditorGUILayout.LabelField("└ End", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Node"))
        {
            _comboChain.InsertArrayElementAtIndex(chainLen);
            SyncTransitionArraySize();
        }
        if (chainLen > 0 && GUILayout.Button("- Remove Last Node"))
        {
            _comboChain.arraySize--;
            SyncTransitionArraySize();
        }
        EditorGUILayout.EndHorizontal();

        if (chainLen > 0 && (_comboTransitions == null || _comboTransitions.arraySize != chainLen - 1))
        {
            EditorGUILayout.HelpBox(
                $"Transition 数量应为 {chainLen - 1}，当前 {_comboTransitions?.arraySize ?? 0}。保存资产或调整节点后会自动同步。",
                MessageType.Warning);
            if (GUILayout.Button("Sync Transition Count"))
            {
                SyncTransitionArraySize();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    void DrawDefaultInspectorExcluding(params string[] exclude)
    {
        var prop = serializedObject.GetIterator();
        var enter = true;
        while (prop.NextVisible(enter))
        {
            enter = false;
            if (prop.name == "m_Script") continue;
            foreach (var ex in exclude)
            {
                if (prop.name == ex) goto next;
            }
            EditorGUILayout.PropertyField(prop, true);
            next: ;
        }
    }

    static string GetRouteName(SerializedProperty routeProp)
    {
        var obj = routeProp.objectReferenceValue;
        return obj != null ? obj.name : "<null>";
    }

    void EnsureTransitionElement(int edgeIndex)
    {
        while (_comboTransitions.arraySize <= edgeIndex)
        {
            _comboTransitions.InsertArrayElementAtIndex(_comboTransitions.arraySize);
        }
    }

    void SyncTransitionArraySize()
    {
        var nodeCount = _comboChain.arraySize;
        var edgeCount = Mathf.Max(0, nodeCount - 1);
        _comboTransitions.arraySize = edgeCount;
        for (var i = 0; i < edgeCount; i++)
        {
            var el = _comboTransitions.GetArrayElementAtIndex(i);
            if (el.FindPropertyRelative("minInputTime").floatValue <= 0f && i > 0)
            {
                el.FindPropertyRelative("minInputTime").floatValue = 0.2f;
            }
        }
    }
}
#endif
