using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Minesweeper.Model;

namespace Minesweeper;

/// <summary>
/// 主窗口。游戏规则全部委托给 <see cref="GameBoard"/>（纯逻辑、可单测），
/// 本类只负责事件接线和渲染。
/// </summary>
public partial class MainWindow : Window
{
    private GameBoard _board = null!;
    private Button[,] _cellButtons = null!;
    private Style _cellStyle = null!;
    private Style _cellRevealedStyle = null!;

    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private int _elapsedSeconds;
    private bool _suppressNextClick;
    private bool _faceSurprised;

    public MainWindow()
    {
        InitializeComponent();

        _cellStyle = (Style)FindResource("CellButtonStyle");
        _cellRevealedStyle = (Style)FindResource("CellButtonRevealedStyle");

        _timer.Tick += OnTimerTick;

        StartNewGame(DifficultyPreset.Beginner);
    }

    // ---------------- 新局 / 难度 ----------------

    private void StartNewGame(DifficultyPreset preset)
    {
        if (_board is not null)
            _board.BoardChanged -= OnBoardChanged;

        _board = new GameBoard(preset);
        _board.BoardChanged += OnBoardChanged;

        _timer.Stop();
        _elapsedSeconds = 0;
        _suppressNextClick = false;
        _faceSurprised = false;

        BuildGrid();
        UpdateMenuChecks();
        RefreshAll();
    }

    private void BuildGrid()
    {
        BoardGrid.Children.Clear();
        BoardGrid.Rows = _board.Rows;
        BoardGrid.Columns = _board.Cols;
        _cellButtons = new Button[_board.Rows, _board.Cols];

        for (int r = 0; r < _board.Rows; r++)
        {
            for (int c = 0; c < _board.Cols; c++)
            {
                var btn = new Button
                {
                    Style = _cellStyle,
                    Background = SilverBrush,
                    Tag = (r, c)
                };
                btn.PreviewMouseLeftButtonDown += OnCellLeftButtonDown;
                btn.PreviewMouseRightButtonDown += OnCellRightButtonDown;
                btn.Click += OnCellClick;

                _cellButtons[r, c] = btn;
                BoardGrid.Children.Add(btn);
            }
        }
    }

    private void UpdateMenuChecks()
    {
        MenuBeginner.IsChecked = _board.Preset == DifficultyPreset.Beginner;
        MenuIntermediate.IsChecked = _board.Preset == DifficultyPreset.Intermediate;
        MenuExpert.IsChecked = _board.Preset == DifficultyPreset.Expert;
    }

    // ---------------- 事件：菜单 / 笑脸 / 键盘 ----------------

    private void OnNewGameClick(object sender, RoutedEventArgs e) => StartNewGame(_board.Preset);

