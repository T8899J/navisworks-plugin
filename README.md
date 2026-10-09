# Curi — 傑出品 Navisworks 查找插件（2023 版）

Curi 取意创造（Create）与好奇（Curious）。Navisworks Manage 2023 .NET 插件，用于读取由上游工具生成的 XML 查找条件，或使用手动条件在当前模型范围内定位对象。

## 概述

插件使用 Navisworks 原生 Search API 搜索对象。每条条件独立执行，结果按条件展示，并要求条件与模型对象形成一对一关系；唯一找到项固定计入有效匹配集合，重复项可一键全选或逐条决定是否计入。隐藏未选中默认受唯一性校验保护，用户查看问题汇总并明确确认后可以继续，同时仍保留不可绕过的集合与选择安全检查。

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
- 多个条件互相独立；唯一找到项与用户选择计入的重复项组成有效集合，再按对象去重。平铺条件语义是 OR，而不是所有条件同时满足的 AND。

### 唯一性校验与结果可视化

每条查询条件在当前选定模型范围内必须恰好匹配 1 个对象：

- 匹配 0 个：未找到；
- 匹配 1 个且该对象未被前面的条件命中：已找到；
- 匹配 1 个但该对象已被前面的条件命中：后出现的条件标记为重复；
- 单条条件匹配 2 个及以上：重复；
- 条件无法执行：条件异常。

结果页可以筛选全部、问题项、已找到、未找到、重复和条件异常。“导出”列只控制结果导出；“计入匹配”列控制重复项是否进入匹配对象总数、还原选择、选择集和隐藏保留集合。唯一找到项固定计入，重复项默认不计入。点击“全选重复项”可计入本次搜索的全部重复条件，全部计入后按钮变为“取消全选重复项”；仍可逐条调整。点击“计入匹配”表头只切换当前筛选中的重复项，半选时点击补齐全选；没有重复项时表头禁用。两种选择相互独立并跨筛选保留，隐藏执行后锁定重复项调整，重新搜索后恢复。

存在任意问题项时，插件暂停隐藏并显示“仍然继续隐藏 / 返回检查”；继续后只保留当前计入匹配的对象。未勾选的重复项、未找到和条件异常不贡献对象，重复状态仍然保留且不会绕过唯一性确认。

双击“重复”结果会在 Navisworks 中选中该条件对应的对象并将相机聚焦到选择，便于定位模型值重复或跨条件重复引用。查看完成后点击结果页的“还原全部选择”即可恢复本次搜索选中的全部对象；即使在问题弹窗中选择“返回检查”，也无需重新导入或重新搜索。多条条件指向同一对象时，第一条保留为“已找到”，后续条件标记为“重复”，说明中会给出首条条件序号。

添加、修改、导入、删除或清空条件会立即使旧结果失效。失效后不能导出旧结果、创建选择集或使用主界面的“隐藏未选中”，必须重新搜索。

### 桥架测量

点击查找窗口页头 **桥架测量 ▾**，选择 **便捷测量** 或 **批量测量**。窗口沿用 Curi 的主题、按钮反馈和窗口淡入；新增功能随现有插件 DLL 一起构建和安装。

**便捷测量**保留单构件实验的小窗口：构件名、大号长度（m）、毫米换算和一个“测量选中构件”按钮。在模型中选好一段桥架后点击按钮，一次只测一个；未选择或多选时直接提示，不进行批量求和。换选后再点击按钮即可测下一段，无需导入或搜索。小窗口独立于查找及批量窗口，关闭大窗口后仍可继续使用；也注册了 Add-Ins **桥架便捷测量** 独立入口。

**批量测量**继续提供以下两种来源，并使用原有的清单和内容切换动效：

- **批量导入查找**：沿用已导入 XML 或手动条件产生的有效查找结果，点击 **开始批量测量**。还未查找时可点击 **导入 / 查找** 返回主窗口准备条件；查找后用 **读取查找结果** 更新测量清单。
- **手动点选构件**：无需导入条件。在模型视图或选择树里选择完整桥架，可按 Ctrl 多选，点击 **测量当前选择** 即可。窗口显示当前选择数，隐藏无关的查找编号列；切换选择不会改变已测结果，再次点击测量才读取新的选择。

- 批量模式的测量对象直接来自查找结果的有效集合：唯一找到项固定计入，重复项需先在查找结果中明确计入，同一对象只测一次。隐藏流程额外保留的 STR 节点不会自动进入测量。手动模式只使用点击测量时的模型选择快照，并对对象去重。
- 窗口集中显示直桥架总长、逐件长度（m）和状态；支持编号筛选、待复核筛选、双击定位、停止与重新测量、导出全部测量清单 CSV。停止后保留已测部分，并明确标记未测构件。
- 复用单构件实验的实例 Fragment 过滤、世界坐标变换、单位换算、面积加权主轴与截面检查。弯头、三通、变径、无有效几何或父子节点重叠的对象标为待复核，长度留空，不计入总长。
- 修改查找条件、重新搜索或改变重复项计入状态，只使批量查找来源的测量结果失效，不影响手动来源。模型更新、关闭或切换会使两种来源均失效。切换测量方式会清空旧清单；测量期间先停止才能切换，两种来源不会混合计长。CSV 记录测量来源。
- 当前测量是直桥架表面几何的纵向长度候选，不包含配件中心线路径长度。实际项目精度、界面与交互由用户在 Navisworks 中验收。

### STR 保护与隐藏

