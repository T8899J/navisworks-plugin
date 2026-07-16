# 傑出品 Navisworks 查找插件

Navisworks Manage 2023 .NET Framework 4.8 插件，读取 XML 查找条件，通过原生 Search API 在三维模型中搜索对象并选中。

## 技术栈

| 项 | 值 |
|---|-----|
| Navisworks | Manage 2023（默认 64 位 Program Files `%ProgramW6432%\Autodesk\Navisworks Manage 2023`；可配置） |
| .NET | Framework 4.8, x64 |
| 语言 | C# 7.3 (WinForms) |
| 构建 | `dotnet build` (MSBuild 17.14) |

## 构建

```powershell
./build_2023.bat
```

## 部署

```powershell
./install_2023.bat
```

路径优先级为：显式 `NavisworksInstallDir`、`NAVISWORKS_2023_PATH`、64 位 Program Files (`ProgramW6432`) 标准目录、`ProgramFiles` 回退。批处理也接受第一个带引号的位置参数作为安装目录。PowerShell 安装入口为 `scripts\install_2023.ps1`；安装后重启 Navisworks 生效。

## 项目结构

```
NavisworksPlugin.csproj    — .NET Framework 4.8 x64
PluginEntry.cs             — AddInPlugin 入口 → 打开 SearchDialog
SearchDialog.cs            — ★ 主对话框 (3 选项卡 + 底部按钮)
XmlSearchParser.cs         — 解析 Navisworks exchange XML
SearchCondition.cs         — 条件 POCO
SearchConditionSnapshot.cs — 条件快照，用于结果失效判定
SearchConditionValidator.cs — 条件有效性校验
SearchResult.cs            — 结果 POCO
SearchResultPolicy.cs      — 四态唯一性结果策略
SearchResultStatus.cs      — 已找到、未找到、重复、条件异常状态
ModelItemMatcher.cs        — 匹配引擎 (原生 Search.FindAll API)
SelectionService.cs        — 选中 + 创建 SelectionSet
HideServiceFixed.cs        — 隐藏未选中 (COM)
LogService.cs              — UTF-8 查找日志
DiagnosticLogSession.cs    — 诊断日志会话
DiagnosticLogExtensions.cs  — 诊断日志扩展方法
ProtectedKeepService.cs    — STR 保护节点查找 (BFS)
```

## 关键架构决策

- **Navisworks 原生 Search API** — `Search.FindAll()` 在 C++ 引擎层执行，不通过 COM 手动遍历（100~1000x 性能差异）
- **SelectionSet 而非自定义集合** — 复用 Navisworks 原生 SelectionSet API，用户已熟悉其操作方式。模式：`new SelectionSet(collection)` + `doc.SelectionSets.AddCopy()` + `selectionSet.Dispose()`
- **STR 节点保护** — BFS 从 RootItem 按 DisplayName 查找 `{prefix}-STR`，命中即停（从遍历 60K 节点降为 3-4 次比较）
- **搜索后选中再隐藏** — 两段式安全设计；每个条件必须恰好匹配 1 个对象，未找到、重复或条件异常任一出现即禁止隐藏

## 禁止事项

- ❌ `SelectionSet.Add()` — API 不存在，用构造函数注入
- ❌ `Document.SelectionSets.Add()` — API 不存在，用 `AddCopy(SavedItem)`
- ❌ 手动 COM 遍历 — 必须用原生 Search API
- ❌ 插件 DLL 放 Navisworks 根目录 → HRESULT:0x80131040
- ❌ 不要 `ExpandItems` / `FilterResultsToScope` / `GetAllDescendants` — 已删除的死代码