    private void OnSmileyClick(object sender, RoutedEventArgs e) => StartNewGame(_board.Preset);

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnDifficultyClick(object sender, RoutedEventArgs e)
    {
        DifficultyPreset preset = ((FrameworkElement)sender).Tag switch
        {
            "Beginner" => DifficultyPreset.Beginner,
            "Intermediate" => DifficultyPreset.Intermediate,
            _ => DifficultyPreset.Expert
        };
        StartNewGame(preset);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            StartNewGame(_board.Preset);
            e.Handled = true;
        }
    }

    private void OnAnyLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _faceSurprised = false;
        UpdateFace();
    }

    // ---------------- 事件：雷区格子 ----------------

    private void OnCellLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var (row, col) = GetCellPos(sender);

        // 双击 chord
        if (e.ClickCount >= 2)
        {
            DoChord(row, col);
            return;
        }

        // 左右同按 chord（按住左键时右键已按下，或反之）
        if (e.RightButton == MouseButtonState.Pressed)
        {
            DoChord(row, col);
            return;
        }

        // 非 chord 的正常按下：清除可能残留的抑制标志
        // （边缘场景：上次 chord 由右键触发后左键在格子外释放，标志未在 Click 中消费）
        _suppressNextClick = false;

        // 按下未翻开的格子时经典"惊愕"表情
        if (_board.Status is GameStatus.Ready or GameStatus.Playing)
        {
            _faceSurprised = true;
            UpdateFace();
        }
    }

    private void OnCellRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Right) return;
        var (row, col) = GetCellPos(sender);

        // 左键按住时按右键 → 左右同按 chord，不再切换标记
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DoChord(row, col);
            return;
        }

        _board.ToggleMark(row, col);
    }

    private void OnCellClick(object sender, RoutedEventArgs e)
    {
        if (_suppressNextClick)
        {
            _suppressNextClick = false;
            return;
        }

        var (row, col) = GetCellPos(sender);
        _faceSurprised = false;
        _board.Reveal(row, col);
    }

    private void DoChord(int row, int col)
    {
        _suppressNextClick = true;
        _faceSurprised = false;
        _board.Chord(row, col);
    }

    private static (int row, int col) GetCellPos(object sender)
    {
        var tuple = (ValueTuple<int, int>)((FrameworkElement)sender).Tag;
        return (tuple.Item1, tuple.Item2);
    }

    // ---------------- 计时 ----------------

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_elapsedSeconds < 999)
            _elapsedSeconds++;
        TimerDisplay.Text = FormatClock(_elapsedSeconds);
    }

    private void OnBoardChanged()
    {
        // 首次左键翻开（含 chord 引发的首翻不可能：chord 仅在 Playing 状态生效）
        if (_board.FirstClickDone && _board.Status == GameStatus.Playing && !_timer.IsEnabled)
            _timer.Start();

        RefreshAll();
    }

    // ---------------- 渲染 ----------------

    private void RefreshAll()
    {
        if (_board.Status is GameStatus.Won or GameStatus.Lost)
            _timer.Stop();

        MineCounter.Text = FormatMines(_board.RemainingMines);
        TimerDisplay.Text = FormatClock(_elapsedSeconds);

        for (int r = 0; r < _board.Rows; r++)
        {
            for (int c = 0; c < _board.Cols; c++)
            {
                RenderCell(_cellButtons[r, c], _board.GetCell(r, c));
            }
        }

        UpdateFace();
    }

    private void RenderCell(Button btn, Cell cell)
    {
        bool lost = _board.Status == GameStatus.Lost;

        if (cell.IsRevealed)
        {
            btn.Style = _cellRevealedStyle;

            if (cell.IsMine)
            {
                // 失败揭示全部雷；正确插旗的雷保持旗子，被踩中的雷红底
                if (lost && cell.Mark == CellMark.Flag && !cell.IsExploded)
                {
                    btn.Background = SilverBrush;
                    btn.Content = CreateFlagContent();
                }
                else
                {
                    btn.Background = cell.IsExploded ? ExplodedBrush : SilverBrush;
                    btn.Content = CreateMineContent();
                }
            }
            else if (cell.AdjacentMines > 0)
            {
                btn.Background = SilverBrush;
                btn.Content = new TextBlock
                {
                    Text = cell.AdjacentMines.ToString(CultureInfo.InvariantCulture),
                    Foreground = GetNumberBrush(cell.AdjacentMines),
                    FontWeight = FontWeights.Bold,
                    FontSize = 13
                };
            }
            else
            {
                btn.Background = SilverBrush;
                btn.Content = null;
            }
        }
        else
        {
            btn.Style = _cellStyle;
            btn.Background = SilverBrush;

            switch (cell.Mark)
            {
                case CellMark.Flag:
                    // 失败时错插旗（非雷）显示红叉旗
                    btn.Content = lost && !cell.IsMine
                        ? CreateWrongFlagContent()
                        : CreateFlagContent();
                    break;
                case CellMark.Question:
                    btn.Content = new TextBlock
                    {
                        Text = "?",
                        FontWeight = FontWeights.Bold,
                        FontSize = 13,
                        Foreground = Brushes.Black
                    };
                    break;
                default:
                    btn.Content = null;
                    break;
            }
        }
    }

    private void UpdateFace()
    {
        SmileyButton.Content = _board.Status switch
        {
            GameStatus.Won => CreateFace(FaceState.Cool),
            GameStatus.Lost => CreateFace(FaceState.Dead),
            _ => CreateFace(_faceSurprised ? FaceState.Surprised : FaceState.Smile)
        };
    }

    // ---------------- 格子内容（纯矢量，无图片资源） ----------------

    private enum FaceState { Smile, Surprised, Cool, Dead }

    private static readonly Brush SilverBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0));
    private static readonly Brush ExplodedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00));

    private static readonly Brush[] NumberBrushes =
    {
        Brushes.Black,                                                  // 0 未用
        new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0xFF)),           // 1 蓝
        new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x00)),           // 2 绿
        new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)),           // 3 红
        new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x80)),           // 4 深蓝
        new SolidColorBrush(Color.FromRgb(0x80, 0x00, 0x00)),           // 5 深红
        new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x80)),           // 6 青
        Brushes.Black,                                                  // 7 黑
        new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),           // 8 灰
    };

    private static Brush GetNumberBrush(int n) =>
        NumberBrushes[Math.Clamp(n, 1, 8)];

    private static string FormatMines(int remaining)
    {
        if (remaining < 0)
            return "-" + Math.Min(Math.Abs(remaining), 99).ToString("00", CultureInfo.InvariantCulture);
        return Math.Min(remaining, 999).ToString("000", CultureInfo.InvariantCulture);
    }

    private static string FormatClock(int seconds) =>
        Math.Clamp(seconds, 0, 999).ToString("000", CultureInfo.InvariantCulture);

    private static Canvas CreateFlagContent()
    {
        var canvas = new Canvas { Width = 16, Height = 16 };

        var pole = new Rectangle { Width = 2, Height = 11, Fill = Brushes.Black };
        Canvas.SetLeft(pole, 4);
        Canvas.SetTop(pole, 2);

        var flag = new Polygon
        {
            Points = new PointCollection { new Point(6, 2), new Point(14, 6), new Point(6, 10) },
            Fill = Brushes.Red
        };

        var ground = new Rectangle { Width = 8, Height = 2, Fill = Brushes.Black };
        Canvas.SetLeft(ground, 1);
        Canvas.SetTop(ground, 13);

        canvas.Children.Add(pole);
        canvas.Children.Add(flag);
        canvas.Children.Add(ground);
        return canvas;
    }

    private static Canvas CreateWrongFlagContent()
    {
        var canvas = CreateFlagContent();

        var x1 = new Line { X1 = 1, Y1 = 1, X2 = 15, Y2 = 15, Stroke = Brushes.Red, StrokeThickness = 2 };
        var x2 = new Line { X1 = 1, Y1 = 15, X2 = 15, Y2 = 1, Stroke = Brushes.Red, StrokeThickness = 2 };
        canvas.Children.Add(x1);
        canvas.Children.Add(x2);
        return canvas;
    }

    private static Canvas CreateMineContent()
    {
        var canvas = new Canvas { Width = 16, Height = 16 };

        var mine = new Ellipse { Width = 8, Height = 8, Fill = Brushes.Black };
        Canvas.SetLeft(mine, 4);
        Canvas.SetTop(mine, 4);

        var vertical = new Line { X1 = 8, Y1 = 1, X2 = 8, Y2 = 15, Stroke = Brushes.Black, StrokeThickness = 1 };
        var horizontal = new Line { X1 = 1, Y1 = 8, X2 = 15, Y2 = 8, Stroke = Brushes.Black, StrokeThickness = 1 };
        var diag1 = new Line { X1 = 3, Y1 = 3, X2 = 13, Y2 = 13, Stroke = Brushes.Black, StrokeThickness = 1 };
        var diag2 = new Line { X1 = 13, Y1 = 3, X2 = 3, Y2 = 13, Stroke = Brushes.Black, StrokeThickness = 1 };

        var highlight = new Ellipse { Width = 2, Height = 2, Fill = Brushes.White };
        Canvas.SetLeft(highlight, 5.5);
        Canvas.SetTop(highlight, 5.5);

        canvas.Children.Add(vertical);
        canvas.Children.Add(horizontal);
        canvas.Children.Add(diag1);
        canvas.Children.Add(diag2);
        canvas.Children.Add(mine);
        canvas.Children.Add(highlight);
        return canvas;
    }

    private static Canvas CreateFace(FaceState state)
    {
        var canvas = new Canvas { Width = 24, Height = 24 };

        var face = new Ellipse
        {
            Width = 22,
            Height = 22,
            Fill = Brushes.Yellow,
            Stroke = Brushes.Black,
            StrokeThickness = 1
        };
        Canvas.SetLeft(face, 1);
        Canvas.SetTop(face, 1);
        canvas.Children.Add(face);

        switch (state)
        {
            case FaceState.Smile:
            case FaceState.Surprised:
                AddDotEye(canvas, 7, 8);
                AddDotEye(canvas, 14, 8);
                break;
            case FaceState.Cool:
                var band = new Rectangle { Width = 12, Height = 3, Fill = Brushes.Black };
                Canvas.SetLeft(band, 6);
                Canvas.SetTop(band, 8);
                canvas.Children.Add(band);
                var leftLens = new Rectangle { Width = 6, Height = 5, Fill = Brushes.Black };
                Canvas.SetLeft(leftLens, 4);
                Canvas.SetTop(leftLens, 8);
                var rightLens = new Rectangle { Width = 6, Height = 5, Fill = Brushes.Black };
                Canvas.SetLeft(rightLens, 14);
                Canvas.SetTop(rightLens, 8);
                canvas.Children.Add(leftLens);
                canvas.Children.Add(rightLens);
                break;
            case FaceState.Dead:
                AddXEye(canvas, 8, 9);
                AddXEye(canvas, 16, 9);
                break;
        }

        switch (state)
        {
            case FaceState.Smile:
                AddPath(canvas, "M 6,13 Q 12,19 18,13");
                break;
            case FaceState.Surprised:
                var mouth = new Ellipse { Width = 5, Height = 6, Fill = Brushes.Black };
                Canvas.SetLeft(mouth, 9.5);
                Canvas.SetTop(mouth, 14);
                canvas.Children.Add(mouth);
                break;
            case FaceState.Cool:
                AddPath(canvas, "M 7,15 Q 12,20 17,15");
                break;
            case FaceState.Dead:
                AddPath(canvas, "M 6,17 Q 12,12 18,17");
                break;
        }

        return canvas;
    }

    private static void AddDotEye(Canvas canvas, double left, double top)
    {
        var eye = new Ellipse { Width = 3, Height = 3, Fill = Brushes.Black };
        Canvas.SetLeft(eye, left);
        Canvas.SetTop(eye, top);
        canvas.Children.Add(eye);
    }

    private static void AddXEye(Canvas canvas, double cx, double cy)
    {
        var l1 = new Line { X1 = cx - 2.5, Y1 = cy - 2.5, X2 = cx + 2.5, Y2 = cy + 2.5, Stroke = Brushes.Black, StrokeThickness = 1.5 };
        var l2 = new Line { X1 = cx - 2.5, Y1 = cy + 2.5, X2 = cx + 2.5, Y2 = cy - 2.5, Stroke = Brushes.Black, StrokeThickness = 1.5 };
        canvas.Children.Add(l1);
        canvas.Children.Add(l2);
    }

    private static void AddPath(Canvas canvas, string data)
    {
        var path = new Path
        {
            Data = Geometry.Parse(data),
            Stroke = Brushes.Black,
            StrokeThickness = 1.5
        };
        canvas.Children.Add(path);
    }
}
