using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TrayTemps;

public enum SpeedMode
{
    Fixed,
    Linear,
    Curve
}

public sealed class CurvePoint
{
    public double Temperature { get; set; } = 40;
    public double Percent { get; set; } = 40;
}

public sealed class SpeedProfileConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public SpeedMode Mode { get; set; } = SpeedMode.Linear;
    public string TemperatureSensorId { get; set; } = string.Empty;
    public double FixedPercent { get; set; } = 50;
    public double LowTemperature { get; set; } = 40;
    public double HighTemperature { get; set; } = 80;
    public double LowPercent { get; set; } = 25;
    public double HighPercent { get; set; } = 100;
    public List<CurvePoint> CurvePoints { get; set; } = [];

    public double Evaluate(float? temperature)
    {
        return Math.Clamp(EvaluateCore(temperature), 0, 100);
    }

    private double EvaluateCore(float? temperature)
    {
        return Mode switch
        {
            SpeedMode.Fixed => FixedPercent,
            SpeedMode.Linear => EvaluateLinear(temperature),
            SpeedMode.Curve => EvaluateCurve(temperature),
            _ => FixedPercent
        };
    }

    private double EvaluateLinear(float? temperature)
    {
        if (temperature is not float value)
        {
            return FixedPercent;
        }

        if (HighTemperature <= LowTemperature)
        {
            return HighPercent;
        }

        if (value <= LowTemperature)
        {
            return LowPercent;
        }

        if (value >= HighTemperature)
        {
            return HighPercent;
        }

        double ratio = (value - LowTemperature) /
                       (HighTemperature - LowTemperature);
        return LowPercent + ((HighPercent - LowPercent) * ratio);
    }

    private double EvaluateCurve(float? temperature)
    {
        return temperature is float value
            ? CurveMath.Evaluate(CurvePoints, value)
            : 0;
    }
}

internal sealed class FanControlConfig
{
    public string ControlId { get; set; } = string.Empty;
    public string CustomName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsHidden { get; set; }
    public string? ProfileId { get; set; }
}

internal sealed class AppConfig
{
    public int Version { get; set; } = 2;
    public string Language { get; set; } = "zh-CN";
    public string ThemeMode { get; set; } = "System";
    public bool FirstScanCompleted { get; set; }
    public List<FanControlConfig> Fans { get; set; } = [];
    public List<SpeedProfileConfig> Profiles { get; set; } = [];
}

public sealed class TemperatureChoice : INotifyPropertyChanged
{
    private string _displayName;
    private float _value;

    public TemperatureChoice(string id, string displayName, float value)
    {
        Id = id;
        _displayName = displayName;
        _value = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string DisplayName
    {
        get => _displayName;
        private set
        {
            if (_displayName == value)
            {
                return;
            }

            _displayName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public float Value
    {
        get => _value;
        private set
        {
            if (Math.Abs(_value - value) < 0.05f)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
        }
    }

    public string DisplayText => DisplayName;

    public void Update(string displayName, float value)
    {
        DisplayName = displayName;
        Value = value;
    }

    public override string ToString() => DisplayName;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

internal sealed record ProfileChoice(
    string Id,
    string Name);

internal sealed record FanCardState(
    string ControlId,
    string Name,
    string HardwareName,
    string HardwareType,
    string FanSensorName,
    bool IsEnabled,
    bool IsHidden,
    bool IsControllable,
    float? Rpm,
    float? Percent,
    string? ProfileId,
    string? Error);

internal sealed record ProfileCardState(
    string Id,
    string Name,
    SpeedMode Mode,
    string TemperatureSensorId,
    string TemperatureSensorName,
    string Summary,
    double FixedPercent,
    double LowTemperature,
    double HighTemperature,
    double LowPercent,
    double HighPercent,
    IReadOnlyList<CurvePoint> CurvePoints);

internal sealed record FanControlUiState(
    IReadOnlyList<FanCardState> Fans,
    IReadOnlyList<ProfileChoice> ProfileChoices,
    IReadOnlyList<ProfileCardState> Profiles,
    IReadOnlyList<TemperatureChoice> Temperatures,
    string Language,
    string ThemeMode,
    bool FirstScanCompleted,
    int DetectedFanCount,
    int DetectedTemperatureCount);

internal static class JsonOptions
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
