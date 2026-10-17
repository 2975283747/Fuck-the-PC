using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace TrayTemps;

internal sealed record TemperatureReading(
    double? CpuCelsius,
    double? GpuCelsius,
    string CpuSource,
    string GpuSource,
    string? Error);

internal sealed record TemperatureSensorInfo(
    string Id,
    HardwareType HardwareType,
    string HardwareName,
    string Name,
    float Value)
{
    public string DisplayName => $"{HardwareName} / {Name}";
}

internal sealed record FanChannelInfo(
    string ControlId,
    HardwareType HardwareType,
    string HardwareName,
    string ControlName,
    string FanSensorId,
    string FanSensorName,
    float? Rpm,
    float? Percent,
    string ControlMode)
{
    public string DisplayName => $"{HardwareName} / {ControlName}";
}

internal sealed record HardwareSnapshot(
    IReadOnlyList<TemperatureSensorInfo> Temperatures,
    IReadOnlyList<FanChannelInfo> Fans,
    string? Error)
{
    public static HardwareSnapshot Empty { get; } = new([], [], null);
}

internal sealed class HardwareMonitor : IDisposable
{
    private readonly object _sync = new();
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private readonly Dictionary<string, IControl> _controls = new(StringComparer.Ordinal);

    private bool _opened;
    private bool _disposed;

