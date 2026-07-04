# 更新日志

## v1.2.0 — 2026-07-04

### 性能优化
- **移除 ExpandItems DFS 遍历** — 原先每个条件调用 `ExpandItems` 做 DFS 展开 scope 节点（10 万节点 ~8.3s），改为直接将 scope 根节点传给 Search API，由 C++ 引擎层原生处理 scope 范围
- **重写 ProtectedKeepService** — 原先 3 次全模型遍历（`.DescendantsAndSelf` × 匹配节点数 ~539s），改为 BFS 从 RootItem 起按 DisplayName 查找，命中即停；STR 节点通常在深度 1（第 3-4 个子节点），从遍历 60K 节点降为 3-4 次比较
- **scope ModelItemCollection 预建复用** — 循环外构建一次 `ModelItemCollection`，避免每条件 `CopyFrom(IEnumerable<ModelItem>)` 的隐式转换开销
- **移除 FilterResultsToScope** — Search API 自带 scope 过滤，无需 .NET 端二次过滤
- **BuildFromNode 快速路径** — 当调用方已知 STR 节点（用户已选中），直接构建结果跳过 BFS 查找

### 修复
- **选项卡标签文字被裁切** — `TabSizeMode.Fixed` 改为 `Normal`，标签宽度自动适配文字
- **工具栏列数错误** — 移除硬编码 `ColumnCount=7`，只用 `ColumnStyles.Add()` 追加列，消除预建 AutoSize 列 + 追加 Percent 列导致的列数翻倍
- **选项页内文字被裁切** — CheckBox 和 GroupBox 改用 `AutoSize=true`，消除硬编码宽度魔数
- **底部按钮 Y 轴不对齐** — 关闭按钮改为 `Dock.Fill` 布局，消除缺失 Y 坐标导致的位置偏移
- **选项界面高度计算** — `checkBoxHeight*2+44` 替代原先遗漏 GroupBox 标题栏的 `+24`，消除文字挤压

### 重构
- 底部三个按钮统一 `Dock.Fill` 布局，移除硬编码 Size/Location
- 删除死代码 `HideService.cs`（已被 `HideServiceFixed.cs` 替代）
- 删除 `ModelDiagnostics.cs` 诊断工具（已剔除出插件）
- 安装脚本从 `.bat` 迁移到 `scripts/install_2023.ps1`（支持 `NAVISWORKS_2023_PATH` 环境变量配置）
- 插件清单移至 `manifests/` 目录

### 界面优化
- 新增「使用指南」按钮，方便用户查阅操作说明
- 选项卡页统一背景色和内边距
- 按钮配色更新：主按钮 blue-600，次按钮白底 slate 描边
- 底部面板高度从 34px 增至 44px（更宽松的点击区域）
- 全局 DPI 自适应（`ScaleLogical()` 覆盖所有间距和尺寸）

---

## v1.1.0 — 2026-07-02

### 修复
- **选项卡标签文字被裁切** — `TabSizeMode.Fixed` 改为 `Normal`，标签宽度自动适配文字
- **选项页内文字被裁切** — CheckBox 和 GroupBox 改用 `AutoSize=true`，消除硬编码宽度魔数
- **底部按钮 Y 轴不对齐** — 关闭按钮缺失 Y 坐标（默认 0），导致比搜索/导出按钮高 6px
- **两个模式间距过大** — 模式 A/B 从 Y=80 压缩到 Y=40，GroupBox 从 120/220 压缩到 72/152

### 重构
- 底部三个按钮统一字体（`Microsoft YaHei UI 9pt Bold`）
- 所有间距改用 `ScaleLogical()` 实现 DPI 自适应
- 删除死代码 `HideService.cs`（已被 `HideServiceFixed.cs` 替代）

---

## v1.0.0 — 2026-06-29

### 新增
- 读取傑出品 XML 在 Navisworks Manage 2023 中执行属性查找
- 支持 `equals` / `contains` 两种匹配方式
- 无分类（category）的 XML 自动采样模型发现所属分类
- 两段式安全设计：模式 A（仅查找选中）/ 模式 B（查找 + 隐藏未选中）
- 搜索范围限定（选择树预选节点）
- STR 结构节点保护（隐藏时自动保留 `{前缀}-STR` 节点）
- 零匹配保护（匹配数为 0 时禁止隐藏）
- 诊断日志 + 查找日志
- 原生 Search API（C++ 引擎层执行，不卡 UI）
- DPI 自适应布局
