using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TrayTemps;

internal partial class SpeedProfileEditorWindow : Window
{
    private readonly string _language;
    private readonly string _profileId;
    private readonly Func<IReadOnlyList<TemperatureChoice>> _temperatureProvider;
    private readonly DispatcherTimer _temperatureTimer;
    private readonly ObservableCollection<TemperatureChoice> _temperatures = [];
    private bool _updatingFixedValue;
    private string? _selectedSensorId;

    internal SpeedProfileEditorWindow(
        SpeedProfileConfig profile,
        Func<IReadOnlyList<TemperatureChoice>> temperatureProvider,
        string language,
        string themeMode)
    {
        _language = language.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "zh-CN";
        _profileId = profile.Id;
        _temperatureProvider = temperatureProvider;
        _selectedSensorId = profile.TemperatureSensorId;
        InitializeComponent();
        ThemeManager.Apply(this, themeMode);
        WindowAnimator.Attach(this);
        ApplyLanguage();

        NameTextBox.Text = profile.Name;
        ModeComboBox.ItemsSource = new[]
        {
            new ModeItem(SpeedMode.Fixed, Strings.Get(_language, "Fixed")),
            new ModeItem(SpeedMode.Linear, Strings.Get(_language, "Linear")),
            new ModeItem(SpeedMode.Curve, Strings.Get(_language, "Curve"))
        };
        ModeComboBox.DisplayMemberPath = nameof(ModeItem.Name);
        ModeComboBox.SelectedValuePath = nameof(ModeItem.Mode);
        ModeComboBox.SelectedValue = profile.Mode;

        SensorComboBox.ItemsSource = _temperatures;
        UpdateTemperatureChoices();
        SensorComboBox.SelectedValue = _selectedSensorId;
        SensorComboBox.DropDownOpened += (_, _) => SuppressComboAutoScroll();
        SensorComboBox.SelectionChanged += (_, _) =>
            _selectedSensorId = SensorComboBox.SelectedValue as string;

        FixedSlider.Value = Math.Clamp(profile.FixedPercent, 0, 100);
        FixedValueTextBox.Text = FixedSlider.Value.ToString(
            "0",
            CultureInfo.InvariantCulture);

        LowTemperatureTextBox.Text = profile.LowTemperature.ToString(
            "0.#",
            CultureInfo.InvariantCulture);
        HighTemperatureTextBox.Text = profile.HighTemperature.ToString(
            "0.#",
            CultureInfo.InvariantCulture);
        LowPercentTextBox.Text = profile.LowPercent.ToString(
            "0",
            CultureInfo.InvariantCulture);
        HighPercentTextBox.Text = profile.HighPercent.ToString(
            "0",
            CultureInfo.InvariantCulture);

        ProfileCurveEditor.SetPoints(profile.CurvePoints);
        UpdateModePanels();

        _temperatureTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _temperatureTimer.Tick += (_, _) =>
        {
            UpdateTemperatureChoices();
        };
        _temperatureTimer.Start();
        Closed += (_, _) => _temperatureTimer.Stop();
    }

    public SpeedProfileConfig? Profile { get; private set; }

    private void ApplyLanguage()
    {
        EditorTitleText.Text = Strings.Get(_language, "ProfilesTitle");
        NameLabel.Text = Strings.Get(_language, "ProfileName");
        ModeLabel.Text = Strings.Get(_language, "Mode");
        SensorLabel.Text = Strings.Get(_language, "Sensor");
        FixedLabel.Text = Strings.Get(_language, "FixedPercent");
        LowTemperatureLabel.Text = Strings.Get(_language, "LowTemperature");
        HighTemperatureLabel.Text = Strings.Get(_language, "HighTemperature");
        LowPercentLabel.Text = Strings.Get(_language, "LowPercent");
        HighPercentLabel.Text = Strings.Get(_language, "HighPercent");
        CurvePointsLabel.Text = Strings.Get(_language, "CurvePoints");
        CurveHint.Text = Strings.Get(_language, "CurveHint");
        SaveButton.Content = Strings.Get(_language, "Save");
        CancelButton.Content = Strings.Get(_language, "Cancel");
    }

