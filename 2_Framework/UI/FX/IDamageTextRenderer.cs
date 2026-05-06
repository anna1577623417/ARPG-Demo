using System.Collections.Generic;

/// <summary>飘字渲染器统一接口。</summary>
public interface IDamageTextRenderer
{
    int ActiveCount { get; }

    void Spawn(in DamageTextData data);

    void Tick(float deltaTime);

    void Clear();

    void Export(List<DamageTextData> outList);

    void Import(List<DamageTextData> inList);
}
