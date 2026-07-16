# 傑出品 — Navisworks 查找插件（2023 版）

Navisworks Manage 2023 .NET 插件，用于读取由上游工具生成的 XML 查找条件，或使用手动条件在当前模型范围内定位对象。

## 概述

插件使用 Navisworks 原生 Search API 搜索对象。每条条件独立执行，结果按条件展示，并要求条件与模型对象形成一对一关系；所有已找到和重复条件的对象会合并去重，用于查看和排查。隐藏未选中默认受唯一性校验保护，用户查看问题汇总并明确确认后可以继续，同时仍保留不可绕过的集合与选择安全检查。

## 目标环境

| 项目 | 值 |
|---|---|
| Navisworks | Manage 2023；默认 64 位 Program Files `%ProgramW6432%\Autodesk\Navisworks Manage 2023` |
| 非标准安装 | 设置 `NAVISWORKS_2023_PATH` |
| .NET Framework | 4.8，x64 |
| 开发工具 | MSBuild / Visual Studio 2022 Build Tools |
| 操作系统 | Windows |

项目文件的路径优先级为：显式 MSBuild 属性 `NavisworksInstallDir`、环境变量 `NAVISWORKS_2023_PATH`、64 位 Program Files (`ProgramW6432`) 标准目录、`ProgramFiles` 回退。`build_2023.bat` 依次使用带引号的第一个位置参数、继承的 `NavisworksInstallDir`、`NAVISWORKS_2023_PATH` 和上述默认目录，并把解析后的目录显式传给还原和重建。

## 功能

### 搜索条件

- 支持 `equals`（精确匹配）和 `contains`（包含匹配）。
- 条件由可选分类、属性名、比较方式和查询值组成。
- 每条结果条件同时保留分类和属性的显示标识与内部标识，便于在不同模型和 XML 来源间追溯。
- 缺少分类时，插件在当前模型中采样最多 2000 个对象来发现分类，并缓存本轮结果。
- 用户必须先从选择树确定搜索范围；插件直接把范围根节点交给原生 Search API，不在 .NET 层展开模型后代。
- 多个条件互相独立，最终命中对象按并集去重，因此平铺条件语义是 OR，而不是所有条件同时满足的 AND。

### 唯一性校验与结果可视化

每条查询条件在当前选定模型范围内必须恰好匹配 1 个对象：

- 匹配 0 个：未找到；
- 匹配 1 个且该对象未被前面的条件命中：已找到；
- 匹配 1 个但该对象已被前面的条件命中：后出现的条件标记为重复；
- 单条条件匹配 2 个及以上：重复；
- 条件无法执行：条件异常。

结果页可以筛选全部、问题项、已找到、未找到、重复和条件异常。每条条件都可独立勾选用于导出，点击选择列表头复选框只批量改变当前筛选下的可见条件；切换筛选后已有勾选继续保留并显示总数。导出勾选与 Navisworks 模型选择完全独立。

存在任意问题项时，插件暂停隐藏并显示“仍然继续隐藏 / 返回检查”；继续后保留所有已匹配对象，重复条件的全部匹配对象都会进入保留集合，未找到和条件异常不贡献对象。

双击“重复”结果会在 Navisworks 中临时选中该条件对应的对象，便于定位模型值重复或跨条件重复引用；关闭提示后自动恢复定位前的全部选择，因此可以继续使用“隐藏未选中”，无需重新导入或重新搜索。多条条件指向同一对象时，第一条保留为“已找到”，后续条件标记为“重复”，说明中会给出首条条件序号。

添加、修改、导入、删除或清空条件会立即使旧结果失效。失效后不能导出旧结果、创建选择集或使用主界面的“隐藏未选中”，必须重新搜索。

### STR 保护与隐藏

隐藏模式会根据唯一模型前缀定位 `{前缀}-STR` 节点及其后代，并将其与搜索命中对象合并为最终保留集合。自动隐藏流程和主界面“隐藏未选中”按钮共用同一实现：问题结果必须取得明确覆盖确认，最终保留集合不能为空，宿主实际选择必须与最终保留集合完全一致。STR 缺失仍单独警告，完成后恢复最终保留集合为当前选择。

