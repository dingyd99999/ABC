namespace Minesweeper.Model;

/// <summary>难度档位（Win95 官方默认）。</summary>
public sealed record DifficultyPreset(string Name, int Cols, int Rows, int Mines)
{
    public static readonly DifficultyPreset Beginner = new("初级", 9, 9, 10);
    public static readonly DifficultyPreset Intermediate = new("中级", 16, 16, 40);
    public static readonly DifficultyPreset Expert = new("高级", 30, 16, 99);

    /// <summary>全部档位，按 初级/中级/高级 顺序。</summary>
    public static readonly IReadOnlyList<DifficultyPreset> All =
        new[] { Beginner, Intermediate, Expert };
}
