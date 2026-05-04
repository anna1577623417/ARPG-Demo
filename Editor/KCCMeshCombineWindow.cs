using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditorInternal;

/// <summary>
/// KCC 友好：将选中层级内的网格合并为单个 MeshCollider，写入资产并可选剔除原碰撞体。
/// 菜单：Tools → KCC → Mesh Combine Tool
/// </summary>
public sealed class KCCMeshCombineWindow : EditorWindow {
    enum MeshSourceMode {
        MeshCollidersOnly,
        MeshFiltersOnly,
        CollidersAndFiltersDedup,
    }

    MeshSourceMode _sourceMode = MeshSourceMode.MeshCollidersOnly;

    LayerMask _layerMask = ~0;

    bool _addMeshRenderer;
    Material _fallbackMaterial;

    bool _destroyOriginalRoots;
    bool _disableOriginalRoots = true;

    bool _stripMeshCollidersFromSources = true;

    bool _bakeBoxColliders;

    bool _bakeSphereColliders;

    bool _bakeCapsuleColliders;

    bool _stripBakedPrimitives = true;

    bool _convexCollider;

    string _meshAssetRelativeFolder = "Assets/GameMain/CombinedCollisionMeshes";
    string _assetBaseName = "Combined";

    const string PrefKeyFolder = "KCCMeshCombine.AssetFolder";

    [MenuItem("Tools/KCC/Mesh Combine Tool")]
    static void Open() {
        var w = GetWindow<KCCMeshCombineWindow>("KCC Mesh Combine");
        w.minSize = new Vector2(380f, 560f);
    }

    void OnEnable() {
        var saved = EditorPrefs.GetString(PrefKeyFolder, _meshAssetRelativeFolder);
        if (!string.IsNullOrEmpty(saved)) {
            _meshAssetRelativeFolder = saved;
        }
    }