### 结果复用与诊断

- **导出结果**：底部按钮位置保持不变，可选择导出已勾选、当前筛选或全部有效结果为 CSV/TXT。
- **创建选择集**：把当前有效命中对象保存为 Navisworks 原生 SelectionSet。
- **隐藏未选中**：使用最近一次有效搜索结果独立执行隐藏；仅查找模式完成后也可使用。
- **诊断日志**：选项页显式启用，记录范围、模型前缀、条件、保护、选择和隐藏决策。
- **自动日志**：每次搜索不再生成普通查找日志文件。

## 项目结构

```
NavisworksPlugin.csproj                 # .NET Framework 4.8 x64 项目文件
PluginEntry.cs                          # 插件入口
SearchDialog.cs                         # 主对话框和交互编排
SearchCondition.cs                      # 搜索条件
SearchConditionSnapshot.cs              # 条件快照
SearchConditionValidator.cs             # 条件校验
SearchResult.cs                         # 条件结果
SearchResultPolicy.cs                   # 四态唯一性策略
SearchResultStatus.cs                   # 结果状态
ResultExportPolicy.cs                   # 勾选、筛选与导出范围策略
OneToOneMatchPolicy.cs                  # 跨条件对象一对一占用策略
XmlSearchParser.cs                      # Navisworks exchange XML 解析
ModelItemMatcher.cs                     # 原生 Search.FindAll API 封装
SelectionService.cs                     # 当前选择和 SelectionSet
SelectionEquivalencePolicy.cs           # 最终保留集合与宿主实际选择的纯集合等价策略
HideServiceFixed.cs                     # 隐藏未选中
ProtectedKeepService.cs                 # STR 保护节点查找
LogService.cs                           # 可选诊断日志会话入口
DiagnosticLogSession.cs                 # 诊断日志会话
DiagnosticLogExtensions.cs              # 诊断日志扩展
manifests/傑出品NavisworksPlugin.plugin # 插件清单
build_2023.bat                          # 根目录构建脚本
install_2023.bat                        # 根目录安装入口
scripts/install_2023.ps1               # PowerShell 安装脚本
tests/                                  # 纯逻辑测试
```

## XML 格式

### 无分类条件

```xml
<condition test="equals" flags="74">
  <property>
    <name internal="LcOaSceneBaseUserName">名称</name>
  </property>
  <value><data type="wstring">M14-101</data></value>
</condition>
```

### 有分类条件

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

## 构建

### 前置要求

1. Visual Studio 2022 Build Tools，包含 .NET desktop build tools 和 MSBuild。
2. Navisworks Manage 2023，API DLL 位于其安装根目录。
3. NuGet 会自动还原 `Microsoft.NETFramework.ReferenceAssemblies`。

### 构建命令

在仓库根目录执行：

```batch
build_2023.bat
```

也可把非标准安装目录作为带引号的第一个参数传入：

```batch
build_2023.bat "<Navisworks Manage 2023 安装目录>"
```

非标准安装时，先设置安装目录：

```batch
set "NAVISWORKS_2023_PATH=<Navisworks Manage 2023 安装目录>"
build_2023.bat
```

也可直接调用 MSBuild：

```batch
MSBuild.exe NavisworksPlugin.csproj /p:Configuration=Release /p:NavisworksInstallDir="<Navisworks Manage 2023 安装目录>" /t:Rebuild /v:m
```

构建产物为 `bin\Release\傑出品NavisworksPlugin.dll`。API 引用路径为：

| DLL | 路径 |
|---|---|
| `Autodesk.Navisworks.Api.dll` | `<NavisworksInstallDir>\Autodesk.Navisworks.Api.dll` |
| `navisworks.gui.roamer.dll` | `<NavisworksInstallDir>\navisworks.gui.roamer.dll` |

## 安装

关闭 Navisworks 后，在仓库根目录执行：

```batch
install_2023.bat
```

