using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using TextBox = System.Windows.Controls.TextBox;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace TrayTemps;

internal sealed class TextInputWindow : Window
{
    private readonly TextBox _textBox;

    public TextInputWindow(
        string title,
        string prompt,
        string initialValue,
        string okText,
        string cancelText,
        string themeMode)
    {
        ThemeManager.Apply(this, themeMode);
        WindowAnimator.Attach(this);
        Background = GetBrush("AppBackground");
        Foreground = GetBrush("TextPrimary");
        Title = title;
        Width = 430;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = GetBrush("AppBackground");
        FontFamily = new FontFamily("Microsoft YaHei UI");
        FontSize = 13;

        var root = new Grid
        {
            Margin = new Thickness(22)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var promptText = new TextBlock
        {
            Text = prompt,
            Foreground = GetBrush("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 10)
        };
        root.Children.Add(promptText);
        Grid.SetRow(promptText, 0);

        _textBox = new TextBox
        {
            Text = initialValue,
            Height = 38,
            Padding = new Thickness(10, 5, 10, 5),
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = GetBrush("BorderBrush"),
            BorderThickness = new Thickness(1),
            Background = GetBrush("CardBackground")
        };
        root.Children.Add(_textBox);
        Grid.SetRow(_textBox, 1);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        var cancelButton = CreateButton(cancelText, isPrimary: false);
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => DialogResult = false;

        var okButton = CreateButton(okText, isPrimary: true);
        okButton.Click += (_, _) => DialogResult = true;

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(okButton);
        root.Children.Add(buttons);
        Grid.SetRow(buttons, 2);

        Content = root;
        Loaded += (_, _) =>
        {
            _textBox.Focus();
            _textBox.SelectAll();
        };
    }

    public string Value => _textBox.Text.Trim();

    private Brush GetBrush(string key)
    {
        return TryFindResource(key) as Brush ?? Brushes.Black;
    }

    private WpfButton CreateButton(string text, bool isPrimary)
    {
        var button = new WpfButton
        {
            Content = text,
            Width = 82,
            Height = 34,
            BorderThickness = new Thickness(0),
            Foreground = isPrimary ? Brushes.White : GetBrush("TextPrimary"),
            Background = isPrimary
                ? GetBrush("Accent")
                : GetBrush("ControlBackground"),
            FontWeight = isPrimary ? FontWeights.SemiBold : FontWeights.Normal,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        return button;
    }
}
