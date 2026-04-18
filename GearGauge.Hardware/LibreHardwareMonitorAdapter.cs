using LibreHardwareMonitor.Hardware;

namespace GearGauge.Hardware;

public sealed class LibreHardwareMonitorAdapter : IDisposable
{
    private Computer? _computer;
    private bool _isOpen;
    private bool _failed;

    public bool IsAvailable => _isOpen && !_failed;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsMotherboardEnabled = true,
                    IsStorageEnabled = false,
                    IsNetworkEnabled = false,
                    IsControllerEnabled = false
                };

                computer.Open();
                computer.Accept(new UpdateVisitor());
                _computer = computer;
                _isOpen = true;
            }
            catch (Exception)
            {
                _failed = true;
                _computer?.Close();
                _computer = null;
            }
        }, cancellationToken);
    }

    public void Update()
    {
        if (!_isOpen || _computer is null)
        {
            return;
        }

        try
        {
            _computer.Accept(new UpdateVisitor());
        }
        catch (Exception)
        {
            _failed = true;
        }
    }

    public IEnumerable<IHardware> GetAllHardware()
    {
        if (_computer is null)
        {
            yield break;
        }

        foreach (var hardware in _computer.Hardware)
        {
            foreach (var nested in EnumerateHardware(hardware))
            {
                yield return nested;
            }
        }
    }

    public IHardware? GetCpuHardware()
    {
        return GetAllHardware().FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
    }

    public IReadOnlyList<IHardware> GetGpuHardware()
    {
        return GetAllHardware()
            .Where(static h => h.HardwareType is HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia)
            .ToArray();
    }

    public IReadOnlyList<ISensor> GetSensors(IHardware hardware)
    {
        return EnumerateHardware(hardware)
            .SelectMany(static h => h.Sensors)
            .ToArray();
    }

    public void Dispose()
    {
        _computer?.Close();
    }

    private static IEnumerable<IHardware> EnumerateHardware(IHardware hardware)
    {
        yield return hardware;

        foreach (var subHardware in hardware.SubHardware)
        {
            foreach (var nested in EnumerateHardware(subHardware))
            {
                yield return nested;
            }
        }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
            {
                subHardware.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor)
        {
        }

        public void VisitParameter(IParameter parameter)
        {
        }
    }
}