隐藏模式会根据唯一模型前缀定位 `{前缀}-STR` 节点及其后代，并将其与当前有效匹配对象合并为最终保留集合。自动隐藏流程和主界面“隐藏未选中”按钮共用同一实现：问题结果必须取得明确覆盖确认，最终保留集合不能为空，宿主实际选择必须与最终保留集合完全一致。STR 缺失仍单独警告，完成后恢复最终保留集合为当前选择。

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
SearchDialog.Measurement.cs             # 查找有效集合与测量窗口连接
TrayMeasurement/                       # 独立批量测量界面、几何提取和汇总策略
SearchCondition.cs                      # 搜索条件
SearchConditionSnapshot.cs              # 条件快照
SearchConditionValidator.cs             # 条件校验
SearchResult.cs                         # 条件结果
SearchResultPolicy.cs                   # 四态唯一性策略
SearchResultStatus.cs                   # 结果状态
ResultExportPolicy.cs                   # 勾选、筛选与导出范围策略
DuplicateMatchInclusionPolicy.cs        # 重复项贡献与对象级去重策略
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

在 Add-Ins 工具栏点击“傑出品查找”打开 Curi 窗口，四步完成查找：

1. **在选择树中选中要搜索的模型节点**：通常是 `TS-M` 开头的根节点。插件只在选中的范围内查找，不选会被拒绝。
2. **准备条件**：点击“导入 XML”或“＋ 添加”，双击任一行可修改。属性名必须与 Navisworks“属性”窗口中的写法完全一致。
3. **点击“执行搜索”**：默认只选中匹配对象，不做隐藏；完成后自动切到“结果”页。
4. **在“结果”页检查，再选择下一步**：先处理“问题项”（未找到、重复、条件异常），确认无误后用“创建选择集”保存结果、“隐藏未选中”只留匹配对象，或“导出结果”输出清单。

遇到问题：

| 情况 | 解决 |
|---|---|
| 什么都没找到 | 核对属性名的空格与大小写，以及搜索范围是否选对 |
| 结果显示重复 | 一个条件命中了多个对象；收窄查询值，或勾选“计入匹配” |
| equals 还是 contains | 编号、名称用 equals；路径、描述里的关键词用 contains |
| 隐藏错了 | 在 Navisworks“常用”选项卡点击“全部显示”即可恢复 |

重复项的批量计入、表头勾选、双击定位与“还原全部选择”等细节见上方[唯一性校验与结果可视化](#唯一性校验与结果可视化)。

## CSV 与诊断日志

每次搜索不再自动生成普通查找日志。导出菜单提供“已勾选、当前筛选、全部结果”三种明确范围；无勾选时只禁用“导出已勾选”，不会影响另外两种范围。CSV 记录“计入总匹配”、`CategoryDisplay`、`CategoryInternal`、`PropertyDisplay`、`PropertyInternal`，以及比较方式、查询值、状态、匹配数量和说明，并对逗号、双引号和换行进行标准转义。用户在选项页开启诊断日志后，才会额外记录范围、模型前缀、STR 保护、唯一性覆盖选择、请求与实际选择校验和隐藏决策。

## 安全流程

| 场景 | 行为 |
|---|---|
| 无文档、范围或唯一模型前缀 | 中止搜索并给出原因 |
| XML 格式错误或无条件 | 中止搜索并给出原因 |
| 任一条件未找到 | 标记“未找到”，暂停隐藏并要求明确选择 |
| 任一条件匹配 2 个及以上对象 | 标记“重复”，默认不计入；用户勾选后整条条件的匹配对象共同计入 |
| 后续条件再次命中已被占用的对象 | 后续条件标记“重复”；即使不计入，对象仍可通过首条唯一条件保留 |
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

比较方式决定值如何匹配，唯一性校验决定匹配关系是否合法。单条条件命中两个或更多对象会标记为“重复”；多条条件重复指向同一对象时，第一条保留为“已找到”，后续条件也会标记为“重复”。重复项默认不计入有效集合，用户可以一键全选或逐条选择是否整组计入；系统始终按模型对象去重，不会随机取一个。

## 许可

内部工具，不公开发行。
# 2023 独立安装包

分发包位于 `dist/JiePinPai-Navisworks-2023-*.zip`。完整解压后双击 `安装.bat`，在窗口中确认自动发现的 Navisworks Manage 2023 目录，或浏览选择 `Roamer.exe` / 桌面快捷方式。关闭目标 Navisworks，确认插件来源后安装，并按 Windows 提示授予管理员权限。另附 `验证.bat`、`卸载.bat` 和 `使用说明.txt`，目标电脑无需开发工具或 Agent。

开发者重新打包：`powershell -NoProfile -ExecutionPolicy Bypass -File .\package_2023.ps1 -Snapshot`。可传 `-NavisworksPath "实际安装目录"`；不传时自动发现。存在多个有效目录时需明确选择。默认正式打包拒绝未提交的交付输入，`-Snapshot` 会在清单中记录当前 Git 状态及源码、安装脚本、编译引用的哈希；程序集版本沿用 `Properties/AssemblyInfo.cs`。

该包仅支持 Manage 2023 x64 / API 20.x。安装前校验完整性与宿主版本，安装失败时恢复旧文件，且不改 `Roamer.exe.config`。文件验证与隔离安装测试不能替代目标电脑的实际模型功能验收。

仅在分发包中禁用桥架批量测量：`powershell -NoProfile -ExecutionPolicy Bypass -File .\package_2023.ps1 -Snapshot -DisableBatchMeasurement`。生成文件名含 `no-batch` 的 ZIP，点击“批量测量”只提示“当前功能暂不可用”；便捷测量和 Add-Ins“桥架便捷测量”快捷入口保留。专用 DLL 输出到 `bin/Release-NoBatch/`，构建开关与功能状态写入包清单并与 DLL 核对，包内使用说明同步为此版本。
