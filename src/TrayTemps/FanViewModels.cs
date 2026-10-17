using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TrayTemps;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ProfileChoiceViewModel : ObservableObject
{
    public ProfileChoiceViewModel(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string DisplayText => Name;

    public override string ToString() => Name;
}

internal sealed class FanCardViewModel : ObservableObject
{
    public FanCardViewModel(string controlId)
    {
        ControlId = controlId;
    }

    public string ControlId { get; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _hardwareName = string.Empty;
    public string HardwareName
    {
        get => _hardwareName;
        set => SetProperty(ref _hardwareName, value);
    }

    private string _fanSensorName = string.Empty;
    public string FanSensorName
    {
        get => _fanSensorName;
        set => SetProperty(ref _fanSensorName, value);
    }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    private bool _isHidden;
    public bool IsHidden
    {
        get => _isHidden;
        set => SetProperty(ref _isHidden, value);
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    private string _rpmText = "-- RPM";
    public string RpmText
    {
        get => _rpmText;
        set => SetProperty(ref _rpmText, value);
    }

    private string _percentText = "-- %";
    public string PercentText
    {
        get => _percentText;
        set => SetProperty(ref _percentText, value);
    }

    private double _percentValue;
    public double PercentValue
    {
        get => _percentValue;
        set => SetProperty(ref _percentValue, value);
    }

    private string? _selectedProfileId;
    public string? SelectedProfileId
    {
        get => _selectedProfileId;
        set => SetProperty(ref _selectedProfileId, value);
    }

    private string _error = string.Empty;
    public string Error
    {
        get => _error;
        set => SetProperty(ref _error, value);
    }

    private string _hideMenuText = "隐藏";
    public string HideMenuText
    {
        get => _hideMenuText;
        set => SetProperty(ref _hideMenuText, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private double _fanOpacity = 0.55;
    public double FanOpacity
    {
        get => _fanOpacity;
        set => SetProperty(ref _fanOpacity, value);
    }

    private IReadOnlyList<ProfileChoiceViewModel> _profileChoices = [];
    public IReadOnlyList<ProfileChoiceViewModel> ProfileChoices
    {
        get => _profileChoices;
        set => SetProperty(ref _profileChoices, value);
    }
}

public sealed class ProfileCardViewModel : ObservableObject
{
    public ProfileCardViewModel(string id)
    {
        Id = id;
    }

    public string Id { get; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _modeText = string.Empty;
    public string ModeText
    {
        get => _modeText;
        set => SetProperty(ref _modeText, value);
    }

    private string _sensorName = string.Empty;
    public string SensorName
    {
        get => _sensorName;
        set => SetProperty(ref _sensorName, value);
    }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    private string _editText = "编辑";
    public string EditText
    {
        get => _editText;
        set => SetProperty(ref _editText, value);
    }

    private string _deleteText = "删除";
    public string DeleteText
    {
        get => _deleteText;
        set => SetProperty(ref _deleteText, value);
    }
}

internal static class Strings
{
    private static readonly Dictionary<string, (string Zh, string En)> Values = new()
    {
        ["AppTitle"] = ("Fuck the PC", "Fuck the PC"),
        ["Fans"] = ("风扇", "Fans"),
        ["Profiles"] = ("调速卡片", "Speed Cards"),
        ["About"] = ("关于", "About"),
        ["ThemeSystem"] = ("跟随系统", "System theme"),
        ["ThemeLight"] = ("浅色模式", "Light mode"),
        ["ThemeDark"] = ("深色模式", "Dark mode"),
        ["Skin"] = ("皮肤", "Skin"),
        ["FansTitle"] = ("风扇控制", "Fan Control"),
        ["FansSubtitle"] = ("每个卡片对应一个可控制的硬件风扇通道", "One card per controllable hardware fan channel"),
        ["ProfilesTitle"] = ("调速卡片", "Speed Cards"),
        ["ProfilesSubtitle"] = ("固定速度、线性调速和多点曲线，可重复分配给任意风扇", "Fixed, linear and multi-point curves, reusable across any fan"),
        ["AddFixed"] = ("固定速度", "Fixed"),
        ["AddLinear"] = ("线性调速", "Linear"),
        ["AddCurve"] = ("曲线调速", "Curve"),
        ["ShowHidden"] = ("显示隐藏项", "Show hidden"),
        ["Language"] = ("语言", "Language"),
        ["Hide"] = ("隐藏", "Hide"),
        ["Unhide"] = ("取消隐藏", "Unhide"),
        ["Rename"] = ("重命名", "Rename"),
        ["RenameFan"] = ("输入新的风扇卡片名称", "Enter a new fan card name"),
        ["Ok"] = ("确定", "OK"),
        ["Edit"] = ("编辑", "Edit"),
        ["Delete"] = ("删除", "Delete"),
        ["Save"] = ("保存", "Save"),
        ["Cancel"] = ("取消", "Cancel"),
        ["Close"] = ("关闭", "Close"),
        ["Minimize"] = ("最小化", "Minimize"),
        ["Maximize"] = ("最大化", "Maximize"),
        ["Restore"] = ("还原", "Restore"),
        ["Off"] = ("关闭", "Off"),
        ["On"] = ("开启", "On"),
        ["NoFans"] = ("未检测到可控制的风扇通道", "No controllable fan channels detected"),
        ["NoProfiles"] = ("还没有调速卡片", "No speed cards yet"),
        ["Detected"] = ("已检测", "Detected"),
        ["FansDetected"] = ("个风扇通道", "fan channels"),
        ["TempsDetected"] = ("个温度传感器", "temperature sensors"),
        ["Selected"] = ("已分配", "Assigned"),
        ["NoProfile"] = ("未分配调速卡片", "No speed card assigned"),
        ["SafetyNote"] = ("软件控制会覆盖主板默认风扇策略；退出软件时恢复默认控制。", "Software control overrides the motherboard fan policy; defaults are restored on exit."),
        ["Fixed"] = ("固定", "Fixed"),
        ["Linear"] = ("线性", "Linear"),
        ["Curve"] = ("曲线", "Curve"),
        ["ProfileName"] = ("卡片名称", "Card name"),
        ["Sensor"] = ("温度传感器", "Temperature sensor"),
        ["Mode"] = ("调速方式", "Mode"),
        ["FixedPercent"] = ("固定转速 (%)", "Fixed speed (%)"),
        ["LowTemperature"] = ("最低温度 (°C)", "Low temperature (°C)"),
        ["HighTemperature"] = ("最高温度 (°C)", "High temperature (°C)"),
        ["LowPercent"] = ("最低温度对应转速 (%)", "Speed at low temperature (%)"),
        ["HighPercent"] = ("最高温度对应转速 (%)", "Speed at high temperature (%)"),
        ["CurvePoints"] = ("曲线节点", "Curve points"),
        ["Points"] = ("个节点", "points"),
        ["CurveHint"] = ("点击空白添加节点，拖动节点调整曲线，右键删除节点。", "Click to add a point, drag to reshape the curve, right-click to remove."),
        ["AddPoint"] = ("添加节点", "Add point"),
        ["RemovePoint"] = ("删除选中节点", "Remove selected"),
        ["Temperature"] = ("温度", "Temperature"),
        ["Speed"] = ("转速", "Speed"),
        ["SaveFailed"] = ("参数无效，请检查温度和转速范围。", "Invalid values. Check temperature and speed ranges."),
        ["DeleteProfile"] = ("确定删除这张调速卡片吗？", "Delete this speed card?"),
        ["DeleteProfileTitle"] = ("删除调速卡片", "Delete speed card"),
        ["NoTemperature"] = ("没有可用的温度传感器", "No temperature sensor available"),
        ["ProfileCreated"] = ("新建调速卡片", "New speed card"),
        ["SpecialThanks"] = ("特别鸣谢", "Special Thanks"),
        ["ThanksText"] = (
            "感谢 LibreHardwareMonitor（Libre）与 PawnIO 提供硬件传感器读取和底层硬件访问能力。",
            "Thanks to LibreHardwareMonitor (Libre) and PawnIO for hardware sensor access and low-level hardware support."),
        ["Close"] = ("关闭", "Close"),
        ["FirstScan"] = ("首次检测完成", "First scan complete"),
        ["Ready"] = ("就绪", "Ready"),
        ["Refresh"] = ("刷新", "Refresh")
    };

    public static string Get(string language, string key)
    {
        if (!Values.TryGetValue(key, out (string Zh, string En) value))
        {
            return key;
        }

        return language.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            ? value.En
            : value.Zh;
    }
}
