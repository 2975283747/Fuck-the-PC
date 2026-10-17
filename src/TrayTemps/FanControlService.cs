using System.IO;
using System.Text.Json;

namespace TrayTemps;

internal sealed class FanControlService : IDisposable
{
    private readonly object _sync = new();
    private readonly HardwareMonitor _monitor;
    private readonly string _configPath;
    private readonly Dictionary<string, FanRuntime> _runtime =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _controlledIds =
        new(StringComparer.Ordinal);

    private AppConfig _config;
    private HardwareSnapshot _snapshot = HardwareSnapshot.Empty;
    private bool _disposed;

    public FanControlService(HardwareMonitor monitor)
    {
        _monitor = monitor;
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrayTemps",
            "config.json");
        _config = LoadConfig();
    }

    public string Language
    {
        get
        {
            lock (_sync)
            {
                return _config.Language;
            }
        }
    }

    public string ConfigPath => _configPath;

    public static string LastConfigError { get; private set; } = string.Empty;

    public bool HasActiveControls
    {
        get
        {
            lock (_sync)
            {
                return _config.Fans.Any(static fan => fan.IsEnabled);
            }
        }
    }

    public void Initialize(HardwareSnapshot snapshot)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _snapshot = snapshot;
            bool configChanged = false;
            bool firstScan = !_config.FirstScanCompleted;

            if (firstScan)
            {
                AddDefaultProfilesIfNeeded(snapshot);
            }

            int mergedCount = MergeDetectedFans(snapshot);
            configChanged |= mergedCount > 0;

            if (firstScan)
            {
                string? defaultProfileId = _config.Profiles.Count > 0
                    ? _config.Profiles[0].Id
                    : null;
                string? cpuProfileId = FindProfileForHardware(
                    snapshot,
                    static temperature => temperature.HardwareType is
                        LibreHardwareMonitor.Hardware.HardwareType.Cpu or
                        LibreHardwareMonitor.Hardware.HardwareType.Motherboard or
                        LibreHardwareMonitor.Hardware.HardwareType.SuperIO or
                        LibreHardwareMonitor.Hardware.HardwareType.EmbeddedController);
                string? gpuProfileId = FindProfileForHardware(
                    snapshot,
                    static temperature => temperature.HardwareType is
                        LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia or
                        LibreHardwareMonitor.Hardware.HardwareType.GpuAmd or
                        LibreHardwareMonitor.Hardware.HardwareType.GpuIntel);

                if (defaultProfileId is not null)
                {
                    foreach (FanControlConfig fan in _config.Fans)
                    {
                        if (!string.IsNullOrWhiteSpace(fan.ProfileId))
                        {
                            continue;
                        }

                        FanChannelInfo? channel = snapshot.Fans.FirstOrDefault(
                            channel => string.Equals(
                                channel.ControlId,
                                fan.ControlId,
                                StringComparison.Ordinal));

                        fan.ProfileId = channel?.HardwareType is
                            LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia or
                            LibreHardwareMonitor.Hardware.HardwareType.GpuAmd or
                            LibreHardwareMonitor.Hardware.HardwareType.GpuIntel
                            ? gpuProfileId ?? defaultProfileId
                            : cpuProfileId ?? defaultProfileId;
                    }
                }

                _config.FirstScanCompleted = true;
                configChanged = true;
            }

            if (configChanged)
            {
                SaveConfig();
            }
        }
    }

    public void Poll(HardwareSnapshot snapshot)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _snapshot = snapshot;

            foreach (FanControlConfig fanConfig in _config.Fans)
            {
                if (!fanConfig.IsEnabled)
                {
                    continue;
                }

                ApplyFan(fanConfig, snapshot);
            }
        }
    }

    public FanControlUiState GetUiState()
    {
        lock (_sync)
        {
            var profileChoices = _config.Profiles
                .OrderBy(static profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(static profile => new ProfileChoice(profile.Id, profile.Name))
                .ToList();

            var fans = _snapshot.Fans
                .Where(IsUsableChannel)
                .Select(channel =>
                {
                    FanControlConfig? stored = FindFanConfig(channel.ControlId);
                    FanControlConfig config = stored ?? new FanControlConfig
                    {
                        ControlId = channel.ControlId
                    };
                    _runtime.TryGetValue(channel.ControlId, out FanRuntime? runtime);

                    return new FanCardState(
                        channel.ControlId,
                        string.IsNullOrWhiteSpace(config.CustomName)
                            ? BuildFanDisplayName(channel)
                            : config.CustomName,
                        channel.HardwareName,
                        channel.HardwareType.ToString(),
                        channel.FanSensorName,
                        config.IsEnabled,
                        config.IsHidden,
                        true,
                        channel.Rpm,
                        runtime?.AppliedPercent ?? channel.Percent,
                        config.ProfileId,
                        runtime?.Error);
                })
                .OrderBy(static fan => fan.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var profiles = _config.Profiles
                .OrderBy(static profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(profile => ToProfileCardState(profile))
                .ToList();

            var temperatures = _snapshot.Temperatures
                .OrderBy(static temperature => TemperatureSortRank(temperature.HardwareType))
                .ThenBy(static temperature => temperature.DisplayName)
                .Select(static temperature => new TemperatureChoice(
                    temperature.Id,
                    $"{temperature.DisplayName}  |  {temperature.Value:0.#} \u00B0C",
                    temperature.Value))
                .ToList();

            return new FanControlUiState(
                fans,
                profileChoices,
                profiles,
                temperatures,
                _config.Language,
                _config.ThemeMode,
                _config.FirstScanCompleted,
                _snapshot.Fans.Count,
                _snapshot.Temperatures.Count);
        }
    }

    public void SetFanEnabled(string controlId, bool enabled)
    {
        lock (_sync)
        {
            FanControlConfig fan = GetOrCreateFanConfig(controlId);
            fan.IsEnabled = enabled;

            if (enabled && string.IsNullOrWhiteSpace(fan.ProfileId) &&
                _config.Profiles.Count > 0)
            {
                fan.ProfileId = _config.Profiles[0].Id;
            }

            if (!enabled)
            {
                _monitor.RestoreControl(controlId);
                _controlledIds.Remove(controlId);
                _runtime.Remove(controlId);
            }

            SaveConfig();
        }
    }

    public void SetFanProfile(string controlId, string? profileId)
    {
        lock (_sync)
        {
            FanControlConfig fan = GetOrCreateFanConfig(controlId);
            fan.ProfileId = profileId;
            SaveConfig();
        }
    }

    public void SetFanHidden(string controlId, bool hidden)
    {
        lock (_sync)
        {
            FanControlConfig fan = GetOrCreateFanConfig(controlId);
            fan.IsHidden = hidden;
            SaveConfig();
        }
    }

    public void SetFanName(string controlId, string name)
    {
        lock (_sync)
        {
            FanControlConfig fan = GetOrCreateFanConfig(controlId);
            fan.CustomName = name.Trim();
            SaveConfig();
        }
    }

    public void SetLanguage(string language)
    {
        lock (_sync)
        {
            _config.Language = language.Equals("en-US", StringComparison.OrdinalIgnoreCase)
                ? "en-US"
                : "zh-CN";
            SaveConfig();
        }
    }

    public void SetThemeMode(string themeMode)
    {
        lock (_sync)
        {
            _config.ThemeMode = themeMode switch
            {
                "Dark" => "Dark",
                "Light" => "Light",
                _ => "System"
            };
            SaveConfig();
        }
    }

    public SpeedProfileConfig CreateProfile(
        SpeedMode mode,
        string temperatureSensorId,
        string name)
    {
        lock (_sync)
        {
            var profile = new SpeedProfileConfig
            {
                Name = name,
                Mode = mode,
                TemperatureSensorId = temperatureSensorId
            };

            if (mode == SpeedMode.Curve)
            {
                profile.CurvePoints =
                [
                    new CurvePoint { Temperature = 40, Percent = 25 },
                    new CurvePoint { Temperature = 60, Percent = 50 },
                    new CurvePoint { Temperature = 80, Percent = 100 }
                ];
            }

            _config.Profiles.Add(profile);
            SaveConfig();
            return CloneProfile(profile);
        }
    }

    public void AddProfile(SpeedProfileConfig profile)
    {
        lock (_sync)
        {
            _config.Profiles.Add(CloneProfile(profile));
            SaveConfig();
        }
    }

    public void UpdateProfile(SpeedProfileConfig profile)
    {
        lock (_sync)
        {
            int index = _config.Profiles.FindIndex(item => item.Id == profile.Id);
            if (index < 0)
            {
                return;
            }

            _config.Profiles[index] = CloneProfile(profile);
            SaveConfig();
        }
    }

    public void DeleteProfile(string profileId)
    {
        lock (_sync)
        {
            _config.Profiles.RemoveAll(profile => profile.Id == profileId);

            foreach (FanControlConfig fan in _config.Fans)
            {
                if (!string.Equals(fan.ProfileId, profileId, StringComparison.Ordinal))
                {
                    continue;
                }

                fan.ProfileId = null;
                fan.IsEnabled = false;
                _monitor.RestoreControl(fan.ControlId);
                _controlledIds.Remove(fan.ControlId);
                _runtime.Remove(fan.ControlId);
            }

            SaveConfig();
        }
    }

    public SpeedProfileConfig? GetProfile(string profileId)
    {
        lock (_sync)
        {
            SpeedProfileConfig? profile = _config.Profiles
                .FirstOrDefault(item => item.Id == profileId);
            return profile is null ? null : CloneProfile(profile);
        }
    }

    public void RestoreAllDefaults()
    {
        lock (_sync)
        {
            foreach (string controlId in _controlledIds.ToList())
            {
                _monitor.RestoreControl(controlId);
            }

            _controlledIds.Clear();
            _runtime.Clear();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            RestoreAllDefaults();
            _disposed = true;
        }
    }

    private void ApplyFan(FanControlConfig fanConfig, HardwareSnapshot snapshot)
    {
        FanChannelInfo? channel = snapshot.Fans.FirstOrDefault(
            channel => string.Equals(
                channel.ControlId,
                fanConfig.ControlId,
                StringComparison.Ordinal));

        if (channel is null)
        {
            _monitor.RestoreControl(fanConfig.ControlId);
            SetRuntimeError(fanConfig.ControlId, "未检测到风扇控制通道");
            return;
        }

        if (!IsUsableChannel(channel))
        {
            _monitor.RestoreControl(fanConfig.ControlId);
            SetRuntimeError(fanConfig.ControlId, "风扇速度传感器不可用");
            return;
        }

        SpeedProfileConfig? profile = _config.Profiles.FirstOrDefault(
            profile => string.Equals(
                profile.Id,
                fanConfig.ProfileId,
                StringComparison.Ordinal));

        if (profile is null)
        {
            _monitor.RestoreControl(fanConfig.ControlId);
            SetRuntimeError(fanConfig.ControlId, "未分配调速卡片");
            return;
        }

        float? temperature = null;
        if (profile.Mode != SpeedMode.Fixed)
        {
            TemperatureSensorInfo? sensor = snapshot.Temperatures.FirstOrDefault(
                sensor => string.Equals(
                    sensor.Id,
                    profile.TemperatureSensorId,
                    StringComparison.Ordinal));

            if (sensor is null)
            {
                _monitor.RestoreControl(fanConfig.ControlId);
                SetRuntimeError(fanConfig.ControlId, "温度传感器不可用");
                return;
            }

            temperature = sensor.Value;
        }

        float percent = (float)profile.Evaluate(temperature);
        bool applied = _monitor.SetSoftwareControl(fanConfig.ControlId, percent);

        if (!applied)
        {
            SetRuntimeError(fanConfig.ControlId, "控制通道写入失败");
            return;
        }

        _controlledIds.Add(fanConfig.ControlId);
        _runtime[fanConfig.ControlId] = new FanRuntime
        {
            AppliedPercent = percent,
            Temperature = temperature,
            Error = null
        };
    }

    private void SetRuntimeError(string controlId, string error)
    {
        if (!_runtime.TryGetValue(controlId, out FanRuntime? runtime))
        {
            runtime = new FanRuntime();
            _runtime[controlId] = runtime;
        }

        runtime.Error = error;
    }

    private FanControlConfig GetOrCreateFanConfig(string controlId)
    {
        FanControlConfig? config = FindFanConfig(controlId);

        if (config is not null)
        {
            return config;
        }

        config = new FanControlConfig
        {
            ControlId = controlId
        };
        _config.Fans.Add(config);
        return config;
    }

    private FanControlConfig? FindFanConfig(string controlId)
    {
        FanControlConfig? config = _config.Fans.FirstOrDefault(
            item => string.Equals(
                item.ControlId,
                controlId,
                StringComparison.Ordinal));
        return config;
    }

    private int MergeDetectedFans(HardwareSnapshot snapshot)
    {
        int added = 0;

        foreach (FanChannelInfo channel in snapshot.Fans)
        {
            if (!IsUsableChannel(channel))
            {
                continue;
            }

            FanControlConfig? existing = _config.Fans.FirstOrDefault(
                fan => string.Equals(
                    fan.ControlId,
                    channel.ControlId,
                    StringComparison.Ordinal));

            if (existing is not null)
            {
                continue;
            }

            _config.Fans.Add(new FanControlConfig
            {
                ControlId = channel.ControlId
            });
            added++;
        }

        return added;
    }

    private static bool IsUsableChannel(FanChannelInfo channel)
    {
        return !string.IsNullOrWhiteSpace(channel.FanSensorId) &&
               channel.Rpm is float rpm &&
               float.IsFinite(rpm) &&
               rpm >= 0;
    }

    private string? FindProfileForHardware(
        HardwareSnapshot snapshot,
        Func<TemperatureSensorInfo, bool> predicate)
    {
        HashSet<string> temperatureIds = snapshot.Temperatures
            .Where(predicate)
            .Select(static temperature => temperature.Id)
            .ToHashSet(StringComparer.Ordinal);

        return _config.Profiles
            .FirstOrDefault(profile => temperatureIds.Contains(profile.TemperatureSensorId))
            ?.Id;
    }

    private void AddDefaultProfilesIfNeeded(HardwareSnapshot snapshot)
    {
        bool english = _config.Language.Equals(
            "en-US",
            StringComparison.OrdinalIgnoreCase);

        TemperatureSensorInfo? cpu = HardwareMonitor.SelectCpuTemperature(
            snapshot.Temperatures);
        TemperatureSensorInfo? gpu = HardwareMonitor.SelectGpuTemperature(
            snapshot.Temperatures);

        if (cpu is not null &&
            !_config.Profiles.Any(profile => profile.Mode == SpeedMode.Linear &&
                                             profile.TemperatureSensorId == cpu.Id))
        {
            _config.Profiles.Add(new SpeedProfileConfig
            {
                Name = english ? "CPU Balanced" : "CPU 平衡",
                Mode = SpeedMode.Linear,
                TemperatureSensorId = cpu.Id,
                LowTemperature = 40,
                HighTemperature = 80,
                LowPercent = 25,
                HighPercent = 100
            });
        }

        if (gpu is not null &&
            !_config.Profiles.Any(profile => profile.Mode == SpeedMode.Linear &&
                                             profile.TemperatureSensorId == gpu.Id))
        {
            _config.Profiles.Add(new SpeedProfileConfig
            {
                Name = english ? "GPU Balanced" : "GPU 平衡",
                Mode = SpeedMode.Linear,
                TemperatureSensorId = gpu.Id,
                LowTemperature = 45,
                HighTemperature = 83,
                LowPercent = 25,
                HighPercent = 100
            });
        }

        if (_config.Profiles.Count == 0)
        {
            _config.Profiles.Add(new SpeedProfileConfig
            {
                Name = english ? "Fixed 50%" : "固定 50%",
                Mode = SpeedMode.Fixed,
                FixedPercent = 50
            });
        }
    }

    private ProfileCardState ToProfileCardState(SpeedProfileConfig profile)
    {
        TemperatureSensorInfo? temperature = _snapshot.Temperatures.FirstOrDefault(
            sensor => string.Equals(
                sensor.Id,
                profile.TemperatureSensorId,
                StringComparison.Ordinal));

        return new ProfileCardState(
            profile.Id,
            profile.Name,
            profile.Mode,
            profile.TemperatureSensorId,
            temperature?.DisplayName ?? string.Empty,
            BuildProfileSummary(profile),
            profile.FixedPercent,
            profile.LowTemperature,
            profile.HighTemperature,
            profile.LowPercent,
            profile.HighPercent,
            profile.CurvePoints
                .OrderBy(static point => point.Temperature)
                .Select(static point => new CurvePoint
                {
                    Temperature = point.Temperature,
                    Percent = point.Percent
                })
                .ToList());
    }

    private static string BuildFanDisplayName(FanChannelInfo channel)
    {
        if (!string.IsNullOrWhiteSpace(channel.FanSensorName))
        {
            return channel.FanSensorName;
        }

        return channel.ControlName;
    }

    private static string BuildProfileSummary(SpeedProfileConfig profile)
    {
        return profile.Mode switch
        {
            SpeedMode.Fixed => $"{profile.FixedPercent:0}%",
            SpeedMode.Linear =>
                $"{profile.LowTemperature:0}\u00B0C \u2192 {profile.LowPercent:0}%  |  " +
                $"{profile.HighTemperature:0}\u00B0C \u2192 {profile.HighPercent:0}%",
            SpeedMode.Curve => $"{profile.CurvePoints.Count} points",
            _ => string.Empty
        };
    }

    private static int TemperatureSortRank(LibreHardwareMonitor.Hardware.HardwareType type)
    {
        return type switch
        {
            LibreHardwareMonitor.Hardware.HardwareType.Cpu => 0,
            LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia => 1,
            LibreHardwareMonitor.Hardware.HardwareType.GpuAmd => 1,
            LibreHardwareMonitor.Hardware.HardwareType.GpuIntel => 1,
            LibreHardwareMonitor.Hardware.HardwareType.Motherboard => 2,
            LibreHardwareMonitor.Hardware.HardwareType.SuperIO => 3,
            LibreHardwareMonitor.Hardware.HardwareType.EmbeddedController => 4,
            _ => 10
        };
    }

    private static SpeedProfileConfig CloneProfile(SpeedProfileConfig profile)
    {
        return new SpeedProfileConfig
        {
            Id = profile.Id,
            Name = profile.Name,
            Mode = profile.Mode,
            TemperatureSensorId = profile.TemperatureSensorId,
            FixedPercent = profile.FixedPercent,
            LowTemperature = profile.LowTemperature,
            HighTemperature = profile.HighTemperature,
            LowPercent = profile.LowPercent,
            HighPercent = profile.HighPercent,
            CurvePoints = profile.CurvePoints
                .OrderBy(static point => point.Temperature)
                .Select(static point => new CurvePoint
                {
                    Temperature = point.Temperature,
                    Percent = point.Percent
                })
                .ToList()
        };
    }

    private AppConfig LoadConfig()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return new AppConfig();
            }

            string json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Options)
                   ?? new AppConfig();
        }
        catch
        {
            LastConfigError = "配置读取失败。";
            return new AppConfig();
        }
    }

    private void SaveConfig()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(_config, JsonOptions.Options);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            LastConfigError = ex.Message;
        }
    }

    private sealed class FanRuntime
    {
        public float? AppliedPercent { get; set; }
        public float? Temperature { get; set; }
        public string? Error { get; set; }
    }
}