    void OnGUI() {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("合并来源", EditorStyles.boldLabel);
        _sourceMode =
            (MeshSourceMode)EditorGUILayout.EnumPopup("网格来源", _sourceMode);

        EditorGUILayout.HelpBox(
            "MeshCollidersOnly：只采 MeshCollider.sharedMesh（推荐做碰撞管线）。\n" +
            "MeshFiltersOnly：只采 MeshFilter（无碰撞也可用渲染网格烘焙）。\n" +
            "CollidersAndFiltersDedup：两者都采；同一物体上 MF 与 MC 共用同一 Mesh 实例时只合并一次。",
            MessageType.None);

        _layerMask = DrawLayerMaskField("包含 Layer", _layerMask);

        EditorGUILayout.LabelField("解析碰撞体烘焙", EditorStyles.boldLabel);
        _bakeBoxColliders =
            EditorGUILayout.Toggle(
                new GUIContent(
                    "烘焙 BoxCollider → 网格",
                    "无 CSG：按 center/size 生成立方体三角网，再与 Mesh 一起做世界空间 Combine。"),
                _bakeBoxColliders);
        _bakeSphereColliders =
            EditorGUILayout.Toggle(
                new GUIContent(
                    "烘焙 SphereCollider → 网格",
                    "用内置单位球拓扑按 radius 缩放，非完美球但与 PhysX Sphere 同属一类近似。"),
                _bakeSphereColliders);
        _bakeCapsuleColliders =
            EditorGUILayout.Toggle(
                new GUIContent(
                    "烘焙 CapsuleCollider → 网格",
                    "用内置 Capsule 拓扑缩放，Direction 会与 Transform 轴向对齐近似。"),
                _bakeCapsuleColliders);
        using (new EditorGUI.DisabledScope(
                   !(_bakeBoxColliders || _bakeSphereColliders || _bakeCapsuleColliders))) {
            _stripBakedPrimitives = EditorGUILayout.Toggle(
                new GUIContent(
                    "Strip 已烘焙的 Box/Sphere/Capsule",
                    "合并成功后删除这些 Primitive 组件（不写 Undo 组合；单步 Undo 仍撤销整窗操作）。"),
                _stripBakedPrimitives);
        }

        EditorGUILayout.HelpBox(
            "Box/Sphere/Capsule 本身没有顶点表，必须烘焙成 Mesh 才能进 Combine。" +
            "\n⚠️ 合并 ≠ 实心并集「填缝」：两箱摆放若留缝，生成的 Mesh 在中间仍为空腔；需在建模/Snapping 对齐或改用 CSG/Voxel。\n" +
            "⚠️ 与其它 Mesh 重合面不会自动 Weld，极小重叠仍可产生薄壳；依赖场景对齐与合并墙策略。",
            MessageType.Warning);

        EditorGUILayout.Space(8f);

        EditorGUILayout.LabelField("产物", EditorStyles.boldLabel);
        _addMeshRenderer = EditorGUILayout.Toggle("附加 MeshRenderer（调试用）", _addMeshRenderer);
        using (new EditorGUI.DisabledScope(!_addMeshRenderer)) {
            _fallbackMaterial = (Material)EditorGUILayout.ObjectField("备用材质（粉球）", _fallbackMaterial,
                typeof(Material), false);
        }

        _convexCollider = EditorGUILayout.Toggle(
            new GUIContent(
                "Convex Collider",
                "静态墙角请保持关闭。Convex 对大合并体常无效或与物理不匹配。"),
            _convexCollider);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("资产保存（必须）", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _meshAssetRelativeFolder = EditorGUILayout.TextField("相对目录", _meshAssetRelativeFolder);
        if (GUILayout.Button("...", GUILayout.Width(28f))) {
            var abs = EditorUtility.OpenFolderPanel("选择保存目录（项目内）", Application.dataPath, "");
            if (!string.IsNullOrEmpty(abs)) {
                abs = abs.Replace('\\', '/');
                var dataPath = Application.dataPath.Replace('\\', '/');
                if (abs.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) {
                    _meshAssetRelativeFolder =
                        "Assets" + abs.Substring(dataPath.Length);
                    EditorPrefs.SetString(PrefKeyFolder, _meshAssetRelativeFolder);
                } else {
                    Debug.LogWarning("[KCCMeshCombine] 请选择项目位于 Assets 下的目录。");
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        _assetBaseName =
            EditorGUILayout.TextField(new GUIContent("资产名前缀", "不含扩展名，自动防重名"), _assetBaseName);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("原物体清理", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Strip：仅删除层级内曾参与合并的 MeshCollider。\nDestroy/Disable roots：对整个选中根节点生效。",
            MessageType.None);

        _stripMeshCollidersFromSources =
            EditorGUILayout.Toggle(new GUIContent("Strip 源 MeshCollider", "移除已合并的 MeshCollider"), _stripMeshCollidersFromSources);

        _disableOriginalRoots =
            EditorGUILayout.Toggle("Disable Original Roots", _disableOriginalRoots);
        using (new EditorGUI.DisabledScope(_destroyOriginalRoots)) {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("(仅当未 Destroy)", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        _destroyOriginalRoots =
            EditorGUILayout.Toggle("Destroy Original Roots", _destroyOriginalRoots);

        if (_destroyOriginalRoots) {
            _disableOriginalRoots = false;
        }

        EditorGUILayout.Space(12f);

        using (new EditorGUILayout.HorizontalScope()) {
            if (GUILayout.Button("Combine Selected", GUILayout.Height(32f))) {
                CombineSelection();
            }
        }
    }

    static bool LayerIncluded(LayerMask mask, int layer) {
        return (mask.value & (1 << layer)) != 0;
    }

    void CombineSelection() {
        var roots = Selection.gameObjects;
        if (roots == null || roots.Length == 0) {
            Debug.LogWarning("[KCCMeshCombine] 未选中任何物体。");
            return;
        }

        var combines = new List<CombineInstance>(256);
        var meshCollidersSeen = new List<MeshCollider>();
        var primitivesToStrip = new List<Collider>();
        var dedupe = new HashSet<ulong>();
        var tempMeshes = new List<Mesh>(4);

        try {
            Mesh unitCubeMesh = null;
            Mesh unitSphereMesh = null;
            Mesh unitCapsuleMesh = null;
            if (_bakeBoxColliders) {
                unitCubeMesh = CopyTemporaryBuiltinMesh(PrimitiveType.Cube);
                tempMeshes.Add(unitCubeMesh);
            }

            if (_bakeSphereColliders) {
                unitSphereMesh = CopyTemporaryBuiltinMesh(PrimitiveType.Sphere);
                tempMeshes.Add(unitSphereMesh);
            }

            if (_bakeCapsuleColliders) {
                unitCapsuleMesh = CopyTemporaryBuiltinMesh(PrimitiveType.Capsule);
                tempMeshes.Add(unitCapsuleMesh);
            }

            foreach (var root in roots) {
                if (!root) {
                    continue;
                }

                var trs = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in trs) {
                    var go = t.gameObject;
                    if (!LayerIncluded(_layerMask, go.layer)) {
                        continue;
                    }

                    Mesh meshFromFilter = null;
                    Mesh meshFromCollider = null;

                    if (_sourceMode is MeshSourceMode.MeshFiltersOnly or MeshSourceMode.CollidersAndFiltersDedup) {
                        var mf = go.GetComponent<MeshFilter>();
                        meshFromFilter = mf != null ? mf.sharedMesh : null;
                    }

                    if (_sourceMode is MeshSourceMode.MeshCollidersOnly or MeshSourceMode.CollidersAndFiltersDedup) {
                        var mc = go.GetComponent<MeshCollider>();
                        if (mc != null && mc.sharedMesh != null) {
                            meshFromCollider = mc.sharedMesh;
                            meshCollidersSeen.Add(mc);
                        }
                    }

                    void TryAddMesh(Mesh m) {
                        if (m == null) {
                            return;
                        }

                        var key =
                            unchecked((ulong)(uint)go.GetInstanceID())
                            ^ ((ulong)(uint)m.GetInstanceID() << 32);
                        if (_sourceMode == MeshSourceMode.CollidersAndFiltersDedup && !dedupe.Add(key)) {
                            return;
                        }

                        combines.Add(new CombineInstance {
                            mesh = m,
                            transform = t.localToWorldMatrix,
                        });
                    }

                    switch (_sourceMode) {
                        case MeshSourceMode.MeshCollidersOnly:
                            TryAddMesh(meshFromCollider);
                            break;
                        case MeshSourceMode.MeshFiltersOnly:
                            TryAddMesh(meshFromFilter);
                            break;
                        default:
                            if (meshFromCollider != null) {
                                TryAddMesh(meshFromCollider);
                            }

                            if (meshFromFilter != null && meshFromFilter != meshFromCollider) {
                                TryAddMesh(meshFromFilter);
                            }

                            break;
                    }

                    void AddCombine(Mesh m, Matrix4x4 bakeLocal) {
                        if (m == null) {
                            return;
                        }

                        combines.Add(new CombineInstance {
                            mesh = m,
                            transform = t.localToWorldMatrix * bakeLocal,
                        });
                    }

                    if (_bakeBoxColliders && unitCubeMesh != null) {
                        var box = go.GetComponent<BoxCollider>();
                        if (box != null && box.enabled) {
                            AddCombine(unitCubeMesh, Matrix4x4.TRS(box.center, Quaternion.identity, box.size));
                            primitivesToStrip.Add(box);
                        }
                    }

                    if (_bakeSphereColliders && unitSphereMesh != null) {
                        var sp = go.GetComponent<SphereCollider>();
                        if (sp != null && sp.enabled) {
                            var er = Mathf.Max(unitSphereMesh.bounds.extents.x, 1e-6f);
                            var scl = Vector3.one * (sp.radius / er);
                            AddCombine(unitSphereMesh, Matrix4x4.TRS(sp.center, Quaternion.identity, scl));
                            primitivesToStrip.Add(sp);
                        }
                    }

                    if (_bakeCapsuleColliders && unitCapsuleMesh != null) {
                        var caps = go.GetComponent<CapsuleCollider>();
                        if (caps != null && caps.enabled) {
                            AddCombine(unitCapsuleMesh, CapsuleBakeLocalMatrix(caps, unitCapsuleMesh.bounds));
                            primitivesToStrip.Add(caps);
                        }
                    }
                }
            }

            if (combines.Count == 0) {
                Debug.LogWarning("[KCCMeshCombine] 没有可合并的网格（Layer / 来源模式 / 烘焙开关）。");
                return;
            }

            var combined = new Mesh {
                name = string.IsNullOrEmpty(_assetBaseName) ? "Combined" : _assetBaseName,
            };

            if (combines.Count > ushort.MaxValue) {
                combined.indexFormat = IndexFormat.UInt32;
            }

            combined.CombineMeshes(combines.ToArray(), true, true);
            combined.RecalculateBounds();

            EnsureProjectFolderExists(_meshAssetRelativeFolder);

            var safeBase = MakeSafeAssetFileName(string.IsNullOrEmpty(_assetBaseName) ? "Combined" : _assetBaseName);
            var assetPath =
                AssetDatabase.GenerateUniqueAssetPath($"{_meshAssetRelativeFolder.TrimEnd('/')}/{safeBase}.asset");

            AssetDatabase.CreateAsset(combined, assetPath);
            AssetDatabase.SaveAssets();

            var combinedGo = new GameObject($"{safeBase}_Collider");
            Undo.RegisterCreatedObjectUndo(combinedGo, "KCC Combine MeshCollider");

            var mfNew = combinedGo.AddComponent<MeshFilter>();
            var mcNew = combinedGo.AddComponent<MeshCollider>();
            mfNew.sharedMesh = combined;
            mcNew.sharedMesh = combined;
            mcNew.convex = _convexCollider;

            var mat = FindFirstPhysicMaterial(roots);
            if (mat != null) {
                mcNew.sharedMaterial = mat;
            }

            if (_addMeshRenderer) {
                var mr = combinedGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _fallbackMaterial != null
                    ? _fallbackMaterial
                    : FindFirstSharedMaterial(roots)
                      ?? AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            }

            combinedGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            combinedGo.transform.localScale = Vector3.one;

            if (_stripMeshCollidersFromSources && meshCollidersSeen.Count > 0) {
                foreach (var mc in meshCollidersSeen) {
                    if (mc == null) {
                        continue;
                    }

                    Undo.DestroyObjectImmediate(mc);
                }
            }

            if (_stripBakedPrimitives && primitivesToStrip.Count > 0) {
                foreach (var c in primitivesToStrip) {
                    if (c == null) {
                        continue;
                    }

                    Undo.DestroyObjectImmediate(c);
                }
            }

            foreach (var root in roots) {
                if (root == null) {
                    continue;
                }

                if (_destroyOriginalRoots) {
                    Undo.DestroyObjectImmediate(root);
                } else if (_disableOriginalRoots) {
                    Undo.RecordObject(root, "KCC Combine Disable Original");
                    root.SetActive(false);
                }
            }

            Selection.activeGameObject = combinedGo;
            EditorGUIUtility.PingObject(combinedGo);

            Debug.Log(
                $"[KCCMeshCombine] combineInstances={combines.Count} verts={combined.vertexCount} tris={combined.triangles.Length / 3} " +
                $"primitivesStripped={(_stripBakedPrimitives ? primitivesToStrip.Count : 0)} asset={assetPath}",
                combined);
        } finally {
            foreach (var tm in tempMeshes) {
                if (tm != null) {
                    DestroyImmediate(tm);
                }
            }
        }
    }

    static Mesh CopyTemporaryBuiltinMesh(PrimitiveType primitiveType) {
        var go = GameObject.CreatePrimitive(primitiveType);
        go.hideFlags = HideFlags.HideAndDontSave;
        var mf = go.GetComponent<MeshFilter>();
        var duplicate = Instantiate(mf.sharedMesh);
        duplicate.name = $"KCCCombineTemp_{primitiveType}";
        DestroyImmediate(go);
        return duplicate;
    }

    /// <summary>内置 Capsule 网格轴向为 Y；按 CapsuleCollider.direction 旋转并缩放至 height/radius。</summary>
    static Matrix4x4 CapsuleBakeLocalMatrix(CapsuleCollider cap, Bounds unitMeshBounds) {
        var h = Mathf.Max(cap.height, cap.radius * 2f + 1e-5f);
        var r = cap.radius;
        var e = unitMeshBounds.extents;
        var radialRef = Mathf.Max(Mathf.Max(e.x, e.z), 1e-6f);
        var sxz = r / radialRef;
        var sy = h / Mathf.Max(e.y * 2f, 1e-6f);
        var scale = new Vector3(sxz, sy, sxz);

        var dirRot = Quaternion.identity;
        switch (cap.direction) {
            case 0:
                dirRot = Quaternion.FromToRotation(Vector3.up, Vector3.right);
                break;
            case 2:
                dirRot = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                break;
        }

        var tr = Matrix4x4.TRS(cap.center, Quaternion.identity, Vector3.one);
        return tr * Matrix4x4.Rotate(dirRot) * Matrix4x4.Scale(scale);
    }

    static LayerMask DrawLayerMaskField(string label, LayerMask layerMask) {
        var layers = InternalEditorUtility.layers;
        var old = layerMask.value;
        var mask = 0;
        for (var i = 0; i < layers.Length; i++) {
            var layerIndex = LayerMask.NameToLayer(layers[i]);
            if (layerIndex < 0) {
                continue;
            }

            if ((old & (1 << layerIndex)) != 0) {
                mask |= 1 << i;
            }
        }

        mask = EditorGUILayout.MaskField(label, mask, layers);

        var newMask = 0;
        for (var i = 0; i < layers.Length; i++) {
            if ((mask & (1 << i)) == 0) {
                continue;
            }

            var layerIndex = LayerMask.NameToLayer(layers[i]);
            if (layerIndex >= 0) {
                newMask |= 1 << layerIndex;
            }
        }

        layerMask.value = newMask;
        return layerMask;
    }

    static Material FindFirstSharedMaterial(GameObject[] roots) {
        foreach (var root in roots) {
            if (root == null) {
                continue;
            }

            var mrs = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in mrs) {
                if (mr.sharedMaterial != null) {
                    return mr.sharedMaterial;
                }
            }
        }

        return null;
    }

    static PhysicMaterial FindFirstPhysicMaterial(GameObject[] roots) {
        foreach (var root in roots) {
            if (root == null) {
                continue;
            }

            var cols = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) {
                if (c != null && c.sharedMaterial != null) {
                    return c.sharedMaterial;
                }
            }
        }

        return null;
    }

    static string MakeSafeAssetFileName(string name) {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name) {
            var bad = false;
            foreach (var ic in invalid) {
                if (c == ic) {
                    bad = true;
                    break;
                }
            }

            sb.Append(bad ? '_' : c);
        }

        var s = sb.ToString().Trim();
        return s.Length == 0 ? "Combined" : s;
    }

    static void EnsureProjectFolderExists(string relativePath) {
        relativePath = relativePath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("Assets", StringComparison.Ordinal)) {
            throw new ArgumentException("保存路径必须以 Assets/ 开头。");
        }

        if (AssetDatabase.IsValidFolder(relativePath)) {
            return;
        }

        var parts = relativePath.Split('/');
        var cur = parts[0];
        for (var i = 1; i < parts.Length; i++) {
            var next = $"{cur}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) {
                AssetDatabase.CreateFolder(cur, parts[i]);
            }

            cur = next;
        }
    }
}
