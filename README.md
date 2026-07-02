# 傑出品 — Navisworks 查找插件（2023 版）

Navisworks Manage 2023 .NET 插件，用于自动执行由「傑出品」Python 工具生成的 XML 查找操作。

---

## 概述

「傑出品」是两件套工具链：

| 组件 | 位置 | 技术栈 | 职责 |
|------|------|--------|------|
| **XML 生成器** | `sqt_tool.py` | Python + Tkinter | 输入查询值，生成 Navisworks exchange XML |
| **查找插件** (本组件) | `navisworks-plugin/` | C# .NET Framework 4.8 | 读取 XML，在 Navisworks 中执行查找并选中 |

**工作流程：**

```
Python 工具生成 XML  →  用户在 Navisworks 打开模型
  →  点击插件按钮「傑出品查找」
  →  选择 .xml 文件
  →  插件解析 XML 条件 → 原生 Search API 匹配 → 选中匹配对象
  →  弹窗报告结果（条件数 / 找到数 / 未找到数 / 总匹配对象数）
  可选： → 仅搜索选中对象（如果选择树中有预选）
  →  用户确认后 → 隐藏未选中
  →  输出日志
```

---

## 目标环境

| 项目 | 值 |
|------|-----|
| Navisworks | **Manage 2023**（安装在 `F:\Navisworks\Navisworks Manage 2023\`） |
| .NET Framework | **4.8** |
| 开发工具 | **命令行编译**（MSBuild 17.14 / VS 2022 Build Tools） |
| 操作系统 | Windows 11（22H2+, 已内置 .NET 4.8.1） |

> 只适配 Navisworks Manage 2023，不做多版本兼容。

---

## 功能

### 核心流程（两段式安全设计）

**第一步：读取 → 查找 → 选中 → 报告**
- 点击按钮，选择 XML 文件
- 解析 XML 中的 `findspec / conditions / condition`
- 支持 `equals`（精确匹配）和 `contains`（包含匹配）
- 可选 `<category>` 限定搜索范围；无 `<category>` 时通过模型采样自动发现所属分类
- 使用 **Navisworks 原生 Search API**（C++ 引擎层执行，100~1000 倍于手动 COM 遍历）
- 所有匹配到的 `ModelItem` 合并为当前选择
- 弹窗报告：条件总数、找到数、未找到数、总匹配数

**第二步（用户确认后）：隐藏未选中**
- 用户查看报告后选择是否隐藏未选中
- 通过 `LcOwDocument.InvertSelection()` + `SetSelectionHidden(ModelItemCollection, true)` 实现
- 匹配数为 0 时**禁止**执行隐藏未选中
- 隐藏后恢复已匹配项为选中状态

### 搜索范围选择（2.0 新增）
- 如果选择树中有预选对象，弹窗询问是否仅搜索选中项（含子项）
- 选「是」→ 只搜索选中对象及其子项
- 选「否」→ 搜索整个模型

### 用户体验
- 等待光标（匹配过程中显示沙漏）
- 文件选择对话框强制置顶（不会被 Navisworks 主窗口挡住）
- 全面的异常处理（文档未打开、XML 格式错误、无匹配等场景均有中文提示）
- **不卡死** — Search API 内部处理消息泵，Navisworks 保持响应

---

## 项目结构

```
navisworks-plugin/
├── NavisworksPlugin.csproj         # .NET Framework 4.8 x64 项目文件
├── Properties/
│   └── AssemblyInfo.cs              # 程序集信息
├── PluginEntry.cs                   # 插件入口（AddInPlugin）→ 打开 SearchDialog
├── SearchDialog.cs                  # ★ 主对话框（3 标签页 + 工具栏 + DataGridView + 搜索执行）
├── XmlSearchParser.cs               # 解析 Navisworks exchange XML → List<SearchCondition>
├── SearchCondition.cs               # 搜索条件 POCO
├── SearchResult.cs                  # 搜索结果 POCO
├── ModelItemMatcher.cs              # 匹配引擎（Navisworks 原生 Search.FindAll API 封装）
├── SelectionService.cs              # 设置 doc.CurrentSelection
├── HideService.cs                   # Hide Unselected（LcOwDocument COM）
├── LogService.cs                    # UTF-8 查找日志
├── 傑出品NavisworksPlugin.plugin   # XML 清单（插件注册 + 自定义选项卡）
├── build_2023.bat                   # MSBuild 编译脚本
├── install_2023.bat                 # 部署脚本
├── test_search.xml                 # 测试用搜索条件 XML
├── .index                          # 代码索引
└── README.md
```

### 各文件职责

| 文件 | 职责 |
|------|------|
| `PluginEntry.cs` | 插件入口点。继承 `AddInPlugin`，`[Plugin]` + `[AddInPlugin]` 属性注册。`Execute()` 启动 SearchDialog |
| `SearchDialog.cs` | **主对话框**。3 个选项卡：搜索条件、选项、结果。工具栏支持导入/导出 XML、添加/删除/清空搜索条件。内置条件编辑器（双击 DataGridView 行编辑）。底部按钮：执行搜索、导出结果、关闭。搜索流程：范围询问 → 匹配 → 选中 → 结果报告 → 用户确认 → 隐藏未选中 → 日志 |
| `XmlSearchParser.cs` | 用 `XDocument` 解析 Navisworks exchange XML，提取 `<condition>` 列表。支持 category、property、value、test 属性解析 |
| `ModelItemMatcher.cs` | **核心匹配引擎**。使用 Navisworks 原生 `Search.FindAll()` API（C++ 引擎层执行，自带消息泵）。无 `<category>` 时通过模型采样自动发现属性所属分类。提供全模型搜索和指定范围搜索两个重载 |
| `SearchCondition.cs` | 条件数据模型：`CategoryInternal`, `CategoryDisplay`, `PropertyInternal`, `PropertyDisplay`, `Test`, `Value` |
| `SearchResult.cs` | 匹配结果数据模型：`QueryValue`, `MatchCount`, `MatchedItems` |
| `SelectionService.cs` | 将 `List<ModelItem>` 合并为 `ModelItemCollection`，设置为 `doc.CurrentSelection` |
| `HideService.cs` | 执行 Hide Unselected：`InvertSelection()` → `SetSelectionHidden(collection, true)` → 恢复已匹配为选中。0 匹配保护 |
| `LogService.cs` | 写 UTF-8 日志到 XML 同级目录，记录每个条件的匹配数和汇总统计 |
| `傑出品NavisworksPlugin.plugin` | XML 清单，定义自定义选项卡和按钮在 Navisworks 中的布局 |

---

## XML 格式支持

### 支架查询（无 category）

```xml
<condition test="equals" flags="74">
  <property>
    <name internal="LcOaSceneBaseUserName">名称</name>
  </property>
  <value><data type="wstring">M14-101</data></value>
