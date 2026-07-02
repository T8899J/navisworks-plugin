# 更新日志

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
