using System;

public interface IBuffStack
{
    BuffInstance Apply(BuffDefinitionSO def, object source);
    bool Remove(BuffInstance instance);
    void Tick(float deltaTime);
    int Count(BuffDefinitionSO def);
    bool Has(BuffDefinitionSO def);
    event Action<BuffInstance> OnPeriodElapsed;
    event Action<BuffInstance> OnExpired;
}
