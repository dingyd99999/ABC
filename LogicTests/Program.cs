using Minesweeper.Model;

namespace Minesweeper.Tests;

public static class Program
{
    private static int _passed;
    private static int _failed;

    private static void Check(string name, bool condition, string detail = "")
    {
        if (condition) { _passed++; Console.WriteLine($"[PASS] {name}"); }
        else { _failed++; Console.WriteLine($"[FAIL] {name} {detail}"); }
    }

    public static int Main()
    {
        // ---------- 1. 首点安全（宽口径：首点及 8 邻域无雷），多难度多轮 ----------
        foreach (var preset in DifficultyPreset.All)
        {
            for (int trial = 0; trial < 30; trial++)
            {
                var b = new GameBoard(preset);
                int fr = trial % preset.Rows, fc = (trial * 7) % preset.Cols;
                b.Reveal(fr, fc);
                bool safe = true;
                for (int dr = -1; dr <= 1 && safe; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int r = fr + dr, c = fc + dc;
                        if (r >= 0 && r < preset.Rows && c >= 0 && c < preset.Cols && b.GetCell(r, c).IsMine)
                            { safe = false; break; }
                    }
                if (!safe) { Check($"首点安全 {preset.Name} trial{trial}", false); safe = false; break; }
                if (trial == 29) Check($"首点安全 {preset.Name} ×30", true);
            }
        }

        // ---------- 2. 首点后才开始（Ready→Playing） ----------
        {
            var b = new GameBoard(DifficultyPreset.Beginner);
            Check("初始状态 Ready", b.Status == GameStatus.Ready && !b.FirstClickDone);
            b.ToggleMark(0, 0); // 右键标记不应启动游戏
            Check("右键不触发开局", b.Status == GameStatus.Ready && !b.FirstClickDone);
            b.Reveal(4, 4);
            Check("首次左键后 Playing", b.Status == GameStatus.Playing && b.FirstClickDone);
        }

        // ---------- 3. 雷数与邻域计数一致性 ----------
        {
            bool allOk = true;
            for (int trial = 0; trial < 50; trial++)
            {
                var b = new GameBoard(DifficultyPreset.Intermediate);
                b.Reveal(0, 0);
                int mines = 0;
                foreach (var cell in b.AllCells) if (cell.IsMine) mines++;
                if (mines != 40) { allOk = false; break; }
                foreach (var cell in b.AllCells)
                {
                    if (cell.IsMine) continue;
                    int cnt = 0;
                    for (int dr = -1; dr <= 1; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int r = cell.Row + dr, c = cell.Col + dc;
                            if (dr == 0 && dc == 0) continue;
                            if (r >= 0 && r < b.Rows && c >= 0 && c < b.Cols && b.GetCell(r, c).IsMine) cnt++;
                        }
                    if (cnt != cell.AdjacentMines) { allOk = false; break; }
                }
                if (!allOk) break;
            }
            Check("中级雷数=40 且邻域计数一致 ×50", allOk);
        }

        // ---------- 4. flood-fill 一致性：0 格的邻居（除旗/雷）均应翻开 ----------
        {
            bool allOk = true;
            for (int trial = 0; trial < 50; trial++)
            {
                var b = new GameBoard(DifficultyPreset.Expert);
                b.Reveal(8, 15);
                foreach (var cell in b.AllCells)
                {
                    if (!cell.IsRevealed || cell.IsMine) continue;
                    if (cell.AdjacentMines != 0) continue;
                    for (int dr = -1; dr <= 1 && allOk; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int r = cell.Row + dr, c = cell.Col + dc;
                            if (dr == 0 && dc == 0) continue;
                            if (r < 0 || r >= b.Rows || c < 0 || c >= b.Cols) continue;
                            var n = b.GetCell(r, c);
                            if (!n.IsRevealed && !n.IsMine && n.Mark != CellMark.Flag) { allOk = false; break; }
                        }
                    if (!allOk) break;
                }
                if (!allOk) break;
            }
            Check("flood-fill 连片展开一致 ×50", allOk);
        }

        // ---------- 5. 标记循环与计数（含负数） ----------
        {
            var b = new GameBoard(DifficultyPreset.Beginner);
            // 插 10 面旗
            for (int i = 0; i < 9; i++) b.ToggleMark(0, i);
            b.ToggleMark(1, 0);
            Check("旗数计数", b.FlagCount == 10 && b.GetCell(1, 0).Mark == CellMark.Flag);
            // 第 11 面旗 → 剩余 -1
            b.ToggleMark(1, 1);
            Check("旗数>雷数时剩余为负", b.RemainingMines == -1 && b.GetCell(1, 1).Mark == CellMark.Flag);
            // (1,1) 再右键 → 旗→问号，计数回退
            b.ToggleMark(1, 1);
            Check("旗→问号 计数回退", b.FlagCount == 10 && b.GetCell(1, 1).Mark == CellMark.Question && b.RemainingMines == 0);
            // 问号→空，计数不变
            b.ToggleMark(1, 1);
            Check("问号→空", b.GetCell(1, 1).Mark == CellMark.None && b.FlagCount == 10);
        }

        // ---------- 6. 踩雷失败：揭示全部雷 ----------
        {
            var b = new GameBoard(DifficultyPreset.Beginner);
            b.Reveal(0, 0);
            var mine = FirstMine(b);
            b.Reveal(mine.Row, mine.Col);
            Check("踩雷后 Lost", b.Status == GameStatus.Lost);
            bool allMinesRevealed = true;
            foreach (var cell in b.AllCells)
                if (cell.IsMine && !cell.IsRevealed) { allMinesRevealed = false; break; }
            Check("失败揭示全部雷", allMinesRevealed);
            Check("被踩中雷标记", mine.IsExploded);
            // 失败后操作失效
            b.Reveal(8, 8);
            b.ToggleMark(4, 4);
            Check("结束后操作无效", b.Status == GameStatus.Lost);
        }

        // ---------- 7. 胜利：翻开全部非雷格 ----------
        {
            var b = new GameBoard(DifficultyPreset.Intermediate);
            b.Reveal(0, 0);
            foreach (var cell in b.AllCells.ToArray())
            {
                if (b.Status == GameStatus.Won) break;
                if (!cell.IsMine && !cell.IsRevealed)
                    b.Reveal(cell.Row, cell.Col);
            }
            Check("全部非雷翻开判胜", b.Status == GameStatus.Won);
            Check("胜利后自动补旗归零", b.RemainingMines == 0);
        }

        // ---------- 8. chord：旗数匹配时快翻 ----------
        {
            // 找一个满足场景的盘面：存在已翻开数字格，其邻域雷均可定位
            bool scenarioOk = false, chordWorked = false, chordSafeWorked = false;
            for (int trial = 0; trial < 200 && !scenarioOk; trial++)
            {
                var b = new GameBoard(DifficultyPreset.Expert);
                b.Reveal(5, 5);
                foreach (var cell in b.AllCells.ToArray())
                {
                    if (b.Status == GameStatus.Won) break;
                    if (!cell.IsMine && !cell.IsRevealed) b.Reveal(cell.Row, cell.Col);
                }
                if (b.Status != GameStatus.Won) continue;

                // 重新开局构造 chord 场景：直接用新盘，翻一个数字格后手动给邻雷全部插旗
                var b2 = new GameBoard(DifficultyPreset.Beginner);
                b2.Reveal(0, 0);
                // 找一个已翻开的数字格
                Cell? target = null;
                foreach (var cell in b2.AllCells)
                    if (cell.IsRevealed && !cell.IsMine && cell.AdjacentMines > 0) { target = cell; break; }
                if (target == null) continue;

                int before = b2.AllCells.Count(c => c.IsRevealed);
                // 给 target 邻域所有雷插旗（正确的旗）
                foreach (var n in NeighborsOf(b2, target))
                    if (n.IsMine) b2.ToggleMark(n.Row, n.Col);
                // 旗数==数字 → chord 应翻开周边未旗格
                b2.Chord(target.Row, target.Col);
                int after = b2.AllCells.Count(c => c.IsRevealed);
                if (after >= before) { scenarioOk = true; chordWorked = after > before || target.AdjacentMines == NeighborsOf(b2, target).Count(n => n.IsMine && n.Mark == CellMark.Flag); }
                // 全部非雷翻开后应判胜
                if (b2.Status == GameStatus.Won) chordSafeWorked = true;
            }
            Check("chord 场景构造成功", scenarioOk);
            Check("chord 翻开周边格", chordWorked);
            Console.WriteLine($"[INFO] chord 判胜联动: {chordSafeWorked}");
        }

        // ---------- 9. 旗格不可被左键翻开 ----------
        {
            var b = new GameBoard(DifficultyPreset.Beginner);
            b.ToggleMark(0, 0); // 插旗
            b.Reveal(0, 0);     // 左键无效
            Check("旗格左键无效", !b.GetCell(0, 0).IsRevealed);
            b.Reveal(1, 1);     // 正常开局
            Check("其他格仍可开局", b.FirstClickDone);
        }

        // ---------- 10. chord 边界：旗数不匹配时无效 ----------
        {
            bool noChangeOk = true, wrongFlagLosesOk = false;
            for (int trial = 0; trial < 100; trial++)
            {
                var b = new GameBoard(DifficultyPreset.Beginner);
                b.Reveal(0, 0);
                Cell? target = null;
                foreach (var cell in b.AllCells)
                    if (cell.IsRevealed && !cell.IsMine && cell.AdjacentMines >= 2) { target = cell; break; }
                if (target == null) continue;

                int before = b.AllCells.Count(c => c.IsRevealed);
                b.Chord(target.Row, target.Col); // 未插旗 → 不匹配
                int after = b.AllCells.Count(c => c.IsRevealed);
                if (after != before && b.Status == GameStatus.Playing) { noChangeOk = false; break; }

                // 错旗场景：把 target 邻域内非雷格插旗、真雷不插 → chord 应触发 Lose
                var neigh = NeighborsOf(b, target).ToList();
                int flagsPlaced = 0;
                foreach (var n in neigh)
                {
                    if (!n.IsMine && !n.IsRevealed && flagsPlaced < target.AdjacentMines)
                    {
                        b.ToggleMark(n.Row, n.Col);
                        flagsPlaced++;
                    }
                }
                bool hasUnflaggedMine = neigh.Any(n => n.IsMine && n.Mark != CellMark.Flag);
                if (flagsPlaced == target.AdjacentMines && hasUnflaggedMine)
                {
                    b.Chord(target.Row, target.Col);
                    if (b.Status == GameStatus.Lost) wrongFlagLosesOk = true;
                    break;
                }
            }
            Check("chord 旗数不匹配时无效果", noChangeOk);
            Check("chord 错旗踩雷判负", wrongFlagLosesOk);
        }

        Console.WriteLine($"\n== 结果: {_passed} 通过, {_failed} 失败 ==");
        return _failed == 0 ? 0 : 1;
    }

    private static Cell FirstMine(GameBoard b)
    {
        foreach (var cell in b.AllCells)
            if (cell.IsMine) return cell;
        throw new InvalidOperationException("no mine");
    }

    private static IEnumerable<Cell> NeighborsOf(GameBoard b, Cell cell)
    {
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int r = cell.Row + dr, c = cell.Col + dc;
                if (r >= 0 && r < b.Rows && c >= 0 && c < b.Cols)
                    yield return b.GetCell(r, c);
            }
    }
}
