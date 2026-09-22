namespace Minesweeper.Model;

/// <summary>
/// 单个格子。纯数据对象，不依赖 WPF。
/// </summary>
public sealed class Cell
{
    public required int Row { get; init; }
    public required int Col { get; init; }

    /// <summary>是否为雷。</summary>
    public bool IsMine { get; internal set; }

    /// <summary>是否已翻开。</summary>
    public bool IsRevealed { get; internal set; }

    /// <summary>当前标记（旗/问号）。</summary>
    public CellMark Mark { get; internal set; } = CellMark.None;

    /// <summary>周边 8 格雷数（非雷格有效）。</summary>
    public int AdjacentMines { get; internal set; }

    /// <summary>是否为被踩中的那颗雷（仅失败时，用于红底高亮）。</summary>
    public bool IsExploded { get; internal set; }
}
