namespace Minesweeper.Model;

/// <summary>
/// 扫雷核心逻辑（纯 C#，不依赖 WPF，可独立单测）。
/// 规则口径：
///  - 首次左键点击后才生成雷区，且避开首点及其周边 8 格（宽口径首点安全）；
///  - 空白格（周边雷数为 0）翻开时 flood-fill 连片展开，展开过程跳过已插旗格；
///  - 右键循环标记：无 → 旗 → 问号 → 无；
///  - chord：已翻开的数字格上，周边插旗数等于该数字时，翻开周边所有未插旗格；
///  - 踩雷失败：揭示全部雷，被踩中的雷红底高亮，错插旗的格显示红叉；
///  - 胜利：全部非雷格已翻开；剩余未插旗的雷自动补旗。
/// </summary>
public sealed class GameBoard
{
    private readonly Cell[,] _cells;
    private readonly Random _random = new();
    private bool _minesPlaced;

    public DifficultyPreset Preset { get; }
    public int Rows => Preset.Rows;
    public int Cols => Preset.Cols;
    public int Mines => Preset.Mines;

    /// <summary>当前插旗数。</summary>
    public int FlagCount { get; private set; }

    /// <summary>剩余雷数 = 总雷数 − 插旗数（可为负）。</summary>
    public int RemainingMines => Mines - FlagCount;

    public GameStatus Status { get; private set; } = GameStatus.Ready;

    /// <summary>首次左键翻开是否已发生（用于驱动计时器启动）。</summary>
    public bool FirstClickDone => _minesPlaced;

    /// <summary>任何一次会改变渲染状态的动作完成后触发。</summary>
    public event Action? BoardChanged;

    public GameBoard(DifficultyPreset preset)
    {
        Preset = preset ?? throw new ArgumentNullException(nameof(preset));
        _cells = new Cell[preset.Rows, preset.Cols];
        for (int r = 0; r < preset.Rows; r++)
            for (int c = 0; c < preset.Cols; c++)
                _cells[r, c] = new Cell { Row = r, Col = c };
    }

    public Cell GetCell(int row, int col)
    {
        if (row < 0 || row >= Rows) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0 || col >= Cols) throw new ArgumentOutOfRangeException(nameof(col));
        return _cells[row, col];
    }

    public IEnumerable<Cell> AllCells
    {
        get
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    yield return _cells[r, c];
        }
    }

    /// <summary>左键翻开。</summary>
    public void Reveal(int row, int col)
    {
        if (Status is GameStatus.Won or GameStatus.Lost) return;

        var cell = _cells[row, col];
        if (cell.IsRevealed || cell.Mark == CellMark.Flag) return;

        if (!_minesPlaced)
            PlaceMines(row, col);

        if (cell.IsMine)
        {
            Lose(cell);
            return;
        }

        FloodReveal(cell);
        CheckWin();
        RaiseChanged();
    }

    /// <summary>右键循环标记：无 → 旗 → 问号 → 无。</summary>
    public void ToggleMark(int row, int col)
    {
        if (Status is GameStatus.Won or GameStatus.Lost) return;

        var cell = _cells[row, col];
        if (cell.IsRevealed) return;

        switch (cell.Mark)
        {
            case CellMark.None:
                cell.Mark = CellMark.Flag;
                FlagCount++;
                break;
            case CellMark.Flag:
                cell.Mark = CellMark.Question;
                FlagCount--;
                break;
            case CellMark.Question:
                cell.Mark = CellMark.None;
                break;
        }
        RaiseChanged();
    }

    /// <summary>
    /// chord 快翻：已翻开的数字格上，周边插旗数 == 数字时，翻开周边所有未插旗格。
    /// 双击或左右同按触发。
    /// </summary>
    public void Chord(int row, int col)
    {
        if (Status != GameStatus.Playing) return;

        var cell = _cells[row, col];
        if (!cell.IsRevealed || cell.AdjacentMines == 0) return;

        int flags = 0;
        foreach (var n in Neighbors(cell))
            if (n.Mark == CellMark.Flag) flags++;

        if (flags != cell.AdjacentMines) return;

        // 先探测是否有雷再翻开，保证踩雷时只爆被点中的那颗
        foreach (var n in Neighbors(cell))
        {
            if (n.IsRevealed || n.Mark == CellMark.Flag) continue;
            if (n.IsMine)
            {
                Lose(n);
                return;
            }
        }

        foreach (var n in Neighbors(cell))
        {
            if (n.IsRevealed || n.Mark == CellMark.Flag) continue;
            FloodReveal(n);
        }

        CheckWin();
        RaiseChanged();
    }

    // ---------------- 内部逻辑 ----------------

    private void PlaceMines(int safeRow, int safeCol)
    {
        // 宽口径首点安全：排除首点及其周边 8 格
        var excluded = new HashSet<(int r, int c)>();
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int r = safeRow + dr, c = safeCol + dc;
                if (r >= 0 && r < Rows && c >= 0 && c < Cols)
                    excluded.Add((r, c));
            }

        var candidates = new List<(int r, int c)>();
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (!excluded.Contains((r, c)))
                    candidates.Add((r, c));

        // 极端兜底：可用格不足以布雷时，退化为仅排除首点本身
        if (candidates.Count < Mines)
        {
            candidates.Clear();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (!(r == safeRow && c == safeCol))
                        candidates.Add((r, c));
        }

        // Fisher–Yates 洗牌后取前 Mines 个
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        for (int i = 0; i < Mines && i < candidates.Count; i++)
        {
            var (r, c) = candidates[i];
            _cells[r, c].IsMine = true;
        }

        // 计算周边雷数
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                if (_cells[r, c].IsMine) continue;
                int count = 0;
                foreach (var n in Neighbors(_cells[r, c]))
                    if (n.IsMine) count++;
                _cells[r, c].AdjacentMines = count;
            }

        _minesPlaced = true;
        Status = GameStatus.Playing;
    }

    private void FloodReveal(Cell start)
    {
        start.IsRevealed = true;
        if (start.AdjacentMines != 0) return;

        var stack = new Stack<Cell>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            foreach (var n in Neighbors(cur))
            {
                if (n.IsRevealed || n.IsMine || n.Mark == CellMark.Flag) continue;
                n.IsRevealed = true;
                if (n.AdjacentMines == 0) stack.Push(n);
            }
        }
    }

    private void Lose(Cell exploded)
    {
        exploded.IsExploded = true;
        foreach (var cell in AllCells)
        {
            if (cell.IsMine)
                cell.IsRevealed = true; // 揭示全部雷；错旗格由 UI 依据 Mark=Flag && !IsMine 画红叉
        }
        Status = GameStatus.Lost;
        RaiseChanged();
    }

    private void CheckWin()
    {
        foreach (var cell in AllCells)
        {
            if (!cell.IsMine && !cell.IsRevealed) return;
        }

        // 胜利：剩余未插旗的雷自动补旗（经典行为），保证雷数计数归零
        foreach (var cell in AllCells)
        {
            if (cell.IsMine && cell.Mark != CellMark.Flag)
            {
                cell.Mark = CellMark.Flag;
                FlagCount++;
            }
        }
        Status = GameStatus.Won;
    }

    private IEnumerable<Cell> Neighbors(Cell cell)
    {
        int r = cell.Row, c = cell.Col;
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int nr = r + dr, nc = c + dc;
                if (nr >= 0 && nr < Rows && nc >= 0 && nc < Cols)
                    yield return _cells[nr, nc];
            }
    }

    private void RaiseChanged() => BoardChanged?.Invoke();
}
