#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataTimelineEditor
{
    SerializedProperty _previewTimeMarkers;

    void DrawPreviewTimeMarkersSection()
    {
        if (_previewTimeMarkers == null)
        {
            return;
        }

        ActionTimelineEditorUI.LightSectionSeparator("Future Position Markers");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Add", EditorStyles.miniButtonLeft, GUILayout.Width(52f)))
            {
                Undo.RecordObject(_action, "Add Preview Time Marker");
                _previewTimeMarkers.arraySize++;
                _previewTimeMarkers
                    .GetArrayElementAtIndex(_previewTimeMarkers.arraySize - 1)
                    .floatValue = Mathf.Clamp01(_previewTime);
                EditorUtility.SetDirty(_action);
            }

            if (GUILayout.Button("× Clear", EditorStyles.miniButtonMid, GUILayout.Width(56f)))
            {
                if (_previewTimeMarkers.arraySize > 0
                    && EditorUtility.DisplayDialog("Clear Markers", "清空所有 Preview Time Markers？", "Clear", "Cancel"))
                {
                    Undo.RecordObject(_action, "Clear Preview Time Markers");
                    _previewTimeMarkers.ClearArray();
                    EditorUtility.SetDirty(_action);
                }
            }

            if (GUILayout.Button("Snap All to Frames", EditorStyles.miniButtonMid))
            {
                SnapAllPreviewMarkersToFrames();
            }

            if (GUILayout.Button("Distribute Evenly", EditorStyles.miniButtonRight))
            {
                DistributePreviewMarkersEvenly();
            }
        }

        var removeIndex = -1;
        for (var i = 0; i < _previewTimeMarkers.arraySize; i++)
        {
            var element = _previewTimeMarkers.GetArrayElementAtIndex(i);
            var value = element.floatValue;

            using (new EditorGUILayout.HorizontalScope())
            {
                var dotRect = GUILayoutUtility.GetRect(12f, 18f, GUILayout.Width(14f));
                EditorGUI.DrawRect(new Rect(dotRect.x + 2f, dotRect.y + 6f, 8f, 8f), ActionTimelineGizmoColors.FutureMark);

                var typed = EditorGUILayout.DelayedFloatField(value, GUILayout.Width(52f));
                if (!Mathf.Approximately(typed, value))
                {
                    element.floatValue = Mathf.Clamp01(typed);
                }

                var slider = EditorGUILayout.Slider(element.floatValue, 0f, 1f);
                if (!Mathf.Approximately(slider, element.floatValue))
                {
                    element.floatValue = slider;
                }

                if (GUILayout.Button("─", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    element.floatValue = Mathf.Clamp01(_previewTime);
                }

                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    removeIndex = i;
                }
            }
        }

        if (removeIndex >= 0)
        {
            Undo.RecordObject(_action, "Remove Preview Time Marker");
            _previewTimeMarkers.DeleteArrayElementAtIndex(removeIndex);
            EditorUtility.SetDirty(_action);
        }
    }

    void SnapAllPreviewMarkersToFrames()
    {
        if (_action?.MainClip == null || _previewTimeMarkers == null || _previewTimeMarkers.arraySize == 0)
        {
            return;
        }

        const float frameStep = 1f / 30f;
        var clipLen = Mathf.Max(0.001f, _action.MainClip.length);
        Undo.RecordObject(_action, "Snap Preview Markers To Frames");
        for (var i = 0; i < _previewTimeMarkers.arraySize; i++)
        {
            var t = _previewTimeMarkers.GetArrayElementAtIndex(i).floatValue;
            t = Mathf.Round(t * clipLen / frameStep) * frameStep / clipLen;
            _previewTimeMarkers.GetArrayElementAtIndex(i).floatValue = Mathf.Clamp01(t);
        }

        EditorUtility.SetDirty(_action);
    }

    void DistributePreviewMarkersEvenly()
    {
        if (_previewTimeMarkers == null)
        {
            return;
        }

        var count = _previewTimeMarkers.arraySize;
        if (count <= 0)
        {
            return;
        }

        Undo.RecordObject(_action, "Distribute Preview Markers");
        if (count == 1)
        {
            _previewTimeMarkers.GetArrayElementAtIndex(0).floatValue = 0.5f;
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                var t = (i + 1f) / (count + 1f);
                _previewTimeMarkers.GetArrayElementAtIndex(i).floatValue = t;
            }
        }

        EditorUtility.SetDirty(_action);
    }
}
#endif