</condition>
```

插件行为：模型采样发现 `LcOaSceneBaseUserName`（显示名"名称"）所属的 category，再用 `HasPropertyByDisplayName(cat, "名称").EqualValue(value)` 精确匹配。

### 坐标查询（有 category）

```xml
<condition test="contains" flags="74">
  <category>
    <name internal="SP3D">SmartPlant 3D</name>
  </category>
  <property>
    <name internal="System Path">System Path</name>
  </property>
  <value><data type="wstring">P-001</data></value>
</condition>
```

插件行为：在 **SP3D (SmartPlant 3D)** category 内查找 `System Path` 属性，值包含 `P-001`。

---

## 数据模型

### SearchCondition

```csharp
public class SearchCondition
{
    public string? CategoryInternal { get; init; }  // category/name internal 属性（可为 null）
    public string? CategoryDisplay { get; init; }   // category/name 显示文本（可为 null）
    public string PropertyInternal { get; init; }   // property/name internal 属性
    public string PropertyDisplay { get; init; }    // property/name 显示文本
    public string Test { get; init; }               // 匹配方式：equals / contains
    public string Value { get; init; }              // 查询值
}
```

### SearchResult

```csharp
public class SearchResult
{
    public string QueryValue { get; init; }                           // 查询值
    public int MatchCount { get; init; }                              // 匹配到的 ModelItem 数量
    public IReadOnlyList<ModelItem> MatchedItems { get; init; }       // 匹配到的 ModelItem 列表
}
```

---

## 编译

### 前置要求

1. **Visual Studio 2022 Build Tools**
   - 安装于 `D:\Apps\Microsoft Visual Studio\2022\BuildTools\`
   - 需包含 .NET desktop build tools 工作负载

2. **Navisworks Manage 2023**
   - 安装于 `F:\Navisworks\Navisworks Manage 2023\`
   - 编译时需要引用其 API DLL

3. **NuGet 包**（自动还原）
   - `Microsoft.NETFramework.ReferenceAssemblies` v1.0.3（无需本地安装 .NET 4.8 Developer Pack）

### 编译命令

```batch
navisworks-plugin\build_2023.bat
```

或直接调用 MSBuild：

```batch
MSBuild.exe NavisworksPlugin.csproj /p:Configuration=Release /t:Rebuild /v:m
```

编译成功后在 `bin\Release\` 生成 `傑出品NavisworksPlugin.dll`。

### API 引用

| DLL | 路径 |
|-----|------|
| `Autodesk.Navisworks.Api.dll` | `F:\Navisworks\Navisworks Manage 2023\Autodesk.Navisworks.Api.dll` |
| `navisworks.gui.roamer.dll` | `F:\Navisworks\Navisworks Manage 2023\navisworks.gui.roamer.dll` |

---

## 安装

### 正确安装路径

Navisworks 2023 要求插件放在 **同名子文件夹** 中：

```
F:\Navisworks\Navisworks Manage 2023\Plugins\
└── 傑出品NavisworksPlugin\
    ├── 傑出品NavisworksPlugin.dll
    └── 傑出品NavisworksPlugin.plugin
