using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Minesweeper.Controls;

/// <summary>
/// 单个七段数码管字符。支持 0-9、'-'（中横）与空白。
/// </summary>
public partial class SevenSegmentDigit : UserControl
{
    // 段位编码：A=1, B=2, C=4, D=8, E=16, F=32, G=64
    // 注意：常量名不能叫 SegA..SegG，会与 XAML x:Name 生成的同名字段冲突（CS0102）
    private const byte BitA = 1 << 0, BitB = 1 << 1, BitC = 1 << 2,
                       BitD = 1 << 3, BitE = 1 << 4, BitF = 1 << 5, BitG = 1 << 6;

    private static readonly Dictionary<char, byte> SegmentMap = new()
    {
        ['0'] = BitA | BitB | BitC | BitD | BitE | BitF,
        ['1'] = BitB | BitC,
        ['2'] = BitA | BitB | BitG | BitE | BitD,
        ['3'] = BitA | BitB | BitG | BitC | BitD,
        ['4'] = BitF | BitG | BitB | BitC,
        ['5'] = BitA | BitF | BitG | BitC | BitD,
        ['6'] = BitA | BitF | BitG | BitE | BitD | BitC,
        ['7'] = BitA | BitB | BitC,
        ['8'] = BitA | BitB | BitC | BitD | BitE | BitF | BitG,
        ['9'] = BitA | BitB | BitC | BitD | BitF | BitG,
        ['-'] = BitG,
        [' '] = 0,
    };

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(char), typeof(SevenSegmentDigit),
            new PropertyMetadata(' ', OnValueChanged));

    public char Value
    {
        get => (char)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public SevenSegmentDigit()
    {
        InitializeComponent();
        ApplySegments(' ');
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SevenSegmentDigit)d).ApplySegments((char)e.NewValue);
    }

    private void ApplySegments(char value)
    {
        byte mask = SegmentMap.TryGetValue(value, out var m) ? m : (byte)0;

        SegA.Visibility = Visible(mask, BitA);
        SegB.Visibility = Visible(mask, BitB);
        SegC.Visibility = Visible(mask, BitC);
        SegD.Visibility = Visible(mask, BitD);
        SegE.Visibility = Visible(mask, BitE);
        SegF.Visibility = Visible(mask, BitF);
        SegG.Visibility = Visible(mask, BitG);
    }

    private static Visibility Visible(byte mask, byte segment) =>
        (mask & segment) != 0 ? Visibility.Visible : Visibility.Collapsed;
}
