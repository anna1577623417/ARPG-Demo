using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>GPU Instancing 飘字：将数字拆成单个 glyph 实例并 DrawMeshInstancedProcedural。</summary>
public sealed class GPUInstancedDamageTextRenderer : IDamageTextRenderer
{
    [StructLayout(LayoutKind.Sequential)]
    struct Glyph
    {
        public Vector3 pos;
        public float size;
        public Vector4 uv;   // x,y,w,h
        public Color color;
        public float startTime;
        public float isCrit;
        public float pad0;
        public float pad1;
    }

    readonly List<DamageTextData> _active = new();
    readonly List<Glyph> _glyphs = new();
    readonly int _maxInstances;
    readonly float _defaultLifeSeconds;
    readonly DamageTextSettingsSO _settings;
    readonly Material _material;

    readonly Mesh _quadMesh;
    ComputeBuffer _glyphBuffer;

    static readonly int GlyphsId = Shader.PropertyToID("_Glyphs");
    static readonly int SpaceModeId = Shader.PropertyToID("_SpaceMode");
    static readonly int NowTimeId = Shader.PropertyToID("_NowTime");
    static readonly int ScreenParamsId = Shader.PropertyToID("_ScreenParamsCustom");
    static readonly int AtlasId = Shader.PropertyToID("_DigitsAtlas");
    static readonly int RiseSpeedId = Shader.PropertyToID("_RiseSpeed");
    static readonly int LifeId = Shader.PropertyToID("_LifeSeconds");
    static readonly int CritSizeMulId = Shader.PropertyToID("_CritSizeMultiplier");
    static readonly int CritRiseMulId = Shader.PropertyToID("_CritRiseMultiplier");
    static readonly int CritTintId = Shader.PropertyToID("_CritTint");

    bool _warnedAtlasCapacity;

    public GPUInstancedDamageTextRenderer(Material material, DamageTextSettingsSO settings, float defaultLifeSeconds)
    {
        _material = material;
        _settings = settings;
        _maxInstances = Mathf.Max(1, settings != null ? settings.gpuMaxInstances : 1024);
        _defaultLifeSeconds = Mathf.Max(0.05f, defaultLifeSeconds);
        _quadMesh = BuildQuadMesh();
    }

    public int ActiveCount => _active.Count;

    public void Spawn(in DamageTextData data)
    {
        if (_active.Count >= _maxInstances)
        {
            // 最老的先丢弃（占位策略）
            _active.RemoveAt(0);
        }

        var d = data;
        if (d.StartTime <= 0f)
        {
            d.StartTime = Time.time;
        }

        _active.Add(d);
    }

    public void Tick(float deltaTime)
    {
        var now = Time.time;
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var d = _active[i];
            if (now - d.StartTime > _defaultLifeSeconds)
            {
                _active.RemoveAt(i);
            }
        }

        if (_material == null || _settings == null || _settings.digitsAtlas == null)
        {
            return;
        }

        RebuildGlyphs();
        UploadAndDraw(now);
    }

    public void Clear()
    {
        _active.Clear();
        _glyphs.Clear();
    }

    public void Release()
    {
        _glyphBuffer?.Release();
        _glyphBuffer = null;
    }

    public void Export(List<DamageTextData> outList)
    {
        if (outList == null)
        {
            return;
        }

        outList.AddRange(_active);
    }

    public void Import(List<DamageTextData> inList)
    {
        if (inList == null)
        {
            return;
        }

        for (var i = 0; i < inList.Count; i++)
        {
            Spawn(inList[i]);
        }
    }

    void RebuildGlyphs()
    {
        _glyphs.Clear();
        if (_active.Count == 0)
        {
            return;
        }

        var cols = Mathf.Max(1, _settings.atlasColumns);
        var rows = Mathf.Max(1, _settings.atlasRows);
        var atlasCapacity = cols * rows;
        if (atlasCapacity < 10 && !_warnedAtlasCapacity)
        {
            _warnedAtlasCapacity = true;
            Debug.LogWarning($"[GPUInstancedDamageTextRenderer] Digits atlas capacity ({atlasCapacity}) is < 10. UV will clamp.", _settings);
        }

        var uvW = 1f / cols;
        var uvH = 1f / rows;

        var size = Mathf.Max(0.01f, _settings.glyphSize);
        var spacing = Mathf.Max(0f, _settings.glyphSpacing);

        for (var i = 0; i < _active.Count; i++)
        {
            var d = _active[i];
            var value = Mathf.Abs(d.Value);
            var digits = ToDigits(value);

            // 居中排版：[-n/2..+n/2]
            var count = digits.Count;
            var half = (count - 1) * 0.5f;
            for (var k = 0; k < count; k++)
            {
                var digit = digits[k];
                if (atlasCapacity > 0)
                {
                    digit = Mathf.Clamp(digit, 0, atlasCapacity - 1);
                }

                var col = digit % cols;
                var row = digit / cols;
                row = Mathf.Clamp(row, 0, rows - 1);

                var uv = new Vector4(col * uvW, 1f - (row + 1) * uvH, uvW, uvH);
                var offsetX = (k - half) * spacing;
                var g = new Glyph
                {
                    pos = d.WorldPos + new Vector3(offsetX, 0f, 0f),
                    size = size,
                    uv = uv,
                    color = d.Color,
                    startTime = d.StartTime,
                    isCrit = d.IsCrit ? 1f : 0f,
                    pad0 = 0f,
                    pad1 = 0f,
                };
                _glyphs.Add(g);
                if (_glyphs.Count >= _maxInstances)
                {
                    return;
                }
            }
        }
    }

    void UploadAndDraw(float now)
    {
        if (_glyphs.Count == 0)
        {
            return;
        }

        EnsureBufferCapacity(_glyphs.Count);
        _glyphBuffer.SetData(_glyphs);

        _material.SetBuffer(GlyphsId, _glyphBuffer);
        _material.SetTexture(AtlasId, _settings.digitsAtlas);
        _material.SetFloat(NowTimeId, now);
        _material.SetFloat(LifeId, _defaultLifeSeconds);
        _material.SetFloat(RiseSpeedId, 40f);
        _material.SetInt(SpaceModeId, (int)_settings.spaceMode);
        _material.SetVector(ScreenParamsId, new Vector4(Screen.width, Screen.height, 0f, 0f));
        _material.SetFloat(CritSizeMulId, Mathf.Max(0.1f, _settings.critSizeMultiplier));
        _material.SetFloat(CritRiseMulId, Mathf.Max(0f, _settings.critRiseMultiplier));
        _material.SetColor(CritTintId, _settings.critTint);

        var bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        Graphics.DrawMeshInstancedProcedural(_quadMesh, 0, _material, bounds, _glyphs.Count);
    }

    void EnsureBufferCapacity(int count)
    {
        if (_glyphBuffer != null && _glyphBuffer.count >= count)
        {
            return;
        }

        _glyphBuffer?.Release();
        _glyphBuffer = new ComputeBuffer(Mathf.Max(count, 64), Marshal.SizeOf<Glyph>());
    }

    static List<int> ToDigits(int value)
    {
        var list = new List<int>(8);
        if (value == 0)
        {
            list.Add(0);
            return list;
        }

        while (value > 0)
        {
            list.Add(value % 10);
            value /= 10;
        }

        list.Reverse();
        return list;
    }

    static Mesh BuildQuadMesh()
    {
        var m = new Mesh { name = "DamageText_Quad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };
        m.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        return m;
    }
}