```

### 一键安装

```batch
navisworks-plugin\install_2023.bat
```

### 手动安装

```batch
copy bin\Release\傑出品NavisworksPlugin.dll "F:\Navisworks\Navisworks Manage 2023\Plugins\傑出品NavisworksPlugin\"
copy 傑出品NavisworksPlugin.plugin "F:\Navisworks\Navisworks Manage 2023\Plugins\傑出品NavisworksPlugin\"
```

### 注意事项

- **不要** 在 Navisworks 安装根目录放置 `.plugin.dll` 文件（会导致程序集名称不匹配错误 HRESULT:0x80131040）
- **不要** 使用 `%APPDATA%\Autodesk\Navisworks\Plugins\2023\` 路径（该目录不存在）
- `install_2023.bat` 包含中文字符，若保存为 UTF-8 编码会导致 cmd.exe 解析失败。如遇安装脚本报错，请手动复制文件（见上方命令）

---

## 使用

1. 启动 Navisworks Manage 2023
2. 打开模型（`.nwd` / `.nwf` / `.nwc`）
3. 在 Add-Ins 工具栏点击 **「傑出品查找」** 按钮
4. 弹出 **傑出品主对话框**，包含 3 个选项卡：
   - **搜索条件** — 导入 XML / 手动添加条件 / 编辑 / 删除 / 清空
   - **选项** — 搜索后隐藏 / 日志生成开关
   - **结果** — 搜索完成后显示匹配汇总和详情
5. 点击 **导入** 选择 `.xml` 文件，或手动 **添加** 条件（双击行编辑）
6. 点击 **执行搜索**
7. 如有预选对象，弹窗询问搜索范围
8. 结果显示在「结果」选项卡，询问是否隐藏未选中
9. 日志自动保存到 XML 同级目录

---

## 日志

每次查找操作后，在 XML 文件同级目录生成日志文件：

```
<xml文件名>_查找日志_YYYYMMDD_HHmmss.txt
```

格式示例：

```
===== 傑出品 Navisworks 查找日志 =====
XML 文件: D:\data\支架.xml
查找时间: 2026-06-29 15:30:00
条件数: 5

--- 查询结果 ---
[找到] M14-101 → 匹配 3 个对象
[找到] M14-102 → 匹配 1 个对象
[未找到] M14-103 → 没有匹配的对象
[找到] M14-104 → 匹配 2 个对象
[未找到] M14-105 → 没有匹配的对象

