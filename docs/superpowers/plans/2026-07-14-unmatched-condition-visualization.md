# 未命中条件可视化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (\`- [ ]\`) syntax for tracking.

**Goal:** 在搜索结果中清晰展示当前搜索范围内未命中的查询条件，并让用户看见条件上下文、失败类型和后续处理方向。

**Architecture:** 保持 Navisworks 搜索、当前选择、STR 保护和隐藏流程不变。搜索结果从仅保存“查询值和数量”扩展为不可变的条件快照和结果状态；结果页用结构化表格显示未命中条件，再保留全部条件明细作为完整审计视图。

**Tech Stack:** C# 7.3、.NET Framework 4.8、WinForms、Navisworks Manage 2023 API、MSTest（新增纯逻辑测试项目）。

## Global Constraints

- 目标运行环境保持 Navisworks Manage 2023、.NET Framework 4.8、x64。
- “未命中”必须表述为“当前选择范围内未命中”，不得暗示整个模型或业务数据源中不存在该对象。
- 不能选中、定位或高亮一个不存在的 ModelItem；可视化对象是查询条件，不是三维模型对象。
- 区分“已执行但命中数为 0”和“条件无法构建/执行”。
- 保持现有多条件并集、STR 保护、零命中禁止隐藏、创建选择集和导出流程语义不变。
- 所有新增 WinForms 布局使用 \`TableLayoutPanel\`、\`Dock\`、既有 DPI 缩放辅助方法和现有颜色体系；不得使用绝对坐标。
- 在通过 Navisworks 手工验收前，不部署到用户插件目录；每个任务仅在其自动化检查通过后提交明确列出的文件。

---

## 产品判定

本计划采用以下产品定义：

| 用户看到的状态 | 业务含义 | 展示位置 |
|---|---|---|
| 已命中 | 条件已运行，至少找到一个范围内对象 | 全部条件明细 |
| 未命中 | 条件已运行，但当前范围内没有对象满足它 | 未命中条件区 + 全部条件明细 |
| 无法执行 | 例如无分类条件未能发现属性所属分类 | 未命中条件区 + 原因列 |
| 搜索未开始 | 条件为空、范围为空、模型前缀无效等前置校验失败 | 现有提示框；不得显示为“未命中” |

推荐结果页布局：

1. 顶部摘要保留总条件、已命中、未命中、无法执行和去重对象数。
2. 摘要下方固定显示“未命中条件（N）”表格；存在项目时使用浅红提示色和文字状态，项目为 0 时显示“本次搜索范围内所有条件均已命中”。
3. 下方显示“全部条件明细”表格，列为状态、分类、属性、匹配方式、查询值、命中对象数、说明。
4. 不新增饼图、柱状图或三维高亮。此处需要的是能定位并修正的条件信息，不是统计装饰。

### Task 1: 确认用户语义并冻结验收标准

**Files:**

- Modify: \`docs/superpowers/plans/2026-07-14-unmatched-condition-visualization.md\`

**Interfaces:**

- Consumes: 当前 \`SearchCondition\` 和 \`SearchResult\` 的一一对应关系。
- Produces: 已确认的“未命中条件”定义和验收数据集。

- [ ] **Step 1: 向用户确认两个边界问题**

确认以下文字与用户意图一致：

\`\`\`text
未找到 = 当前选择范围内，某条查询条件没有命中任何 ModelItem。
界面显示的是未命中的查询条件及原因，而不是在三维视图中高亮不存在的对象。
\`\`\`

同时询问用户是否需要把未命中条件同步导出到 CSV；默认将其包含在现有导出，因为它属于同一次结果。

- [ ] **Step 2: 准备 Navisworks 验收数据**

准备三条条件：一条已命中；一条属性存在但值不存在；一条不提供分类且无法从当前范围发现分类。记录每条的分类、属性、匹配方式和查询值。

- [ ] **Step 3: 写下验收预期**

\`\`\`text
已命中：显示命中数大于 0，状态为“已命中”。
值不存在：显示“未命中”，说明为“当前搜索范围内没有匹配对象”。
分类无法发现：显示“无法执行”，说明为“无法确定属性所属分类”。
范围为空：只显示范围校验提示，不更新为“未命中”结果。
\`\`\`

### Task 2: 建立不可变的条件结果快照与状态模型

**Files:**

- Create: \`SearchResultStatus.cs\`
- Modify: \`SearchResult.cs\`
- Modify: \`ModelItemMatcher.cs\`
- Modify: \`NavisworksPlugin.csproj\`
- Create: \`tests/NavisworksPlugin.Tests/NavisworksPlugin.Tests.csproj\`
- Create: \`tests/NavisworksPlugin.Tests/SearchResultTests.cs\`

**Interfaces:**

- Consumes: \`SearchCondition\`、原生 Search API 的匹配集合。
- Produces: \`SearchResult.Status\`、\`SearchResult.StatusMessage\` 和搜索发生时的条件字段快照。

- [ ] **Step 1: 写出失败测试，固定状态语义**

\`\`\`csharp
[TestMethod]
public void CreateCompletedResult_WithNoItems_ShouldKeepOriginalConditionContext()
{
    var condition = new SearchCondition
    {
        CategoryDisplay = "Item",
        PropertyDisplay = "名称",
        Test = "equals",
        Value = "M14-404"
    };

    SearchResult result = SearchResult.CreateCompleted(
        condition,
        new List<ModelItem>());

    Assert.AreEqual(SearchResultStatus.NoMatch, result.Status);
    Assert.AreEqual("Item", result.CategoryDisplay);
    Assert.AreEqual("名称", result.PropertyDisplay);
    Assert.AreEqual("equals", result.Test);
    Assert.AreEqual("M14-404", result.QueryValue);
    Assert.AreEqual("当前搜索范围内没有匹配对象", result.StatusMessage);
}

[TestMethod]
public void CreateUnresolvableResult_ShouldNotBeReportedAsNoMatch()
{
    var condition = new SearchCondition
    {
        PropertyDisplay = "名称",
        Test = "equals",
        Value = "M14-404"
    };

    SearchResult result = SearchResult.CreateUnresolvable(
        condition,
        "无法确定属性所属分类");

    Assert.AreEqual(SearchResultStatus.Unresolvable, result.Status);
    Assert.AreNotEqual(SearchResultStatus.NoMatch, result.Status);
    Assert.AreEqual("无法确定属性所属分类", result.StatusMessage);
}
\`\`\`

- [ ] **Step 2: 运行测试，确认当前实现无法满足状态与上下文要求**

Run: \`dotnet test tests\\NavisworksPlugin.Tests\\NavisworksPlugin.Tests.csproj -c Release\`

Expected: FAIL，因为 \`SearchResultStatus\`、条件快照字段和工厂方法尚不存在。

- [ ] **Step 3: 增加最小结果模型**

新增状态枚举：

\`\`\`csharp
public enum SearchResultStatus
{
    Matched,
    NoMatch,
    Unresolvable
}
\`\`\`

在 \`SearchResult\` 中新增以下语义字段，并通过工厂方法在搜索发生时写入：

\`\`\`csharp
public string CategoryDisplay { get; private set; }
public string PropertyDisplay { get; private set; }
public string Test { get; private set; }
public SearchResultStatus Status { get; private set; }
public string StatusMessage { get; private set; }
\`\`\`

工厂方法约束：\`CreateCompleted\` 在命中数大于 0 时使用 \`Matched\`，命中集合为空时使用 \`NoMatch\`；\`CreateUnresolvable\` 使用 \`Unresolvable\`。二者都复制 \`SearchCondition\` 的显示字段与查询值，不保存对可编辑 \`_conditions\` 列表的引用。

- [ ] **Step 4: 让匹配器写入准确状态**

修改 \`ModelItemMatcher.ExecuteSearches\`：

\`\`\`csharp
if (navisCond == null)
{
    results.Add(SearchResult.CreateUnresolvable(
        cond,
        "无法确定属性所属分类"));
    continue;
}

ModelItemCollection found = search.FindAll(doc, false);
results.Add(SearchResult.CreateCompleted(cond, found.Cast<ModelItem>()));
\`\`\`

\`CreateCompleted\` 必须根据集合数量返回 \`Matched\` 或 \`NoMatch\`，不能把“原生条件无法构建”伪装为零命中。

- [ ] **Step 5: 在项目文件中纳入新源文件和测试项目依赖**

主项目在现有 \`Compile\` 项中加入 \`SearchResultStatus.cs\`。测试项目使用 \`Microsoft.NET.Test.Sdk\`、\`MSTest.TestAdapter\`、\`MSTest.TestFramework\`，目标框架为 \`net48\`，并引用主项目及本机 Navisworks API DLL；测试只调用不需要启动 Navisworks 的结果工厂方法。

- [ ] **Step 6: 运行测试，确认状态模型通过**

Run: \`dotnet test tests\\NavisworksPlugin.Tests\\NavisworksPlugin.Tests.csproj -c Release\`

Expected: PASS，且三个结果状态均有独立断言。

- [ ] **Step 7: Commit**

\`\`\`bash
git add SearchResultStatus.cs SearchResult.cs ModelItemMatcher.cs NavisworksPlugin.csproj tests/NavisworksPlugin.Tests
git commit -m "feat: preserve search result status and condition context"
\`\`\`

### Task 3: 将结果页改为面向未命中条件的可视化

**Files:**

- Modify: \`SearchDialog.cs\`
- Modify: \`NavisworksPlugin.csproj\`
- Create: \`ResultSummary.cs\`
- Create: \`tests/NavisworksPlugin.Tests/ResultSummaryTests.cs\`

**Interfaces:**

- Consumes: \`SearchResult.Status\`、条件上下文快照和状态说明。
- Produces: 未命中计数、未命中表格、完整结果表格；两张表均来自同一份 \`_lastResults\`。

- [ ] **Step 1: 写出失败测试，固定统计口径**

\`\`\`csharp
[TestMethod]
public void BuildSummary_ShouldCountNoMatchAndUnresolvableSeparately()
{
    var results = new List<SearchResult>
    {
        SearchResult.CreateCompleted(NoMatchCondition(), new List<ModelItem>()),
        SearchResult.CreateUnresolvable(UnresolvableCondition(), "无法确定属性所属分类")
    };

    ResultSummary summary = ResultSummary.From(results, totalMatched: 0);

    Assert.AreEqual(0, summary.MatchedConditionCount);
    Assert.AreEqual(1, summary.NoMatchConditionCount);
    Assert.AreEqual(1, summary.UnresolvableConditionCount);
}
\`\`\`

- [ ] **Step 2: 用纯逻辑帮助类通过统计测试**

新增一个不依赖 WinForms 控件的 \`ResultSummary\` 类型，负责把 \`SearchResult\` 集合转换为摘要数字。它只能读取结果状态，不能访问 \`Document\`、控件或静态窗口状态。

- [ ] **Step 3: 重建结果页布局**

将现有结果页的单个 \`ListBox\` 改为一个纵向 \`TableLayoutPanel\`，包含：

\`\`\`text
结果摘要
未命中条件（N）或“本次范围内所有条件均已命中”
未命中条件 DataGridView
全部条件明细
全部条件 DataGridView
\`\`\`

未命中表和全部明细表统一使用以下列：状态、分类、属性、匹配方式、查询值、命中对象数、说明。状态文字必须存在；浅红底色仅作为辅助，不能只依赖颜色。

- [ ] **Step 4: 绑定结果，不从当前可编辑条件列表反查上下文**

\`ShowResults\` 仅使用传入的 \`SearchResult\` 快照：

\`\`\`csharp
var noMatchRows = results.Where(r =>
    r.Status == SearchResultStatus.NoMatch ||
    r.Status == SearchResultStatus.Unresolvable);
\`\`\`

用 \`BindingList\` 或逐行填充两个 \`DataGridView\`。禁止用 \`_conditions[index]\` 补齐分类和属性信息，因为用户可在搜索结束后修改、导入或清空条件。

- [ ] **Step 5: 明确没有执行搜索时的显示行为**

在每次点击“执行搜索”后，先调用单一 \`ResetResultState\`：禁用导出/创建选择集，清空摘要和表格，并置空 \`_lastResults\`。仅当搜索完整执行并生成结果后才启用结果操作。

前置校验失败时保留已有错误提示，但不能继续展示或导出上一次搜索的未命中结果。

- [ ] **Step 6: 运行纯逻辑测试**

Run: \`dotnet test tests\\NavisworksPlugin.Tests\\NavisworksPlugin.Tests.csproj -c Release\`

Expected: PASS，未命中与无法执行计数互不混淆。

- [ ] **Step 7: 编译插件**

Run: \`build_2023.bat\`

Expected: Release DLL 生成成功，且没有 WinForms 布局或缺少源文件的编译错误。

- [ ] **Step 8: Commit**

\`\`\`bash
git add SearchDialog.cs ResultSummary.cs NavisworksPlugin.csproj tests/NavisworksPlugin.Tests
git commit -m "feat: visualize unmatched search conditions"
\`\`\`

### Optional Follow-up: 同步导出、日志和使用说明

**Files:**

- Modify: \`SearchDialog.cs\`
- Modify: \`LogService.cs\`
- Modify: \`DiagnosticLogSession.cs\`
- Modify: \`README.md\`
- Modify: \`docs/Obsidian/傑出品-Navisworks查找插件-项目知识库.md\`
- Create: \`ResultExportFormatter.cs\`
- Modify: \`NavisworksPlugin.csproj\`
- Create: \`tests/NavisworksPlugin.Tests/ResultExportTests.cs\`

**Interfaces:**

- Consumes: 任务 2 的结果状态与条件快照。
- Produces: 与可视化一致的 CSV、日志和操作说明。

- [ ] **Step 1: 写出失败测试，固定导出列与状态文本**

\`\`\`csharp
[TestMethod]
public void ToCsvRow_ShouldIncludeStatusAndConditionContext()
{
    SearchResult result = SearchResult.CreateCompleted(
        NoMatchCondition(),
        new List<ModelItem>());

    string csvRow = ResultExportFormatter.ToCsvRow(result);

    StringAssert.Contains(csvRow, "未命中");
    StringAssert.Contains(csvRow, "Item");
    StringAssert.Contains(csvRow, "名称");
    StringAssert.Contains(csvRow, "M14-404");
}
\`\`\`

- [ ] **Step 2: 增加统一的状态文本与 CSV 转义格式化**

\`ResultExportFormatter\` 负责状态中文文本、CSV 字段转义和列顺序。导出列固定为：状态、分类、属性、匹配方式、查询值、命中对象数、说明、匹配详情。

所有包含逗号、双引号或换行的字段使用同一转义函数，避免新增列后继续拼接不安全的 CSV 文本。

- [ ] **Step 3: 更新导出与日志**

结果导出和普通查询日志都使用结果快照输出状态及完整条件上下文。诊断日志保留原有范围、前缀、保护和隐藏信息，不改变其开关语义。

- [ ] **Step 4: 更新用户说明**

在 README、内置使用说明和 Obsidian 知识库中说明：

\`\`\`text
“未命中”表示在本次选定搜索范围内没有匹配对象。
“无法执行”表示插件无法构建该条件，通常需要检查分类或属性名称。
\`\`\`

- [ ] **Step 5: 运行测试并重新编译**

Run: \`dotnet test tests\\NavisworksPlugin.Tests\\NavisworksPlugin.Tests.csproj -c Release\`

Expected: PASS。

Run: \`build_2023.bat\`

Expected: Release DLL 生成成功。

- [ ] **Step 6: Commit**

\`\`\`bash
git add SearchDialog.cs LogService.cs DiagnosticLogSession.cs README.md docs/Obsidian tests/NavisworksPlugin.Tests
git commit -m "docs: explain unmatched search results"
\`\`\`

### Task 4: 部署到 Navisworks 并进行真实模型验收

**Files:**

- No source changes.

**Interfaces:**

- Consumes: Release DLL、插件清单、任务 1 的三类条件数据。
- Produces: 可复现的宿主内验收记录。

- [ ] **Step 1: 部署更新后的 DLL 和清单**

Run: \`install_2023.bat\`

Expected: DLL 与 \`.plugin\` 清单复制到 Navisworks 2023 的插件专属目录。

- [ ] **Step 2: 验收部分命中场景**

在一个范围中执行“已命中 + 值不存在 + 无法发现分类”三条条件。确认结果页同时可见：

\`\`\`text
1 条已命中
1 条未命中
1 条无法执行
\`\`\`

确认每一行均显示分类、属性、方式、查询值、命中数和说明，且未命中区域无需滚动即可被注意到。

- [ ] **Step 3: 验收全命中与全未命中场景**

全命中时，未命中区域显示成功态提示，不出现空白大区域。全未命中时，零命中保护仍阻止隐藏未选中。

- [ ] **Step 4: 验收操作安全与结果新鲜度**

先完成一次成功搜索，再执行一次范围为空或前缀无效的搜索。确认旧结果不会继续显示为本次结果，导出和创建选择集保持禁用。

- [ ] **Step 5: 验收导出与日志一致性**

导出的 CSV 和开启诊断日志后的结果，均包含与结果页一致的状态和条件上下文；其中“未命中”明确包含“当前搜索范围内”语义。

## Self-Review

### Spec coverage

- “可以知道哪些东西没有找到”：任务 2 保存条件上下文，任务 3 用未命中表格展示。
- “可以一目了然地看见”：任务 3 在结果摘要下方固定显示未命中区域，使用状态文字与辅助颜色。
- 不误导用户：任务 1、2、3 区分范围内未命中、无法执行和未开始搜索。
- 保持现有安全逻辑：任务 3、5 验收零命中保护、STR 保护和选择集状态。
- 可追溯：可选后续任务将同一状态同步至导出、日志和说明；初始范围只交付结果页可视化。

### Placeholder scan

本计划未使用 TBD、TODO、“适当处理”或未定义的验证步骤。每个任务均包含文件范围、输入输出、可运行命令或宿主验收预期。

### Type consistency

- \`SearchResultStatus\` 是任务 2 的唯一状态来源。
- \`SearchResult.Status\` 和 \`SearchResult.StatusMessage\` 被任务 3 与任务 4 消费。
- \`ResultSummary\` 与 \`ResultExportFormatter\` 只消费 \`SearchResult\` 快照，不依赖可编辑的窗口状态。

## Execution Handoff

Plan complete and saved to \`docs/superpowers/plans/2026-07-14-unmatched-condition-visualization.md\`. Two execution options:

1. Subagent-Driven (recommended) - I dispatch a fresh subagent per task, review between tasks, fast iteration.

2. Inline Execution - Execute tasks in this session using executing-plans, batch execution with checkpoints.

Before either option, confirm the product definition in Task 1, especially whether “未找到” means a zero-hit query condition and whether the CSV export should include the new state fields.