    public HardwareMonitor()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsMemoryEnabled = false,
            IsStorageEnabled = false,
            IsNetworkEnabled = false,
            IsControllerEnabled = true
        };
    }

    public HardwareSnapshot ReadSnapshot()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return HardwareSnapshot.Empty;
            }

            try
            {
                if (!_opened)
                {
                    _computer.Open();
                    _opened = true;
                }

                _computer.Accept(_visitor);

                var temperatures = new List<TemperatureSensorInfo>();
                var fans = new List<FanChannelInfo>();
                _controls.Clear();

                foreach (IHardware hardware in _computer.Hardware)
                {
                    CollectHardware(hardware, temperatures, fans);
                }

                return new HardwareSnapshot(temperatures, fans, null);
            }
            catch (Exception ex)
            {
                return new HardwareSnapshot([], [], ex.Message);
            }
        }
    }

    public bool SetSoftwareControl(string controlId, float percent)
    {
        lock (_sync)
        {
            if (_disposed || !_controls.TryGetValue(controlId, out IControl? control))
            {
                return false;
            }

            try
            {
                control.SetSoftware(Math.Clamp(percent, 0f, 100f));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool RestoreControl(string controlId)
    {
        lock (_sync)
        {
            if (_disposed || !_controls.TryGetValue(controlId, out IControl? control))
            {
                return false;
            }

            try
            {
                control.SetDefault();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static TemperatureReading CreateTemperatureReading(HardwareSnapshot snapshot)
    {
        TemperatureSensorInfo? cpu = SelectCpuTemperature(snapshot.Temperatures);
        TemperatureSensorInfo? gpu = SelectGpuTemperature(snapshot.Temperatures);

        string? error = snapshot.Error;
        if (error is null && cpu is null && gpu is null)
        {
            error = "未读取到 CPU/GPU 温度传感器";
        }

        return new TemperatureReading(
            cpu?.Value,
            gpu?.Value,
            cpu?.DisplayName ?? string.Empty,
            gpu?.DisplayName ?? string.Empty,
            error);
    }

    public static TemperatureSensorInfo? SelectCpuTemperature(
        IEnumerable<TemperatureSensorInfo> values)
    {
        TemperatureSensorInfo? cpuSensor = values
            .Where(static value => value.HardwareType == HardwareType.Cpu)
            .OrderByDescending(static value => CpuSensorPriority(value.Name))
            .ThenByDescending(static value => value.Value)
            .FirstOrDefault();

        if (cpuSensor is not null)
        {
            return cpuSensor;
        }

        return values
            .Where(static value => value.HardwareType is HardwareType.Motherboard
                or HardwareType.SuperIO
                or HardwareType.EmbeddedController)
            .Select(static value => new
            {
                Value = value,
                Priority = MotherboardCpuSensorPriority(value.Name)
            })
            .Where(static item => item.Priority > 0)
            .OrderByDescending(static item => item.Priority)
            .ThenByDescending(static item => item.Value.Value)
            .Select(static item => item.Value)
            .FirstOrDefault();
    }

    public static TemperatureSensorInfo? SelectGpuTemperature(
        IEnumerable<TemperatureSensorInfo> values)
    {
        return values
            .Where(static value => IsGpu(value.HardwareType))
            .GroupBy(static value => (value.HardwareType, value.HardwareName))
            .Select(static group => new
            {
                Best = group
                    .OrderByDescending(value => GpuSensorPriority(value.Name))
                    .ThenByDescending(static value => value.Value)
                    .First(),
                DevicePriority = GpuDevicePriority(group.Key.HardwareType)
            })
            .OrderByDescending(static item => item.DevicePriority)
            .ThenByDescending(static item => GpuSensorPriority(item.Best.Name))
            .ThenByDescending(static item => item.Best.Value)
            .Select(static item => item.Best)
            .FirstOrDefault();
    }

    private void CollectHardware(
        IHardware hardware,
        List<TemperatureSensorInfo> temperatures,
        List<FanChannelInfo> fans)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature ||
                sensor.Value is not float value ||
                !float.IsFinite(value) ||
                value <= -20f ||
                value >= 150f)
            {
                continue;
            }

            temperatures.Add(new TemperatureSensorInfo(
                sensor.Identifier.ToString(),
                hardware.HardwareType,
                hardware.Name,
                sensor.Name,
                value));
        }

        var fanSensors = hardware.Sensors
            .Where(static sensor => sensor.SensorType == SensorType.Fan)
            .ToList();

        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Control ||
                sensor.Control is not IControl control)
            {
                continue;
            }

            ISensor controlSensor = control.Sensor;
            string controlId = controlSensor.Identifier.ToString();
            _controls[controlId] = control;

            ISensor? fanSensor = fanSensors.FirstOrDefault(
                sensor => sensor.Index == controlSensor.Index);

            fans.Add(new FanChannelInfo(
                controlId,
                hardware.HardwareType,
                hardware.Name,
                controlSensor.Name,
                fanSensor?.Identifier.ToString() ?? string.Empty,
                fanSensor?.Name ?? string.Empty,
                fanSensor?.Value,
                controlSensor.Value,
                ReadControlMode(control)));
        }

        foreach (IHardware subHardware in hardware.SubHardware)
        {
            CollectHardware(subHardware, temperatures, fans);
        }
    }

    private static string ReadControlMode(IControl control)
    {
        try
        {
            return control.ControlMode.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static int CpuSensorPriority(string sensorName)
    {
        string name = sensorName.ToLowerInvariant();

        if (name.Contains("cpu package", StringComparison.Ordinal))
        {
            return 100;
        }

        if (name.Contains("tctl/tdie", StringComparison.Ordinal))
        {
            return 98;
        }

        if (name.Contains("tctl", StringComparison.Ordinal) ||
            name.Contains("tdie", StringComparison.Ordinal))
        {
            return 96;
        }

        if (name.Contains("core max", StringComparison.Ordinal))
        {
            return 94;
        }

        if (name.Contains("core average", StringComparison.Ordinal))
        {
            return 88;
        }

        return name.Contains("package", StringComparison.Ordinal) ? 80 : 10;
    }

    private static int MotherboardCpuSensorPriority(string sensorName)
    {
        string name = sensorName.ToLowerInvariant();

        if (name.Contains("gpu", StringComparison.Ordinal) ||
            name.Contains("system", StringComparison.Ordinal) ||
            name.Contains("chipset", StringComparison.Ordinal) ||
            name.Contains("vrm", StringComparison.Ordinal) ||
            name.Contains("mos", StringComparison.Ordinal) ||
            name.Contains("ambient", StringComparison.Ordinal) ||
            name.Contains("memory", StringComparison.Ordinal))
        {
            return 0;
        }

        if (name.Contains("cpu package", StringComparison.Ordinal))
        {
            return 100;
        }

        if (name.Contains("cpu core", StringComparison.Ordinal))
        {
            return 95;
        }

        if (name.Contains("tctl", StringComparison.Ordinal) ||
            name.Contains("tdie", StringComparison.Ordinal))
        {
            return 92;
        }

        if (name.Contains("cpu", StringComparison.Ordinal) &&
            (name.Contains("temp", StringComparison.Ordinal) ||
             name.Contains("socket", StringComparison.Ordinal)))
        {
            return 90;
        }

        return name.Contains("cpu", StringComparison.Ordinal) ? 70 : 0;
    }

    private static int GpuSensorPriority(string sensorName)
    {
        string name = sensorName.ToLowerInvariant();

        if (name.Contains("gpu core", StringComparison.Ordinal) ||
            name == "core" ||
            name.Contains("core temperature", StringComparison.Ordinal))
        {
            return 100;
        }

        if (name.Contains("edge", StringComparison.Ordinal))
        {
            return 95;
        }

        if (name.Contains("hot spot", StringComparison.Ordinal) ||
            name.Contains("hotspot", StringComparison.Ordinal))
        {
            return 70;
        }

        if (name.Contains("memory", StringComparison.Ordinal) ||
            name.Contains("vram", StringComparison.Ordinal) ||
            name.Contains("hbm", StringComparison.Ordinal))
        {
            return 20;
        }

        return 50;
    }

    private static bool IsGpu(HardwareType hardwareType)
    {
        return hardwareType is HardwareType.GpuNvidia
            or HardwareType.GpuAmd
            or HardwareType.GpuIntel;
    }

    private static int GpuDevicePriority(HardwareType hardwareType)
    {
        return hardwareType switch
        {
            HardwareType.GpuNvidia => 300,
            HardwareType.GpuAmd => 280,
            HardwareType.GpuIntel => 100,
            _ => 0
        };
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_opened)
            {
                try
                {
                    _computer.Close();
                }
                catch
                {
                    // Closing must not block application shutdown.
                }

                _opened = false;
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

            foreach (IHardware subHardware in hardware.SubHardware)
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