--- 汇总 ---
总条件数: 5
匹配成功: 3
未找到: 2
总计匹配对象数: 6
```

---

## 安全流程

| 场景 | 行为 |
|------|------|
| 无文档打开 | 弹窗提示"请先打开一个 Navisworks 文档"，中止 |
| XML 文件格式错误 | 弹窗提示"XML 解析失败：{具体错误}"，中止 |
| XML 中无 `<condition>` | 弹窗提示"XML 中未找到查询条件"，中止 |
| 所有值均未匹配 | 匹配数为 0 → 弹窗报告 → **禁止隐藏未选中** |
| 部分匹配 | 正常选中，弹窗报告，询问用户是否隐藏 |
| 全部匹配 | 正常选中，弹窗报告，询问用户是否隐藏 |
| 隐藏未选中失败 | 弹窗报错，选中状态保留 |
| 日志写入失败 | 弹窗警告但不中止操作 |

---

## 边界情况与性能

| 场景 | 处理方式 |
|------|----------|
| property 匹配 | 优先 display name，回退到 internal 名称 |
| 无 category 条件 | 模型采样发现分类（扫描前 200 个元素），再用 `HasPropertyByDisplayName` |
| 有 category 条件 | 直接使用 `HasPropertyByDisplayName(category, property)` |
| 大模型 | 原生 Search API（C++ 引擎层执行），避免数千万次 COM 跨边界调用 |
| 多个条件 | 每个条件独立执行一次 `Search.FindAll()`，原生引擎每次仅需毫秒到秒级 |
| 文件选择对话框被挡住 | 使用 `Process.MainWindowHandle` 作为所有者，强制置顶 |
| Navisworks 响应性 | Search API 内部 `reportProgress: true` 自动泵消息，不卡 UI |

---

## 常见问题

### Q: 插件按钮没出现在 Navisworks 中？

A: 检查 DLL 是否在正确的路径：
```
F:\Navisworks\Navisworks Manage 2023\Plugins\傑出品NavisworksPlugin\傑出品NavisworksPlugin.dll
```
同时检查 `.plugin` 清单文件是否存在。确认后重启 Navisworks。

### Q: 之前装过但没显示，怎么办？

A: Navisworks 2023 常用的用户级插件目录不存在。务必使用 `F:\Navisworks\Navisworks Manage 2023\Plugins\` 下的子文件夹结构。**不要将 DLL 直接放在 `Plugins\` 根目录**。

### Q: 为什么不能把 `.plugin.dll` 放到 Navisworks 根目录？

A: 内置插件（如 `Navisworks.Clash.Plugin.dll`）的**程序集名称**包含 `.Plugin` 后缀。我们的程序集名是 `傑出品NavisworksPlugin`（不含 `.plugin`），放在根目录会导致 `HRESULT:0x80131040`（程序集清单不匹配）错误。

### Q: 匹配不到任何对象？

A: 检查以下几点：
1. **无 category 的 XML** — 插件会自动采样模型发现分类。若采样失败（空模型或属性名不匹配），检查 `property internal` 值是否与模型中一致
2. **有 category 的 XML** — 确认 `<category>` 和 `<property>` 的 display name 与 Navisworks 属性面板中的显示一致
3. 可以用 Navisworks 的「选择树」查看目标对象的属性面板，确认属性名和分类名

### Q: 点击按钮后 Navisworks 卡住（未响应）？

A: 旧版本存在此问题（手动 COM 遍历导致 UI 线程阻塞）。当前版本已使用 Navisworks 原生 Search API 替代手动遍历，搜索在 C++ 引擎层执行，内部处理消息泵，**不会导致 Navisworks 卡死**。如果仍遇到卡死，检查是否有文件选择对话框被 Navisworks 主窗口挡住（按 `Alt+Tab` 切换）。

### Q: 编译报错？

A: 确认 `F:\Navisworks\Navisworks Manage 2023\Autodesk.Navisworks.Api.dll` 存在，MSBuild 路径正确。运行 `build_2023.bat` 会自动检查这些条件。

---

## 与 Python 工具的兼容性

- 本插件与 `sqt_tool.py` **独立演进**
- Python 工具负责 XML 生成（**不修改**）
- 本插件负责 XML 消费
- 数据格式通过 `<condition>` schema 约定

当前约定的 schema：

| 元素 | 支架 XML | 坐标 XML |
|------|----------|----------|
| category | 无 | `internal="SP3D"` / 显示名 "SmartPlant 3D" |
| property | `internal="LcOaSceneBaseUserName"` / 显示名 "名称" | `internal="System Path"` / 显示名 "System Path" |
| test | `equals` | `contains` |

---

## 路线图

| 阶段 | 内容 | 状态 |
|------|------|------|
| **第一版** | 读取 XML → 查找 → 选中 → 弹窗报告 → 用户确认后隐藏 → 日志 | ✅ 已完成 |
| **第二版** | 搜索范围选择（仅搜索选中对象） | ✅ 已完成 |
| **第三版** | 性能优化（原生 Search API 替代手动 COM 遍历） | ✅ 已完成 |
| **第四版** | Navisworks 2023 兼容适配（SetSelectionHidden、无分类 XML 自动发现） | ✅ 已完成 |
| **第五版** | GUI 重构（3 标签页对话框 + 工具栏 + DataGridView + 条件编辑器） | ✅ 已完成 |
| **第六版** | 外观优化（Navisworks 风格 UI、DPI 自适应） | 🔧 进行中 |
| **第七版** | 批量处理多个 XML 文件 | ⏳ 计划中 |

---

## 许可

内部工具，不公开发行。
