# Win95 经典扫雷（C# / WPF）

Windows 95 经典扫雷复刻，**C# + WPF（.NET 8）**，单机桌面应用，无后端、无外部资源依赖（旗子/雷/笑脸/LED 数码管全部为 WPF 矢量绘制与样式，可移植）。

## 玩法规格

- **三档难度**（Win95 官方默认，游戏菜单切换即重开新局）：
  - 初级：9×9，10 雷
  - 中级：16×16，40 雷
  - 高级：30×16（宽×高），99 雷
- **计时器**：首次左键翻开才开始计时，三位 LED 数码管显示（0–999 封顶），胜负结算即停；
- **雷数计数器**：剩余雷数 = 总雷数 − 插旗数，支持负数（显示为 `-xx`）；
- **左键**翻开，**右键**循环标记：旗 → 问号 → 空；
- **chord 快翻**：已翻开的数字格上**双击**，或**左右键同按**，当周边插旗数等于该数字时翻开周边所有未插旗格；
- **首点安全（宽口径）**：雷区在首次左键点击后才生成，且避开首点及其周边 8 格；
- **flood-fill**：翻开空白格（周边雷数 0）自动连片展开（跳过已插旗格）；
- **失败**：踩雷后揭示全部雷，被踩中的雷红底高亮，错插旗的格显示红叉旗；
- **胜利**：全部非雷格翻开即胜，剩余未插旗的雷自动补旗；
- **笑脸按钮 / F2 / 菜单"新局"**：随时重开新局，计数与计时器不残留。

## 工程结构

```
Minesweeper/
├── Minesweeper.csproj          # .NET 8, net8.0-windows, UseWPF
├── App.xaml / App.xaml.cs
├── MainWindow.xaml             # Win95 复古样式（斜面边框、LED、笑脸按钮）
├── MainWindow.xaml.cs          # 事件接线 + 渲染（矢量绘制旗/雷/笑脸）
├── Model/
│   ├── Enums.cs                # CellMark / GameStatus
│   ├── DifficultyPreset.cs     # 三档难度预设（record）
│   ├── Cell.cs                 # 格子数据
│   └── GameBoard.cs            # 核心规则（纯 C#，不依赖 WPF，可独立单测）
├── Controls/
│   ├── SevenSegmentDigit.xaml(.cs)   # 单个七段数码管（矩形段位矢量绘制）
│   └── LedDisplay.xaml(.cs)          # 多位 LED 显示（按字符串重建数码管）
└── LogicTests/                 # 核心逻辑行为测试（Linux/macOS/Windows 均可跑）
    ├── LogicTests.csproj       # 以 Compile Include 链接主工程 Model 源码（无双源）
    └── Program.cs              # 24 项断言（见下）
```

## 核心逻辑自动化测试

`LogicTests` 是纯 .NET 8 控制台工程（不依赖 WPF，跨平台可跑），直接链接主工程 `Model/` 源码做行为级验证：

```bash
cd LogicTests
dotnet run
```

覆盖 24 项断言：三档难度首点安全 ×30（首点及 8 邻域无雷）、首点后才进入 Playing、雷数与邻域计数一致性 ×50、flood-fill 连片展开一致性 ×50、标记循环与负数计数、踩雷判负并揭示全部雷、结束后操作失效、全部非雷翻开判胜 + 自动补旗归零、chord 快翻 / 旗数不匹配无效 / 错旗踩雷判负、旗格左键无效。

## 构建与运行（Windows）

前置：安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（或 Visual Studio 2022 + .NET 桌面开发工作负载）。

> zip 解压后**根目录即主工程**（`Minesweeper.csproj` 与源码同级），无需再进入子目录；`LogicTests/` 是嵌套其中的独立测试工程，已被主工程显式排除编译。

```powershell
# 在解压后的根目录直接运行
dotnet run
```

或用 Visual Studio 打开 `Minesweeper.csproj`，直接 F5。

仅编译不运行：

```powershell
dotnet build
```

## 在 Linux/macOS 上做编译级验证（可选）

WPF UI 无法在非 Windows 系统运行，但可以做编译验证：

```bash
dotnet build -p:EnableWindowsTargeting=true
```

（会从 NuGet 拉取 Windows 桌面引用包，需要网络。运行时行为仍需在 Windows 上确认。）

## 验证状态（如实声明）

- **已做（沙箱 Linux 实际执行）**：
  - `dotnet build -p:EnableWindowsTargeting=true` 编译级验证：**Build succeeded，0 错误 0 警告**（.NET SDK 8.0.425）；
  - `LogicTests` 行为测试：**24 项断言全部通过**（详见上一节）。
- **未验证（沙箱为 Linux，无 WPF 运行时，无法运行 UI）**：实际鼠标点击交互（含左右同按 chord 的真实事件序列）、计时器走秒、窗口布局渲染效果、菜单勾选显示、笑脸按下动画等一切运行时表现，需在 Windows 上 `dotnet run` 确认。
