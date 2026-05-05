using System;

public interface IResourcePool
{
    float GetCurrent(ResourceType type);
    float GetMax(ResourceType type);
    float GetNormalized(ResourceType type);
    bool Drain(ResourceType type, float amount, out float actualDrained);
    bool Refill(ResourceType type, float amount, out float actualRefilled);
    void SetCurrent(ResourceType type, float value);
    bool IsEmpty(ResourceType type);
    event Action<ResourceType, float, float> OnCurrentChanged;
}
