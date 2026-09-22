using System.Windows;
using System.Windows.Controls;

namespace Minesweeper.Controls;

/// <summary>
/// 多位 LED 显示。Text 变化时按字符重建七段数码管（雷数计数需支持负号位，位数不固定）。
/// </summary>
public partial class LedDisplay : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(LedDisplay),
            new PropertyMetadata(string.Empty, OnTextChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public LedDisplay()
    {
        InitializeComponent();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((LedDisplay)d).Rebuild((string?)e.NewValue ?? string.Empty);
    }

    private void Rebuild(string text)
    {
        DigitsPanel.Children.Clear();
        foreach (var ch in text)
        {
            DigitsPanel.Children.Add(new SevenSegmentDigit { Value = ch });
        }
    }
}
