using System.Collections.Generic;

public enum DamageTextMode
{
    CPU = 0,
    GPU = 1
}

/// <summary>双路径切换控制（当前已接 CPU，GPU 预留）。</summary>
public sealed class DamageTextModeController
{
    readonly IDamageTextRenderer _cpu;
    readonly IDamageTextRenderer _gpu;
    IDamageTextRenderer _current;
    DamageTextMode _mode;

    readonly int _switchUpThreshold;
    readonly int _switchDownThreshold;

    public DamageTextModeController(
        IDamageTextRenderer cpu,
        IDamageTextRenderer gpu,
        int switchUpThreshold,
        int switchDownThreshold)
    {
        _cpu = cpu;
        _gpu = gpu;
        _switchUpThreshold = switchUpThreshold;
        _switchDownThreshold = switchDownThreshold;
        _mode = DamageTextMode.CPU;
        _current = _cpu;
    }

    public IDamageTextRenderer Current => _current;
    public DamageTextMode CurrentMode => _mode;

    public void CheckSwitch()
    {
        if (_gpu == null)
        {
            return;
        }

        var count = _current.ActiveCount;
        if (_mode == DamageTextMode.CPU && count > _switchUpThreshold)
        {
            MigrateAndSwitch(DamageTextMode.GPU);
        }
        else if (_mode == DamageTextMode.GPU && count < _switchDownThreshold)
        {
            MigrateAndSwitch(DamageTextMode.CPU);
        }
    }

    void MigrateAndSwitch(DamageTextMode target)
    {
        if (_current == null)
        {
            return;
        }

        var snapshot = new List<DamageTextData>();
        _current.Export(snapshot);
        _current.Clear();

        _mode = target;
        _current = target == DamageTextMode.GPU ? _gpu : _cpu;
        _current?.Import(snapshot);
    }
}
