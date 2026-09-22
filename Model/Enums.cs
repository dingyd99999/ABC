namespace Minesweeper.Model;

/// <summary>格子标记（右键循环：无 → 旗 → 问号 → 无）。</summary>
public enum CellMark
{
    None = 0,
    Flag = 1,
    Question = 2
}

/// <summary>游戏状态。</summary>
public enum GameStatus
{
    /// <summary>尚未开始（未产生雷区、计时未启动）。</summary>
    Ready = 0,
    Playing = 1,
    Won = 2,
    Lost = 3
}
