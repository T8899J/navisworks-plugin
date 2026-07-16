# 傑出品 — Navisworks 查找插件（2023 版）

Navisworks Manage 2023 .NET 插件，用于读取由上游工具生成的 XML 查找条件，或使用手动条件在当前模型范围内定位对象。

## 概述

插件使用 Navisworks 原生 Search API 搜索对象。每条条件独立执行，结果按条件展示；所有已找到和重复条件的对象会合并去重，用于查看和排查。隐藏未选中是受严格安全门禁保护的可选操作。

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
- 缺少分类时，插件在当前模型中采样最多 2000 个对象来发现分类，并缓存本轮结果。
- 用户必须先从选择树确定搜索范围；插件直接把范围根节点交给原生 Search API，不在 .NET 层展开模型后代。
- 多个条件互相独立，最终命中对象按并集去重，因此平铺条件语义是 OR，而不是所有条件同时满足的 AND。

### 唯一性校验与结果可视化

每条查询条件在当前选定模型范围内必须恰好匹配 1 个对象：

- 匹配 0 个：未找到；
- 匹配 1 个：已找到；
- 匹配 2 个及以上：重复；
- 条件无法执行：条件异常。

结果页可以筛选全部、问题项、已找到、未找到、重复和条件异常。存在任意问题项时，插件严格阻止“隐藏未选中”；处理条件或模型数据并重新搜索，直到全部条件唯一匹配后才允许隐藏。

双击“重复”结果会在 Navisworks 中选中该条件命中的全部对象，便于定位模型中的重复值。两条相同的输入条件若各自只匹配同一对象，二者仍都是“已找到”，不构成模型值重复。

添加、修改、导入、删除或清空条件会立即使旧结果失效。失效后不能导出旧结果、创建选择集或继续使用旧的隐藏确认，必须重新搜索。

### STR 保护与隐藏

隐藏模式会根据唯一模型前缀定位 `{前缀}-STR` 节点及其后代，并将其与搜索命中对象合并为最终保留集合。只有所有条件均为“已找到”，且范围、模型前缀、最终保留集合、当前选择和用户确认都有效时，才执行隐藏未选中；完成后恢复最终保留集合为当前选择。

### 结果复用与诊断

- **导出结果**：导出当前有效结果为 CSV 或 TXT。
- **创建选择集**：把当前有效命中对象保存为 Navisworks 原生 SelectionSet。
- **普通日志**：仅在本次搜索存在源 XML 路径时写入该 XML 同级目录；全手动会话没有源路径，不写普通日志。
- **诊断日志**：选项页显式启用，记录范围、模型前缀、条件、保护、选择和隐藏决策。

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
XmlSearchParser.cs                      # Navisworks exchange XML 解析
ModelItemMatcher.cs                     # 原生 Search.FindAll API 封装
SelectionService.cs                     # 当前选择和 SelectionSet
HideServiceFixed.cs                     # 隐藏未选中
ProtectedKeepService.cs                 # STR 保护节点查找
LogService.cs                           # 普通查找日志
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
7. 重复结果可双击选中全部重复对象；修正条件或模型数据后重新搜索。
8. 所有条件唯一匹配时，隐藏模式才会提供隐藏确认。
9. 有效结果可导出或创建选择集；一旦添加、修改、导入、删除或清空条件，这些旧结果操作立即禁用。

## CSV 与日志

仅当本次搜索有源 XML 路径时，普通日志才写在 XML 同级目录；全手动会话没有源 XML 路径，因此不写普通日志。文件名格式为：

```
<xml文件名>_查找日志_YYYYMMDD_HHmmss.txt
```

CSV 和普通日志均记录完整条件（分类、属性、比较方式、查询值）、状态、匹配数量和说明。CSV 对逗号、双引号和换行进行标准转义，因此查询值包含这些字符时仍可保持列完整。用户开启诊断日志后，额外记录范围、模型前缀、STR 保护、选择写入和隐藏决策。

示例：

```
===== 傑出品 Navisworks 查找日志 =====
条件: SmartPlant 3D / System Path / contains / P-001
状态: 重复
匹配数量: 3
说明: 当前范围内匹配多个对象；隐藏未选中已阻止。

条件: SmartPlant 3D / System Path / equals / P-002
状态: 已找到
匹配数量: 1
```

## 安全流程

| 场景 | 行为 |
|---|---|
| 无文档、范围或唯一模型前缀 | 中止搜索并给出原因 |
| XML 格式错误或无条件 | 中止搜索并给出原因 |
| 任一条件未找到 | 标记“未找到”，严格阻止隐藏 |
| 任一条件匹配 2 个及以上对象 | 标记“重复”，严格阻止隐藏 |
| 任一条件无法执行 | 标记“条件异常”，严格阻止隐藏 |
| 全部条件唯一匹配 | 标记“已找到”，允许用户确认隐藏 |
| 搜索后编辑条件 | 旧结果失效，导出、选择集和隐藏相关操作禁用 |
| 隐藏执行失败 | 报告错误并保留可见选择状态 |
| 日志写入失败 | 报告警告，不改变已完成的搜索结果 |

## 常见问题

### 插件按钮没有出现？

确认 DLL 和 `.plugin` 清单位于 `<NavisworksInstallDir>\Plugins\傑出品NavisworksPlugin\`，然后重启 Navisworks。

### 编译找不到 Navisworks API？

确认 `<NavisworksInstallDir>\Autodesk.Navisworks.Api.dll` 存在。非标准安装时设置 `NAVISWORKS_2023_PATH`，或向 MSBuild 传入 `NavisworksInstallDir`。

### 为什么结果是“重复”？

比较方式决定值如何匹配，唯一性校验决定匹配数量是否合法。即使比较方式本身正确，当前范围内命中两个或更多对象仍是“重复”，必须修正条件或模型数据后才能隐藏。

## 许可

内部工具，不公开发行。