    private void ModeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateModePanels();
    }

    private void UpdateModePanels()
    {
        SpeedMode mode = ModeComboBox.SelectedValue is SpeedMode selected
            ? selected
            : SpeedMode.Linear;

        FixedPanel.Visibility = mode == SpeedMode.Fixed
            ? Visibility.Visible
            : Visibility.Collapsed;
        LinearPanel.Visibility = mode == SpeedMode.Linear
            ? Visibility.Visible
            : Visibility.Collapsed;
        CurvePanel.Visibility = mode == SpeedMode.Curve
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void FixedSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingFixedValue || FixedValueTextBox is null)
        {
            return;
        }

        _updatingFixedValue = true;
        FixedValueTextBox.Text = Math.Round(e.NewValue).ToString(
            "0",
            CultureInfo.InvariantCulture);
        _updatingFixedValue = false;
    }

    private void FixedValueTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_updatingFixedValue || FixedSlider is null)
        {
            return;
        }

        if (double.TryParse(
                FixedValueTextBox.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value))
        {
            _updatingFixedValue = true;
            FixedSlider.Value = Math.Clamp(value, 0, 100);
            _updatingFixedValue = false;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SpeedMode mode = ModeComboBox.SelectedValue is SpeedMode selected
            ? selected
            : SpeedMode.Linear;

        string name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Strings.Get(_language, "ProfileCreated");
        }

        string? sensorId = SensorComboBox.SelectedValue as string;
        if (mode != SpeedMode.Fixed && string.IsNullOrWhiteSpace(sensorId))
        {
            ShowValidationError();
            return;
        }

        var profile = new SpeedProfileConfig
        {
            Id = _profileId,
            Name = name,
            Mode = mode,
            TemperatureSensorId = sensorId ?? string.Empty,
            FixedPercent = Math.Clamp(FixedSlider.Value, 0, 100)
        };

        if (mode == SpeedMode.Linear)
        {
            if (!TryParseDouble(LowTemperatureTextBox.Text, out double lowTemperature) ||
                !TryParseDouble(HighTemperatureTextBox.Text, out double highTemperature) ||
                !TryParseDouble(LowPercentTextBox.Text, out double lowPercent) ||
                !TryParseDouble(HighPercentTextBox.Text, out double highPercent) ||
                highTemperature <= lowTemperature ||
                lowPercent is < 0 or > 100 ||
                highPercent is < 0 or > 100)
            {
                ShowValidationError();
                return;
            }

            profile.LowTemperature = lowTemperature;
            profile.HighTemperature = highTemperature;
            profile.LowPercent = lowPercent;
            profile.HighPercent = highPercent;
        }

        if (mode == SpeedMode.Curve)
        {
            List<CurvePoint> points = ProfileCurveEditor.Points.ToList();

            if (points.Count < 2 ||
                points.Any(static point =>
                    point.Percent is < 0 or > 100 ||
                    !double.IsFinite(point.Temperature) ||
                    !double.IsFinite(point.Percent)))
            {
                ShowValidationError();
                return;
            }

            profile.CurvePoints = points
                .Select(static point => new CurvePoint
                {
                    Temperature = point.Temperature,
                    Percent = point.Percent
                })
                .ToList();
        }

        Profile = profile;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void UpdateTemperatureChoices()
    {
        string? selected = SensorComboBox.SelectedValue as string ??
                           _selectedSensorId;
        Dictionary<string, TemperatureChoice> latest = _temperatureProvider()
            .ToDictionary(static item => item.Id, StringComparer.Ordinal);

        for (int index = _temperatures.Count - 1; index >= 0; index--)
        {
            if (!latest.ContainsKey(_temperatures[index].Id))
            {
                _temperatures.RemoveAt(index);
            }
        }

        foreach (TemperatureChoice current in latest.Values)
        {
            TemperatureChoice? existing = _temperatures.FirstOrDefault(
                item => string.Equals(item.Id, current.Id, StringComparison.Ordinal));

            if (existing is null)
            {
                _temperatures.Add(current);
            }
            else
            {
                existing.Update(current.DisplayName, current.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(selected) &&
            !_temperatures.Any(item => item.Id == selected))
        {
            SensorComboBox.SelectedIndex = -1;
            _selectedSensorId = null;
            return;
        }

        if (SensorComboBox.SelectedValue is null &&
            !string.IsNullOrWhiteSpace(selected))
        {
            SensorComboBox.SelectedValue = selected;
        }

        _selectedSensorId = SensorComboBox.SelectedValue as string ?? selected;
    }

    private void SuppressComboAutoScroll()
    {
        if (SensorComboBox.Template.FindName(
                "DropDownScrollViewer",
                SensorComboBox) is not ScrollViewer scrollViewer)
        {
            return;
        }

        scrollViewer.RequestBringIntoView -= OnRequestBringIntoView;
        scrollViewer.RequestBringIntoView += OnRequestBringIntoView;
    }

    private static void OnRequestBringIntoView(
        object sender,
        RequestBringIntoViewEventArgs e)
    {
        e.Handled = true;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ShowValidationError()
    {
        System.Windows.MessageBox.Show(
            Strings.Get(_language, "SaveFailed"),
            Strings.Get(_language, "AppTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private sealed record ModeItem(SpeedMode Mode, string Name)
    {
        public string DisplayText => Name;
    }
}