或直接运行 PowerShell 脚本：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install_2023.ps1
```

默认目标目录为：

```
<NavisworksInstallDir>\Plugins\傑出品NavisworksPlugin\
  傑出品NavisworksPlugin.dll
  傑出品NavisworksPlugin.plugin
```

不要将 DLL 直接放进 `Plugins\` 根目录或 Navisworks 安装根目录。安装后重启 Navisworks。

## 使用

1. 启动 Navisworks Manage 2023 并打开模型。
2. 在选择树中选定一个模型范围。
3. 在 Add-Ins 工具栏点击“傑出品查找”。
4. 导入 XML，或添加并编辑手动条件。
5. 在“选项”中选择仅选中或隐藏模式，并按需启用诊断日志。
6. 执行搜索，在“结果”中查看状态和匹配数量；优先用“问题项”筛选排查。
7. 勾选需要导出的条件，或点击“勾选”表头批量处理当前筛选；底部“导出结果”可导出已勾选、当前筛选或全部结果。
8. 重复结果可双击临时定位；关闭提示后恢复原选择，可直接继续隐藏，无需重新搜索。
9. 问题结果会暂停隐藏；选择“仍然继续隐藏”后保留全部匹配对象并继续安全检查，选择“返回检查”则留在结果页。
10. 有效且有匹配对象的结果会启用主界面“隐藏未选中”，仅查找模式完成后也可单独使用。
11. 一旦添加、修改、导入、删除或清空条件，导出勾选和旧结果会清空，导出、选择集和隐藏按钮立即禁用。

## CSV 与诊断日志

每次搜索不再自动生成普通查找日志。导出菜单提供“已勾选、当前筛选、全部结果”三种明确范围；无勾选时只禁用“导出已勾选”，不会影响另外两种范围。CSV 分别记录 `CategoryDisplay`、`CategoryInternal`、`PropertyDisplay`、`PropertyInternal`，以及比较方式、查询值、状态、匹配数量和说明，并对逗号、双引号和换行进行标准转义。用户在选项页开启诊断日志后，才会额外记录范围、模型前缀、STR 保护、唯一性覆盖选择、请求与实际选择校验和隐藏决策。

## 安全流程

| 场景 | 行为 |
|---|---|
| 无文档、范围或唯一模型前缀 | 中止搜索并给出原因 |
| XML 格式错误或无条件 | 中止搜索并给出原因 |
| 任一条件未找到 | 标记“未找到”，暂停隐藏并要求明确选择 |
| 任一条件匹配 2 个及以上对象 | 标记“重复”，继续时保留该条件的全部匹配对象 |
| 后续条件再次命中已被占用的对象 | 后续条件标记“重复”，说明首条条件序号；对象集合只保留一份 |
| 任一条件无法执行 | 标记“条件异常”，暂停隐藏且该条件不贡献对象 |
| 全部条件唯一匹配 | 标记“已找到”，允许用户确认隐藏 |
| 问题结果选择继续 | 仅绕过唯一性要求，仍校验 STR、最终集合和实际选择 |
| 搜索后编辑条件 | 旧结果和导出勾选清空，导出、选择集和主界面隐藏按钮禁用 |
| 隐藏执行失败 | 报告错误并保留可见选择状态 |

## 常见问题

### 插件按钮没有出现？

确认 DLL 和 `.plugin` 清单位于 `<NavisworksInstallDir>\Plugins\傑出品NavisworksPlugin\`，然后重启 Navisworks。

### 编译找不到 Navisworks API？

确认 `<NavisworksInstallDir>\Autodesk.Navisworks.Api.dll` 存在。非标准安装时设置 `NAVISWORKS_2023_PATH`，或向 MSBuild 传入 `NavisworksInstallDir`。

### 为什么结果是“重复”？

比较方式决定值如何匹配，唯一性校验决定匹配关系是否合法。单条条件命中两个或更多对象会标记为“重复”；多条条件重复指向同一对象时，第一条保留为“已找到”，后续条件也会标记为“重复”。建议先修正条件或模型数据；明确选择继续隐藏时，插件会保留实际命中的对象集合，不会随机取一个。

## 许可

内部工具，不公开发行。
