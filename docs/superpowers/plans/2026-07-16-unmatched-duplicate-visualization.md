# 未找到与重复对象可视化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将每条查询条件验证为“未找到、唯一找到、重复或条件异常”，在结果页结构化展示，并且只有全部条件唯一匹配时才允许隐藏未选中。

**Architecture:** 把数量判定和隐藏门禁放入不依赖 Navisworks 的纯逻辑策略层，用条件快照保证结果可追溯；匹配层负责生成状态，WinForms 结果页只负责筛选和展示。现有范围搜索、STR 保护、选择合并和隐藏服务保持原有边界。

**Tech Stack:** C# 7.3、.NET Framework 4.8、WinForms、Autodesk Navisworks Manage 2023 API、MSTest 4.3.2、Microsoft.NET.Test.Sdk 18.8.1、MSBuild 17.14。

## Global Constraints

- 主插件继续面向 Navisworks Manage 2023、.NET Framework 4.8、x64。
- 每条查询条件必须恰好匹配 1 个对象；0 个为未找到，大于等于 2 个为重复。
- 分类或条件无法构造时必须标记为条件异常，不能伪装成未找到。
- 任意未找到、重复或条件异常都必须严格阻止隐藏，不提供绕过选项。
- 搜索范围仍是用户执行搜索时的当前选择快照，不自动扩大到整个模型。
- 多条件仍独立搜索并在对象层取并集；STR 保护、选择去重和隐藏服务保持现有语义。
- “重复”只表示同一条条件命中至少 2 个不同对象；重复输入条件或不同条件命中同一对象不标记为重复。
- 不引入第三方 WinForms UI 库，不改插件清单、入口或部署目录。
- 保留工作区中已有的 `HideServiceFixed.cs`、`ModelItemMatcher.cs`、`Properties/AssemblyInfo.cs` 用户改动，实施时在其基础上合并。
- `.codegraph/`、`.superpowers/`、`bin/`、`obj/` 不得进入功能提交。

## File Map

**Create**

- `SearchResultStatus.cs`：四态结果与结果筛选枚举。
- `SearchResultPolicy.cs`：数量分类、隐藏门禁和筛选判定。
- `SearchConditionSnapshot.cs`：本次搜索使用的不可变条件快照。
- `SearchConditionValidator.cs`：不依赖 Navisworks 的基础条件校验。
- `tests/JiePinPai.Navisworks.Tests/JiePinPai.Navisworks.Tests.csproj`：纯逻辑测试项目。
- `tests/JiePinPai.Navisworks.Tests/SearchResultPolicyTests.cs`：状态、门禁、筛选测试。
- `tests/JiePinPai.Navisworks.Tests/SearchConditionSnapshotTests.cs`：快照隔离测试。
- `tests/JiePinPai.Navisworks.Tests/SearchConditionValidatorTests.cs`：条件校验测试。

**Modify**

- `NavisworksPlugin.csproj`：纳入新增生产源文件。
- `SearchResult.cs`：保存条件快照、状态、说明和匹配对象。
- `ModelItemMatcher.cs`：生成四态结果并区分条件异常与零命中。
- `SearchDialog.cs`：结果表格、筛选、定位、结果失效和严格隐藏门禁。
- `DiagnosticLogSession.cs`：记录带原因的隐藏阻断。
- `DiagnosticLogExtensions.cs`：记录四态结果明细与汇总。
- `LogService.cs`：旧版文本日志输出完整条件和四态统计。
- `README.md`：说明唯一性规则、结果页和隐藏门禁。
- `CHANGELOG.md`：记录功能与行为变更。
- `docs/Obsidian/傑出品-Navisworks查找插件-项目知识库.md`：同步可复用知识和当前流程。

---

### Task 1: 建立纯逻辑结果策略与测试基线

**Files:**

- Create: `SearchResultStatus.cs`
- Create: `SearchResultPolicy.cs`
- Create: `tests/JiePinPai.Navisworks.Tests/JiePinPai.Navisworks.Tests.csproj`
- Create: `tests/JiePinPai.Navisworks.Tests/SearchResultPolicyTests.cs`
- Modify: `NavisworksPlugin.csproj`

**Interfaces:**

- Produces: `SearchResultStatus`、`SearchResultFilter`。
- Produces: `SearchResultPolicy.Classify(bool conditionExecuted, int matchCount)`。
- Produces: `SearchResultPolicy.CanHide(IEnumerable<SearchResultStatus> statuses)`。
- Produces: `SearchResultPolicy.MatchesFilter(SearchResultStatus status, SearchResultFilter filter)`。
- Produces: `SearchResultPolicy.GetDisplayName(SearchResultStatus status)`。

- [ ] **Step 1: 创建测试项目并写状态分类失败测试**

Create `tests/JiePinPai.Navisworks.Tests/JiePinPai.Navisworks.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="4.3.2" />
    <PackageReference Include="MSTest.TestFramework" Version="4.3.2" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="..\..\SearchResultStatus.cs" Link="Production\SearchResultStatus.cs" />
    <Compile Include="..\..\SearchResultPolicy.cs" Link="Production\SearchResultPolicy.cs" />
  </ItemGroup>
</Project>
```

Create `tests/JiePinPai.Navisworks.Tests/SearchResultPolicyTests.cs`:

```csharp
using System;
using JiePinPai.Navisworks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class SearchResultPolicyTests
    {
        [DataTestMethod]
        [DataRow(false, 0, SearchResultStatus.ConditionInvalid)]
        [DataRow(true, 0, SearchResultStatus.NotFound)]
        [DataRow(true, 1, SearchResultStatus.Found)]
        [DataRow(true, 2, SearchResultStatus.Duplicate)]
        [DataRow(true, 20, SearchResultStatus.Duplicate)]
        public void Classify_UsesExecutionAndExactOneRule(
            bool executed,
            int count,
            SearchResultStatus expected)
        {
            Assert.AreEqual(expected, SearchResultPolicy.Classify(executed, count));
        }

        [TestMethod]
        public void Classify_RejectsNegativeMatchCount()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => SearchResultPolicy.Classify(true, -1));
        }

        [TestMethod]
        public void CanHide_RequiresAtLeastOneResultAndAllFound()
        {
            Assert.IsFalse(SearchResultPolicy.CanHide(null));
            Assert.IsFalse(SearchResultPolicy.CanHide(Array.Empty<SearchResultStatus>()));
            Assert.IsTrue(SearchResultPolicy.CanHide(new[]
            {
                SearchResultStatus.Found,
                SearchResultStatus.Found,
            }));
            Assert.IsFalse(SearchResultPolicy.CanHide(new[]
            {
                SearchResultStatus.Found,
                SearchResultStatus.Duplicate,
            }));
        }

        [DataTestMethod]
        [DataRow(SearchResultStatus.NotFound, SearchResultFilter.Problems, true)]
        [DataRow(SearchResultStatus.Duplicate, SearchResultFilter.Problems, true)]
        [DataRow(SearchResultStatus.ConditionInvalid, SearchResultFilter.Problems, true)]
        [DataRow(SearchResultStatus.Found, SearchResultFilter.Problems, false)]
        [DataRow(SearchResultStatus.Found, SearchResultFilter.All, true)]
        [DataRow(SearchResultStatus.Duplicate, SearchResultFilter.Duplicate, true)]
        public void MatchesFilter_UsesStatusGroups(
            SearchResultStatus status,
            SearchResultFilter filter,
            bool expected)
        {
            Assert.AreEqual(expected, SearchResultPolicy.MatchesFilter(status, filter));
        }

        [DataTestMethod]
        [DataRow(SearchResultStatus.Found, "已找到")]
        [DataRow(SearchResultStatus.NotFound, "未找到")]
        [DataRow(SearchResultStatus.Duplicate, "重复")]
        [DataRow(SearchResultStatus.ConditionInvalid, "条件异常")]
        public void GetDisplayName_ReturnsStableChineseLabels(
            SearchResultStatus status,
            string expected)
        {
            Assert.AreEqual(expected, SearchResultPolicy.GetDisplayName(status));
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认因生产类型不存在而失败**

Run:

```powershell
dotnet test tests\JiePinPai.Navisworks.Tests\JiePinPai.Navisworks.Tests.csproj -c Release
```

Expected: build fails with missing `SearchResultStatus` and `SearchResultPolicy` types.

- [ ] **Step 3: 实现四态枚举和纯逻辑策略**

Create `SearchResultStatus.cs`:

```csharp
namespace JiePinPai.Navisworks
{
    public enum SearchResultStatus
    {
        Found,
        NotFound,
        Duplicate,
        ConditionInvalid,
    }

    public enum SearchResultFilter
    {
        All,
        Problems,
        Found,
        NotFound,
        Duplicate,
        ConditionInvalid,
    }
}
```

Create `SearchResultPolicy.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace JiePinPai.Navisworks
{
    public static class SearchResultPolicy
    {
        public static SearchResultStatus Classify(
            bool conditionExecuted,
            int matchCount)
        {
            if (matchCount < 0)
                throw new ArgumentOutOfRangeException(nameof(matchCount));

            if (!conditionExecuted)
                return SearchResultStatus.ConditionInvalid;
            if (matchCount == 0)
                return SearchResultStatus.NotFound;
            if (matchCount == 1)
                return SearchResultStatus.Found;
            return SearchResultStatus.Duplicate;
        }

        public static bool CanHide(IEnumerable<SearchResultStatus> statuses)
        {
            if (statuses == null)
                return false;

            bool hasAny = false;
            foreach (SearchResultStatus status in statuses)
            {
                hasAny = true;
                if (status != SearchResultStatus.Found)
                    return false;
            }

            return hasAny;
        }

        public static bool MatchesFilter(
            SearchResultStatus status,
            SearchResultFilter filter)
        {
            switch (filter)
            {
                case SearchResultFilter.All:
                    return true;
                case SearchResultFilter.Problems:
                    return status != SearchResultStatus.Found;
                case SearchResultFilter.Found:
                    return status == SearchResultStatus.Found;
                case SearchResultFilter.NotFound:
                    return status == SearchResultStatus.NotFound;
                case SearchResultFilter.Duplicate:
                    return status == SearchResultStatus.Duplicate;
                case SearchResultFilter.ConditionInvalid:
                    return status == SearchResultStatus.ConditionInvalid;
                default:
                    throw new ArgumentOutOfRangeException(nameof(filter));
            }
        }

        public static string GetDisplayName(SearchResultStatus status)
        {
            switch (status)
            {
                case SearchResultStatus.Found:
                    return "已找到";
                case SearchResultStatus.NotFound:
                    return "未找到";
                case SearchResultStatus.Duplicate:
                    return "重复";
                case SearchResultStatus.ConditionInvalid:
                    return "条件异常";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }
    }
}
```

Add to the production compile item group in `NavisworksPlugin.csproj`:

```xml
<Compile Include="SearchResultStatus.cs" />
<Compile Include="SearchResultPolicy.cs" />
```

- [ ] **Step 4: 运行策略测试并确认通过**

Run:

```powershell
dotnet test tests\JiePinPai.Navisworks.Tests\JiePinPai.Navisworks.Tests.csproj -c Release
```

Expected: all `SearchResultPolicyTests` pass.

- [ ] **Step 5: 编译主插件确认新增类型兼容 C# 7.3**

Run:

```powershell
dotnet build NavisworksPlugin.csproj -c Release
```

Expected: build succeeds with 0 errors.

- [ ] **Step 6: 提交结果策略基线**

```powershell
git add NavisworksPlugin.csproj SearchResultStatus.cs SearchResultPolicy.cs tests\JiePinPai.Navisworks.Tests
git commit -m "test: define unique search result policy"
```

---

### Task 2: 添加不可变条件快照并扩展结果模型

**Files:**

- Create: `SearchConditionSnapshot.cs`
- Create: `tests/JiePinPai.Navisworks.Tests/SearchConditionSnapshotTests.cs`
- Modify: `tests/JiePinPai.Navisworks.Tests/JiePinPai.Navisworks.Tests.csproj`
- Modify: `SearchResult.cs`
- Modify: `NavisworksPlugin.csproj`

**Interfaces:**

- Consumes: `SearchCondition`、`SearchResultStatus`。
- Produces: `SearchConditionSnapshot.From(int conditionIndex, SearchCondition source)`。
- Produces: `SearchResult.Condition`、`SearchResult.Status`、`SearchResult.StatusMessage`、`SearchResult.IsProblem`。

- [ ] **Step 1: 写条件快照隔离失败测试**

Add linked production files to the test project:

```xml
<Compile Include="..\..\SearchCondition.cs" Link="Production\SearchCondition.cs" />
<Compile Include="..\..\SearchConditionSnapshot.cs" Link="Production\SearchConditionSnapshot.cs" />
```

Create `tests/JiePinPai.Navisworks.Tests/SearchConditionSnapshotTests.cs`:

```csharp
using JiePinPai.Navisworks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class SearchConditionSnapshotTests
    {
        [TestMethod]
        public void From_CopiesAllFieldsAndDoesNotTrackLaterEdits()
        {
            var source = new SearchCondition
            {
                CategoryInternal = "LcOaItem",
                CategoryDisplay = "Item",
                PropertyInternal = "LcOaSceneBaseUserName",
                PropertyDisplay = "名称",
                Test = "equals",
                Value = "M14-101",
            };

            SearchConditionSnapshot snapshot = SearchConditionSnapshot.From(3, source);
            source.Value = "CHANGED";

            Assert.AreEqual(3, snapshot.ConditionIndex);
            Assert.AreEqual(4, snapshot.DisplayIndex);
            Assert.AreEqual("Item", snapshot.CategoryDisplay);
            Assert.AreEqual("名称", snapshot.PropertyDisplay);
            Assert.AreEqual("equals", snapshot.Test);
            Assert.AreEqual("M14-101", snapshot.Value);
        }

        [TestMethod]
        public void DisplayNames_UseExplicitPlaceholdersInsteadOfBlankCells()
        {
            var source = new SearchCondition
            {
                CategoryDisplay = "",
                CategoryInternal = "",
                PropertyDisplay = "",
                PropertyInternal = "",
            };

            SearchConditionSnapshot snapshot = SearchConditionSnapshot.From(0, source);

            Assert.AreEqual("（自动识别）", snapshot.GetCategoryName());
            Assert.AreEqual("（缺失）", snapshot.GetPropertyName());
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认快照类型缺失**

Run:

```powershell
dotnet test tests\JiePinPai.Navisworks.Tests\JiePinPai.Navisworks.Tests.csproj -c Release
```

Expected: build fails because `SearchConditionSnapshot` does not exist.

- [ ] **Step 3: 实现条件快照**

Create `SearchConditionSnapshot.cs`:

```csharp
using System;

namespace JiePinPai.Navisworks
{
    public sealed class SearchConditionSnapshot
    {
        public int ConditionIndex { get; }
        public int DisplayIndex => ConditionIndex + 1;
        public string CategoryInternal { get; }
        public string CategoryDisplay { get; }
        public string PropertyInternal { get; }
        public string PropertyDisplay { get; }
        public string Test { get; }
        public string Value { get; }

        private SearchConditionSnapshot(
            int conditionIndex,
            SearchCondition source)
        {
            ConditionIndex = conditionIndex;
            CategoryInternal = source.CategoryInternal ?? string.Empty;
            CategoryDisplay = source.CategoryDisplay ?? string.Empty;
            PropertyInternal = source.PropertyInternal ?? string.Empty;
            PropertyDisplay = source.PropertyDisplay ?? string.Empty;
            Test = source.Test ?? string.Empty;
            Value = source.Value ?? string.Empty;
        }

        public static SearchConditionSnapshot From(
            int conditionIndex,
            SearchCondition source)
        {
            if (conditionIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(conditionIndex));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return new SearchConditionSnapshot(conditionIndex, source);
        }

        public string GetCategoryName()
        {
            if (!string.IsNullOrWhiteSpace(CategoryDisplay))
                return CategoryDisplay;
            if (!string.IsNullOrWhiteSpace(CategoryInternal))
                return CategoryInternal;
            return "（自动识别）";
        }

        public string GetPropertyName()
        {
            if (!string.IsNullOrWhiteSpace(PropertyDisplay))
                return PropertyDisplay;
            if (!string.IsNullOrWhiteSpace(PropertyInternal))
                return PropertyInternal;
            return "（缺失）";
        }
    }
}
```

Add to `NavisworksPlugin.csproj`:

```xml
<Compile Include="SearchConditionSnapshot.cs" />
```

- [ ] **Step 4: 扩展 SearchResult 为统一结果事实源**

Replace `SearchResult.cs` with:

```csharp
using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    public class SearchResult
    {
        public SearchConditionSnapshot Condition { get; set; }
        public SearchResultStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public int MatchCount { get; set; }
        public List<ModelItem> MatchedItems { get; set; }

        public string QueryValue => Condition?.Value ?? string.Empty;
        public bool IsProblem => Status != SearchResultStatus.Found;

        public SearchResult()
        {
            Status = SearchResultStatus.ConditionInvalid;
            StatusMessage = string.Empty;
            MatchedItems = new List<ModelItem>();
        }
    }
}
```

- [ ] **Step 5: 运行快照测试和主插件编译**

Run:

```powershell
dotnet test tests\JiePinPai.Navisworks.Tests\JiePinPai.Navisworks.Tests.csproj -c Release
dotnet build NavisworksPlugin.csproj -c Release
```

Expected: snapshot test passes; the plugin build may fail only at old `SearchResult.QueryValue` initializers in `ModelItemMatcher.cs`, proving Task 3 must migrate those call sites before its commit. Do not commit while the plugin build is red.

- [ ] **Step 6: Temporarily update old matcher initializers to use Condition snapshots, then confirm green build**

In `ModelItemMatcher.cs`, replace every `QueryValue = cond.Value` initializer with:

```csharp
Condition = SearchConditionSnapshot.From(results.Count, cond),
```

Replace `MakeEmptyResult(string queryValue)` with:

```csharp
private static SearchResult MakeEmptyResult(
    int conditionIndex,
    SearchCondition condition)
{
    return new SearchResult
    {
        Condition = SearchConditionSnapshot.From(conditionIndex, condition),
        Status = SearchResultStatus.ConditionInvalid,
        StatusMessage = "条件无法构造。",
        MatchCount = 0,
        MatchedItems = new List<ModelItem>(),
    };
}
```

Update its call to:

```csharp
results.Add(MakeEmptyResult(results.Count, cond));
```

Run:

```powershell
dotnet build NavisworksPlugin.csproj -c Release
```

Expected: build succeeds with 0 errors. Task 3 will replace this transitional matcher code with final validation logic.

- [ ] **Step 7: 提交条件快照和结果模型**

```powershell
git add NavisworksPlugin.csproj SearchConditionSnapshot.cs SearchResult.cs ModelItemMatcher.cs tests\JiePinPai.Navisworks.Tests
git commit -m "refactor: preserve search condition snapshots"
```

---

### Task 3: 验证条件并从匹配层生成四态结果

**Files:**

- Create: `SearchConditionValidator.cs`
- Create: `tests/JiePinPai.Navisworks.Tests/SearchConditionValidatorTests.cs`
- Modify: `tests/JiePinPai.Navisworks.Tests/JiePinPai.Navisworks.Tests.csproj`
- Modify: `ModelItemMatcher.cs:51-102, 173-226`
- Modify: `NavisworksPlugin.csproj`

**Interfaces:**

- Consumes: `SearchConditionSnapshot`、`SearchResultPolicy.Classify`。
- Produces: `SearchConditionValidator.TryValidate(SearchCondition condition, out string errorMessage)`。
- Produces: `TryBuildNavisCondition(SearchCondition condition, Dictionary<string, string> categoryCache, out NavisCondition navisCondition, out string errorMessage)`。
- Produces: every `ModelItemMatcher.MatchAll` result with a complete snapshot, status and message.

- [ ] **Step 1: 写基础条件校验失败测试**

Add to the test project:

```xml
<Compile Include="..\..\SearchConditionValidator.cs" Link="Production\SearchConditionValidator.cs" />
```

Create `tests/JiePinPai.Navisworks.Tests/SearchConditionValidatorTests.cs`:

```csharp
using JiePinPai.Navisworks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class SearchConditionValidatorTests
    {
        [TestMethod]
        public void TryValidate_AcceptsSupportedCompleteCondition()
        {
            var condition = new SearchCondition
            {
                PropertyDisplay = "名称",
                Test = "equals",
                Value = "M14-101",
            };

            Assert.IsTrue(SearchConditionValidator.TryValidate(condition, out string error));
            Assert.AreEqual(string.Empty, error);
        }

        [DataTestMethod]
        [DataRow("", "equals", "M14-101", "属性名不能为空。")]
        [DataRow("名称", "equals", "", "查询值不能为空。")]
        [DataRow("名称", "startsWith", "M14", "匹配方式仅支持 equals 或 contains。")]
        public void TryValidate_RejectsIncompleteOrUnsupportedCondition(
            string property,
            string test,
            string value,
            string expectedError)
        {
            var condition = new SearchCondition
            {
                PropertyDisplay = property,
                Test = test,
                Value = value,
            };

            Assert.IsFalse(SearchConditionValidator.TryValidate(condition, out string error));
            Assert.AreEqual(expectedError, error);
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认校验器缺失**

```powershell
dotnet test tests\JiePinPai.Navisworks.Tests\JiePinPai.Navisworks.Tests.csproj -c Release
```

Expected: build fails because `SearchConditionValidator` does not exist.

- [ ] **Step 3: 实现不依赖宿主的条件校验器**

Create `SearchConditionValidator.cs`:

```csharp
using System;

namespace JiePinPai.Navisworks
{
    public static class SearchConditionValidator
    {
        public static bool TryValidate(
            SearchCondition condition,
            out string errorMessage)
        {
            if (condition == null)
            {
                errorMessage = "搜索条件为空。";
                return false;
            }

            string propertyName = !string.IsNullOrWhiteSpace(condition.PropertyDisplay)
                ? condition.PropertyDisplay
                : condition.PropertyInternal;
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                errorMessage = "属性名不能为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(condition.Value))
            {
                errorMessage = "查询值不能为空。";
                return false;
            }

            bool supported = string.Equals(
                    condition.Test,
                    "equals",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    condition.Test,
                    "contains",
                    StringComparison.OrdinalIgnoreCase);
            if (!supported)
            {
                errorMessage = "匹配方式仅支持 equals 或 contains。";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
```

Add to `NavisworksPlugin.csproj`:

```xml
<Compile Include="SearchConditionValidator.cs" />
```

- [ ] **Step 4: 让匹配器区分条件异常和正常零命中**

Replace `ExecuteSearches` in `ModelItemMatcher.cs` with:

```csharp
private static List<SearchResult> ExecuteSearches(
    Document doc,
    List<SearchCondition> conditions,
    IEnumerable<ModelItem> scopeItems)
{
    var categoryCache = new Dictionary<string, string>();
    DiscoverCategories(doc, conditions, categoryCache);

    ModelItemCollection scopeCollection = null;
    if (scopeItems != null)
    {
        scopeCollection = new ModelItemCollection();
        foreach (ModelItem item in scopeItems)
            scopeCollection.Add(item);
    }

    var results = new List<SearchResult>(conditions.Count);
    for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
    {
        SearchCondition condition = conditions[conditionIndex];
        SearchConditionSnapshot snapshot = SearchConditionSnapshot.From(
            conditionIndex,
            condition);

        if (!TryBuildNavisCondition(
                condition,
                categoryCache,
                out NavisCondition navisCondition,
                out string validationError))
        {
            results.Add(CreateResult(
                snapshot,
                conditionExecuted: false,
                matchedItems: new List<ModelItem>(),
                invalidReason: validationError));
            continue;
        }

        using (var search = new Search())
        {
            if (scopeCollection != null)
                search.Selection.CopyFrom(scopeCollection);
            else
                search.Selection.SelectAll();

            search.SearchConditions.Add(navisCondition);
            ModelItemCollection found = search.FindAll(doc, false);
            var seenItems = new HashSet<ModelItem>();
            List<ModelItem> matchedItems = found
                .Cast<ModelItem>()
                .Where(item => item != null && seenItems.Add(item))
                .ToList();
            results.Add(CreateResult(
                snapshot,
                conditionExecuted: true,
                matchedItems: matchedItems,
                invalidReason: string.Empty));
        }
    }

    return results;
}
```

Keep `search.FindAll(doc, false)` outside any per-condition catch block. A
failure while constructing one condition becomes `ConditionInvalid`; a host or
model failure while executing the Search API aborts the whole run through the
existing `BtnSearch_Click` exception path instead of publishing partial data.

Replace `BuildNavisCondition` and `MakeEmptyResult` with:

```csharp
private static bool TryBuildNavisCondition(
    SearchCondition condition,
    Dictionary<string, string> categoryCache,
    out NavisCondition navisCondition,
    out string errorMessage)
{
    navisCondition = null;
    if (!SearchConditionValidator.TryValidate(condition, out errorMessage))
        return false;

    string propertyName = !string.IsNullOrWhiteSpace(condition.PropertyDisplay)
        ? condition.PropertyDisplay
        : condition.PropertyInternal;
    string categoryName = !string.IsNullOrWhiteSpace(condition.CategoryDisplay)
        ? condition.CategoryDisplay
        : condition.CategoryInternal;

    if (string.IsNullOrWhiteSpace(categoryName))
    {
        string discoveryKey = condition.PropertyDisplay;
        if (string.IsNullOrWhiteSpace(discoveryKey)
            || !categoryCache.TryGetValue(discoveryKey, out categoryName))
        {
            errorMessage = $"无法识别属性“{propertyName}”所属分类。";
            return false;
        }
    }

    try
    {
        NavisCondition propertyCondition =
            NavisCondition.HasPropertyByDisplayName(categoryName, propertyName);
        bool contains = string.Equals(
            condition.Test,
            "contains",
            StringComparison.OrdinalIgnoreCase);
        navisCondition = contains
            ? propertyCondition.DisplayStringContains(condition.Value)
                .IgnoreStringValueCase()
            : propertyCondition.EqualValue(
                VariantData.FromDisplayString(condition.Value))
                .IgnoreStringValueCase();
        errorMessage = string.Empty;
        return true;
    }
    catch (Exception ex)
    {
        navisCondition = null;
        errorMessage = "条件构造失败：" + ex.Message;
        return false;
    }
}

private static SearchResult CreateResult(
    SearchConditionSnapshot snapshot,
    bool conditionExecuted,
    List<ModelItem> matchedItems,
    string invalidReason)
{
    int matchCount = matchedItems?.Count ?? 0;
    SearchResultStatus status = SearchResultPolicy.Classify(
        conditionExecuted,
        matchCount);

    string message;
    switch (status)
    {
        case SearchResultStatus.Found:
            message = "当前选定范围内唯一匹配 1 个对象。";
            break;
        case SearchResultStatus.NotFound:
            message = "当前选定范围内未找到对象。";
            break;
        case SearchResultStatus.Duplicate:
            message = $"当前选定范围内匹配 {matchCount} 个对象，要求唯一。";
            break;
        case SearchResultStatus.ConditionInvalid:
            message = invalidReason;
            break;
        default:
            throw new InvalidOperationException("未知搜索结果状态。");
    }

    return new SearchResult
    {
        Condition = snapshot,
        Status = status,
        StatusMessage = message,
        MatchCount = matchCount,
        MatchedItems = matchedItems ?? new List<ModelItem>(),
    };
}
```

- [ ] **Step 5: 运行纯逻辑测试和主插件编译**

```powershell
dotnet test tests\JiePinPai.Navisworks.Tests\JiePinPai.Navisworks.Tests.csproj -c Release
dotnet build NavisworksPlugin.csproj -c Release
```

Expected: all unit tests pass and plugin build succeeds with 0 errors.
Also verify that `MatchCount` is assigned from the de-duplicated `matchedItems`
list, so duplicate status means two or more distinct model objects rather than
duplicate enumeration entries.

- [ ] **Step 6: 提交匹配状态生成**

```powershell
git add NavisworksPlugin.csproj SearchConditionValidator.cs ModelItemMatcher.cs tests\JiePinPai.Navisworks.Tests
git commit -m "feat: classify unique search outcomes"
```

---

### Task 4: 将结果页升级为结构化状态表格

**Files:**

- Modify: `SearchDialog.cs:31-59, 550-592, 1409-1430`

**Interfaces:**

- Consumes: `SearchResult.Status`、`SearchResult.Condition`、`SearchResultPolicy.MatchesFilter`。
- Produces: `SetActiveResultFilter(SearchResultFilter filter)`。
- Produces: `RefreshResultsGrid(IEnumerable<SearchResult> results)`。
- Produces: `ShowResults(List<SearchResult> results, int totalMatched, string scopeLabel)`。

- [ ] **Step 1: 用结果表格字段替换旧 ListBox 字段**

Replace the result field declarations with:

```csharp
private Label _lblResultSummary;
private FlowLayoutPanel _resultFilterPanel;
private DataGridView _resultsGrid;
private SearchResultFilter _activeResultFilter = SearchResultFilter.All;
private Button _btnSearch;
private Button _btnExportResults;
private Button _btnCreateSelectionSet;
private Button _btnClose;
```

- [ ] **Step 2: 替换结果页布局**

Replace `BuildResultsTab` with:

```csharp
private void BuildResultsTab()
{
    _tabResults.SuspendLayout();
    int summaryHeight = CalculateContentHeight(this.Font, 2, 24);
    int filterHeight = CalculateButtonHeight(this.Font) + ScaleLogical(12);

    var root = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 3,
        Padding = new Padding(ScaleLogical(4)),
        BackColor = _tabResults.BackColor,
    };
    root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
    root.RowStyles.Add(new RowStyle(SizeType.Absolute, summaryHeight));
    root.RowStyles.Add(new RowStyle(SizeType.Absolute, filterHeight));
    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

    _lblResultSummary = new Label
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(ScaleLogical(12), ScaleLogical(8), ScaleLogical(12), 0),
        BackColor = Color.FromArgb(239, 246, 255),
        ForeColor = Color.FromArgb(30, 64, 175),
        BorderStyle = BorderStyle.FixedSingle,
        Text = "尚未执行搜索。",
    };

    _resultFilterPanel = new FlowLayoutPanel
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        Padding = new Padding(0, ScaleLogical(6), 0, 0),
        BackColor = _tabResults.BackColor,
    };
    AddResultFilterButton(SearchResultFilter.All, "全部");
    AddResultFilterButton(SearchResultFilter.Problems, "问题项");
    AddResultFilterButton(SearchResultFilter.Found, "已找到");
    AddResultFilterButton(SearchResultFilter.NotFound, "未找到");
    AddResultFilterButton(SearchResultFilter.Duplicate, "重复");
    AddResultFilterButton(SearchResultFilter.ConditionInvalid, "条件异常");

    _resultsGrid = CreateResultsGrid();
    SetActiveResultFilter(SearchResultFilter.All);

    root.Controls.Add(_lblResultSummary, 0, 0);
    root.Controls.Add(_resultFilterPanel, 0, 1);
    root.Controls.Add(_resultsGrid, 0, 2);
    _tabResults.Controls.Add(root);
    _tabResults.ResumeLayout(true);
}
```

- [ ] **Step 3: 添加筛选按钮和只读结果表格工厂**

Add these methods beside `BuildResultsTab`:

```csharp
private void AddResultFilterButton(SearchResultFilter filter, string text)
{
    var button = new Button
    {
        Text = text,
        Tag = filter,
        AutoSize = true,
        MinimumSize = new Size(
            ScaleLogical(74),
            CalculateButtonHeight(this.Font)),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.White,
        ForeColor = Color.FromArgb(51, 65, 85),
        Font = this.Font,
        Margin = new Padding(0, 0, ScaleLogical(6), 0),
    };
    button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
    button.Click += ResultFilterButton_Click;
    _resultFilterPanel.Controls.Add(button);
}

private DataGridView CreateResultsGrid()
{
    var grid = new DataGridView
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        ReadOnly = true,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false,
        AutoGenerateColumns = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        GridColor = Color.FromArgb(226, 232, 240),
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
    };
    grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
    grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
    grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
    grid.EnableHeadersVisualStyles = false;
    grid.RowTemplate.Height = CalculateContentHeight(grid.Font, 1, 12);
    ApplyGridHeaderLayout(grid);

    grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        Name = "ConditionIndex",
        HeaderText = "序号",
        Width = ScaleLogical(58),
    });
    grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        Name = "Status",
        HeaderText = "状态",
        Width = ScaleLogical(88),
    });
    grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        Name = "Category",
        HeaderText = "分类",
        Width = ScaleLogical(118),
    });
    grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        Name = "Property",
        HeaderText = "属性名",
        Width = ScaleLogical(132),
    });
    grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        Name = "Test",
        HeaderText = "匹配方式",
        Width = ScaleLogical(86),
    });
    grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        Name = "Value",
        HeaderText = "查询值",
        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        FillWeight = 32F,
    });
    grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        Name = "MatchCount",
        HeaderText = "匹配数",
        Width = ScaleLogical(70),
    });
    grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        Name = "Message",
        HeaderText = "说明",
        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        FillWeight = 48F,
    });
    return grid;
}
```

- [ ] **Step 4: 添加状态筛选、统计和行配色**

Add these methods:

```csharp
private void ResultFilterButton_Click(object sender, EventArgs e)
{
    var button = sender as Button;
    if (button?.Tag is SearchResultFilter filter)
        SetActiveResultFilter(filter);
}

private void SetActiveResultFilter(SearchResultFilter filter)
{
    _activeResultFilter = filter;
    foreach (Control control in _resultFilterPanel.Controls)
    {
        var button = control as Button;
        if (button == null)
            continue;

        bool active = button.Tag is SearchResultFilter value && value == filter;
        button.BackColor = active ? Color.FromArgb(37, 99, 235) : Color.White;
        button.ForeColor = active ? Color.White : Color.FromArgb(51, 65, 85);
    }
    RefreshResultsGrid(_lastResults ?? Enumerable.Empty<SearchResult>());
}

private void RefreshResultsGrid(IEnumerable<SearchResult> results)
{
    _resultsGrid.Rows.Clear();
    foreach (SearchResult result in results)
    {
        if (!SearchResultPolicy.MatchesFilter(result.Status, _activeResultFilter))
            continue;

        int rowIndex = _resultsGrid.Rows.Add(
            result.Condition.DisplayIndex,
            SearchResultPolicy.GetDisplayName(result.Status),
            result.Condition.GetCategoryName(),
            result.Condition.GetPropertyName(),
            result.Condition.Test,
            result.Condition.Value,
            result.MatchCount,
            result.StatusMessage);
        DataGridViewRow row = _resultsGrid.Rows[rowIndex];
        row.Tag = result;
        ApplyResultRowStyle(row, result.Status);
        foreach (DataGridViewCell cell in row.Cells)
            cell.ToolTipText = Convert.ToString(cell.Value);
    }
}

private static void ApplyResultRowStyle(
    DataGridViewRow row,
    SearchResultStatus status)
{
    switch (status)
    {
        case SearchResultStatus.Found:
            row.DefaultCellStyle.BackColor = Color.White;
            row.DefaultCellStyle.ForeColor = Color.FromArgb(22, 101, 52);
            break;
        case SearchResultStatus.NotFound:
            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 241, 242);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(153, 27, 27);
            break;
        case SearchResultStatus.Duplicate:
            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 247, 237);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(154, 52, 18);
            break;
        case SearchResultStatus.ConditionInvalid:
            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 251, 235);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(133, 77, 14);
            break;
    }
    row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
    row.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
}

private void UpdateResultFilterCaptions(IReadOnlyCollection<SearchResult> results)
{
    foreach (Control control in _resultFilterPanel.Controls)
    {
        var button = control as Button;
        if (!(button?.Tag is SearchResultFilter filter))
            continue;

        int count = results.Count(r =>
            SearchResultPolicy.MatchesFilter(r.Status, filter));
        button.Text = $"{GetFilterTitle(filter)} {count}";
    }
}

private static string GetFilterTitle(SearchResultFilter filter)
{
    switch (filter)
    {
        case SearchResultFilter.All: return "全部";
        case SearchResultFilter.Problems: return "问题项";
        case SearchResultFilter.Found: return "已找到";
        case SearchResultFilter.NotFound: return "未找到";
        case SearchResultFilter.Duplicate: return "重复";
        case SearchResultFilter.ConditionInvalid: return "条件异常";
        default: throw new ArgumentOutOfRangeException(nameof(filter));
    }
}
```

- [ ] **Step 5: 替换 ShowResults**

```csharp
private void ShowResults(
    List<SearchResult> results,
    int totalMatched,
    string scopeLabel)
{
    int found = results.Count(r => r.Status == SearchResultStatus.Found);
    int notFound = results.Count(r => r.Status == SearchResultStatus.NotFound);
    int duplicate = results.Count(r => r.Status == SearchResultStatus.Duplicate);
    int invalid = results.Count(r => r.Status == SearchResultStatus.ConditionInvalid);

    _lastResults = results;
    _lblResultSummary.Text =
        $"范围：{scopeLabel}　　条件总数：{results.Count}　　" +
        $"已找到：{found}　未找到：{notFound}　重复：{duplicate}　条件异常：{invalid}\n" +
        $"去重后的总匹配对象：{totalMatched} 个";
    UpdateResultFilterCaptions(results);
    SetActiveResultFilter(results.Any(r => r.IsProblem)
        ? SearchResultFilter.Problems
        : SearchResultFilter.All);
}
```

In `BtnSearch_Click`, replace the old four-argument result call and its
`matchedConds` / `unmatchedConds` counters with:

```csharp
ShowResults(results, totalMatched, modelPrefix);
```

- [ ] **Step 6: 编译并进行 WinForms 静态检查**

```powershell
dotnet build NavisworksPlugin.csproj -c Release
git diff --check
```

Expected: build succeeds; no whitespace errors. Verify no references to `_lstResultDetails` remain with:

```powershell
rg "_lstResultDetails" SearchDialog.cs
```

Expected: no output.

- [ ] **Step 7: 提交结构化结果页**

```powershell
git add SearchDialog.cs
git commit -m "feat: add structured search result table"
```

---

### Task 5: 实现结果失效、原条件定位和重复对象定位

**Files:**

- Modify: `SearchDialog.cs:355, 616-621, 800-820, 1100-1127`

**Interfaces:**

- Consumes: `SearchResult.Condition.ConditionIndex`、`SearchResult.MatchedItems`。
- Produces: `InvalidateSearchResults(string message)`。
- Produces: `ResultsGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)`。
- Enforces: the conditions grid is read-only so every real edit goes through
  `ConditionsGrid_CellDoubleClick` and invalidates cached results.

- [ ] **Step 1: 统一条件编辑入口并添加结果失效方法**

In `CreateSearchGrid`, set:

```csharp
grid.ReadOnly = true;
```

This prevents visual-only cell edits that never update `_conditions`. Keep
double-click editing and Delete-key deletion as the only table interactions.

```csharp
private void InvalidateSearchResults(string message)
{
    _lastResults = null;
    _lastTotalMatched = 0;
    _lastHideExecuted = false;
    _currentModelPrefix = null;
    _lblResultSummary.Text = message;
    UpdateResultFilterCaptions(Array.Empty<SearchResult>());
    SetActiveResultFilter(SearchResultFilter.All);
    _btnExportResults.Enabled = false;
    _btnCreateSelectionSet.Enabled = false;
}
```

- [ ] **Step 2: 在所有条件变更路径中使旧结果失效**

Change the clear button handler to:

```csharp
_btnClearConditions = MakeToolButton("清空", (s, e) =>
{
    _conditions.Clear();
    RefreshConditionsGrid();
    InvalidateSearchResults("条件已清空，请添加条件后重新执行搜索。");
});
```

At the end of successful `LoadFromXml`, add:

```csharp
InvalidateSearchResults("已导入新的搜索条件，请执行搜索。");
```

After a successful edit in `ConditionsGrid_CellDoubleClick`, add:

```csharp
InvalidateSearchResults("条件已修改，请重新执行搜索。");
```

After a successful add in `BtnAddCondition_Click`, add:

```csharp
InvalidateSearchResults("已添加搜索条件，请重新执行搜索。");
```

After a successful delete in `BtnDeleteCondition_Click`, add:

```csharp
InvalidateSearchResults("已删除搜索条件，请重新执行搜索。");
```

- [ ] **Step 3: 实现结果行双击行为并绑定表格事件**

Immediately after `_resultsGrid = CreateResultsGrid();` in `BuildResultsTab`,
add:

```csharp
_resultsGrid.CellDoubleClick += ResultsGrid_CellDoubleClick;
```

```csharp
private void ResultsGrid_CellDoubleClick(
    object sender,
    DataGridViewCellEventArgs e)
{
    if (e.RowIndex < 0 || e.RowIndex >= _resultsGrid.Rows.Count)
        return;

    var result = _resultsGrid.Rows[e.RowIndex].Tag as SearchResult;
    if (result == null)
        return;

    if (result.Status == SearchResultStatus.Duplicate)
    {
        try
        {
            List<ModelItem> selected = SelectionService.SetSelection(
                _doc,
                result.MatchedItems);
            MessageBox.Show(
                this,
                $"已在 Navisworks 中选中 {selected.Count} 个重复对象。\n" +
                "再次搜索前，请重新确认选择树中的搜索范围。",
                "傑出品·重复对象",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "无法选中重复对象：\n" + ex.Message,
                "傑出品·定位失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        return;
    }

    int conditionIndex = result.Condition.ConditionIndex;
    if (conditionIndex < 0 || conditionIndex >= _conditionsGrid.Rows.Count)
        return;

    _tabControl.SelectedTab = _tabConditions;
    _conditionsGrid.ClearSelection();
    DataGridViewRow conditionRow = _conditionsGrid.Rows[conditionIndex];
    conditionRow.Selected = true;
    _conditionsGrid.CurrentCell = conditionRow.Cells[COL_VALUE];
    _conditionsGrid.FirstDisplayedScrollingRowIndex = conditionIndex;
}
```

- [ ] **Step 4: 编译并检查所有变更入口都调用失效方法**

```powershell
dotnet build NavisworksPlugin.csproj -c Release
rg -n "InvalidateSearchResults" SearchDialog.cs
```

Expected: one method definition and calls from clear、import、edit、add、delete paths.

- [ ] **Step 5: 提交结果生命周期和定位交互**

```powershell
git add SearchDialog.cs
git commit -m "feat: invalidate and locate search results"
```

---

### Task 6: 应用严格隐藏门禁并记录四态诊断

**Files:**

- Modify: `SearchDialog.cs:1134-1403`
- Modify: `DiagnosticLogSession.cs:67-100`
- Modify: `DiagnosticLogExtensions.cs`

**Interfaces:**

- Consumes: `SearchResultPolicy.CanHide`。
- Produces: `DiagnosticLogExtensions.LogSearchResults(DiagnosticLogSession session, IEnumerable<SearchResult> results, bool hideGatePassed)`。
- Produces: `DiagnosticLogSession.LogHideBlocked(string reason)`。

- [ ] **Step 1: 扩展诊断日志的结果明细和门禁记录**

Add to `DiagnosticLogExtensions.cs`:

```csharp
public static void LogSearchResults(
    this DiagnosticLogSession session,
    IEnumerable<SearchResult> results,
    bool hideGatePassed)
{
    if (session == null)
        return;

    List<SearchResult> list = (results ?? Enumerable.Empty<SearchResult>()).ToList();
    session.StartSection("查询条件结果");
    foreach (SearchResult result in list)
    {
        SearchConditionSnapshot condition = result.Condition;
        AppendLine(
            session,
            $"#{condition.DisplayIndex} " +
            $"状态={SearchResultPolicy.GetDisplayName(result.Status)}, " +
            $"分类={Safe(condition.GetCategoryName())}, " +
            $"属性={Safe(condition.GetPropertyName())}, " +
            $"方式={Safe(condition.Test)}, " +
            $"查询值={Safe(condition.Value)}, " +
            $"匹配数={result.MatchCount}, " +
            $"说明={Safe(result.StatusMessage)}");
    }

    AppendLine(session, $"已找到: {list.Count(r => r.Status == SearchResultStatus.Found)}");
    AppendLine(session, $"未找到: {list.Count(r => r.Status == SearchResultStatus.NotFound)}");
    AppendLine(session, $"重复: {list.Count(r => r.Status == SearchResultStatus.Duplicate)}");
    AppendLine(session, $"条件异常: {list.Count(r => r.Status == SearchResultStatus.ConditionInvalid)}");
    AppendLine(session, $"隐藏门禁通过: {(hideGatePassed ? "是" : "否")}");
}
```

Add an overload to `DiagnosticLogSession.cs`:

```csharp
public void LogHideBlocked(string reason)
{
    _sb.AppendLine($"禁止隐藏：{Safe(reason)}");
}
```

- [ ] **Step 2: 搜索完成后计算唯一性门禁并更新结果页调用**

Immediately after `ModelItemMatcher.MatchAll(_doc, scopeRoots, _conditions)` in
`BtnSearch_Click`, replace the old `LogXmlScopeResultStats` call and result
summary block with:

```csharp
List<ModelItem> matchedItemsInScope =
    MergeUniqueItems(results.SelectMany(r => r.MatchedItems));
totalMatched = matchedItemsInScope.Count;
bool resultGatePassed = SearchResultPolicy.CanHide(
    results.Select(r => r.Status));

_lastResults = results;
_lastTotalMatched = totalMatched;
_lastHideExecuted = false;
ShowResults(results, totalMatched, modelPrefix);
diagnosticLog?.LogXmlScopeResultStats(
    results.Sum(r => r.MatchCount),
    matchedItemsInScope.Count,
    0);
diagnosticLog?.LogSearchResults(results, resultGatePassed);
```

- [ ] **Step 3: 将门禁纳入 willHide 判定**

Replace the `willHide` assignment with:

```csharp
bool willHide = !isSelectOnlyMode
    && resultGatePassed
    && totalMatched > 0
    && finalKeepItems.Count > 0
    && actualSelectionCount > 0;
```

- [ ] **Step 4: 在 STR 与隐藏流程之前严格阻断问题结果**

Immediately before the existing `ProtectedKeepResult protectedKeepResult;`
declaration, add this early guard:

```csharp
if (!isSelectOnlyMode && !resultGatePassed)
{
    int notFoundCount = results.Count(
        r => r.Status == SearchResultStatus.NotFound);
    int duplicateCount = results.Count(
        r => r.Status == SearchResultStatus.Duplicate);
    int invalidCount = results.Count(
        r => r.Status == SearchResultStatus.ConditionInvalid);
    string reason =
        $"结果未通过唯一性校验：未找到 {notFoundCount} 条，" +
        $"重复 {duplicateCount} 条，条件异常 {invalidCount} 条。";

    if (matchedItemsInScope.Count > 0)
    {
        List<ModelItem> problemSelection = SelectionService.SetSelection(
            _doc,
            matchedItemsInScope,
            diagnosticLog);
        diagnosticLog?.LogDecision(
            $"问题结果定位选择数量: {problemSelection.Count}");
    }

    diagnosticLog?.LogDecision(reason);
    diagnosticLog?.LogHideBlocked(reason);
    MessageBox.Show(
        this,
        reason +
        "\n\n已阻止隐藏未选中。结果页保留全部问题项；" +
        "修改条件或模型数据后请重新搜索。" +
        "\n再次搜索前，请在选择树中重新选择目标模型范围。",
        "傑出品·唯一性校验未通过",
        MessageBoxButtons.OK,
        MessageBoxIcon.Warning);
    _lastHideExecuted = false;
    _btnExportResults.Enabled = true;
    _btnCreateSelectionSet.Enabled = totalMatched > 0;
    _tabControl.SelectedTab = _tabResults;
    return;
}
```

Leave the existing protected-node lookup and all following code in place. The
guard returns only for non-select-only runs with a problem result, so the
current all-unique order remains exactly: STR warning, final keep-set
construction, selection write, safety checks, then hide confirmation. The
`finally` block still writes diagnostics and restores the cursor/search button.

- [ ] **Step 5: 保持结果按钮与结果有效性一致**

At the successful end of `BtnSearch_Click`, use:

```csharp
_lastHideExecuted = hideExecuted;
_btnExportResults.Enabled = true;
_btnCreateSelectionSet.Enabled = totalMatched > 0;
_tabControl.SelectedTab = _tabResults;
```

- [ ] **Step 6: 运行策略测试、编译和隐藏调用静态检查**

```powershell
dotnet test tests\JiePinPai.Navisworks.Tests\JiePinPai.Navisworks.Tests.csproj -c Release
dotnet build NavisworksPlugin.csproj -c Release
rg -n "HideService\.HideUnselected" SearchDialog.cs
```

Expected: all tests pass; build succeeds; exactly one
`HideService.HideUnselected` call remains in the existing hide-confirmation
branch, structurally reachable only from the `resultGatePassed` path.

- [ ] **Step 7: 提交严格门禁与诊断日志**

```powershell
git add SearchDialog.cs DiagnosticLogSession.cs DiagnosticLogExtensions.cs
git commit -m "feat: block hiding when unique validation fails"
```

---

### Task 7: 扩展 CSV 与普通结果日志

**Files:**

- Modify: `SearchDialog.cs:1433-1470`
- Modify: `LogService.cs:33-97`

**Interfaces:**

- Consumes: complete `SearchResult` snapshots and statuses.
- Produces: CSV columns for condition index, status, full condition, count, message and object details.
- Produces: four-state legacy text log.

- [ ] **Step 1: 添加可靠 CSV 转义助手**

Add to `SearchDialog.cs`:

```csharp
private static string EscapeCsv(string value)
{
    string safe = value ?? string.Empty;
    return "\"" + safe.Replace("\"", "\"\"") + "\"";
}

private static string FormatMatchedItems(IEnumerable<ModelItem> items)
{
    return string.Join(
        "; ",
        (items ?? Enumerable.Empty<ModelItem>()).Select(item =>
            $"{item.DisplayName ?? "（无名称）"} [{item.InstanceGuid}]"));
}
```

- [ ] **Step 2: 替换 CSV 写入循环**

Replace the writer body in `BtnExportResults_Click` with:

```csharp
writer.WriteLine(
    "条件序号,状态,分类,属性名,匹配方式,查询值,匹配数,说明,匹配对象详情");
foreach (SearchResult result in _lastResults)
{
    SearchConditionSnapshot condition = result.Condition;
    string[] columns =
    {
        condition.DisplayIndex.ToString(),
        SearchResultPolicy.GetDisplayName(result.Status),
        condition.GetCategoryName(),
        condition.GetPropertyName(),
        condition.Test,
        condition.Value,
        result.MatchCount.ToString(),
        result.StatusMessage,
        FormatMatchedItems(result.MatchedItems),
    };
    writer.WriteLine(string.Join(",", columns.Select(EscapeCsv)));
}
```

- [ ] **Step 3: 让普通日志使用四态结果**

Replace the result loop and summary counters in `LogService.WriteLog` with:

```csharp
int foundCount = 0;
int notFoundCount = 0;
int duplicateCount = 0;
int invalidCount = 0;

sb.AppendLine("--- 查询结果 ---");
foreach (SearchResult result in results)
{
    switch (result.Status)
    {
        case SearchResultStatus.Found: foundCount++; break;
        case SearchResultStatus.NotFound: notFoundCount++; break;
        case SearchResultStatus.Duplicate: duplicateCount++; break;
        case SearchResultStatus.ConditionInvalid: invalidCount++; break;
    }

    SearchConditionSnapshot condition = result.Condition;
    sb.AppendLine(
        $"[{SearchResultPolicy.GetDisplayName(result.Status)}] " +
        $"#{condition.DisplayIndex} " +
        $"{condition.GetCategoryName()} / {condition.GetPropertyName()} / " +
        $"{condition.Test} / {condition.Value} → " +
        $"匹配 {result.MatchCount} 个对象；{result.StatusMessage}");
}

sb.AppendLine(string.Empty);
sb.AppendLine("--- 汇总 ---");
sb.AppendLine($"总条件数: {results.Count}");
sb.AppendLine($"已找到: {foundCount}");
sb.AppendLine($"未找到: {notFoundCount}");
sb.AppendLine($"重复: {duplicateCount}");
sb.AppendLine($"条件异常: {invalidCount}");
sb.AppendLine($"总计匹配对象数（去重）: {totalMatchedCount}");
sb.AppendLine($"唯一性校验: {(SearchResultPolicy.CanHide(results.Select(r => r.Status)) ? "通过" : "未通过")}");
```

- [ ] **Step 4: 让正常完成和门禁阻断路径都写普通日志**

Add this helper to `SearchDialog.cs`:

```csharp
private void WriteLegacyResultLog(
    List<SearchResult> results,
    int totalMatched,
    bool hideExecuted)
{
    if (string.IsNullOrWhiteSpace(_currentXmlPath))
        return;

    LogService.WriteLog(
        _currentXmlPath,
        results,
        totalMatched,
        hideExecuted);
}
```

In the uniqueness-gate early-return branch from Task 6, immediately before
`return`, add:

```csharp
WriteLegacyResultLog(results, totalMatched, false);
```

At the successful end of `BtnSearch_Click`, before switching to the results
tab, add:

```csharp
WriteLegacyResultLog(results, totalMatched, hideExecuted);
```

- [ ] **Step 5: 编译并静态检查导出字段**

```powershell
dotnet build NavisworksPlugin.csproj -c Release
rg -n "条件序号,状态,分类,属性名,匹配方式,查询值,匹配数,说明,匹配对象详情" SearchDialog.cs
rg -n "唯一性校验" LogService.cs
```

Expected: build succeeds and each search finds exactly one expected header/log line.

- [ ] **Step 6: 提交导出与日志**

```powershell
git add SearchDialog.cs LogService.cs
git commit -m "feat: export complete search result states"
```

---

### Task 8: 文档、构建、部署与 Navisworks 验收

**Files:**

- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/Obsidian/傑出品-Navisworks查找插件-项目知识库.md`

**Interfaces:**

- Consumes: completed feature behavior.
- Produces: user-facing usage rules and a repeatable acceptance record.

- [ ] **Step 1: 更新 README 的结果状态和隐藏安全说明**

Add a “唯一性校验与结果可视化” section containing these exact rules:

```markdown
### 唯一性校验与结果可视化

每条查询条件在当前选定模型范围内必须恰好匹配 1 个对象：

- 匹配 0 个：未找到；
- 匹配 1 个：已找到；
- 匹配 2 个及以上：重复；
- 条件无法执行：条件异常。

结果页可以筛选全部、问题项、已找到、未找到、重复和条件异常。存在任意问题项时，插件严格阻止“隐藏未选中”；处理条件或模型数据并重新搜索，直到全部条件唯一匹配后才允许隐藏。
```

- [ ] **Step 2: 更新 CHANGELOG**

Add an unreleased entry:

```markdown
## 未发布

### 新增
- 结果页结构化表格与四态筛选：已找到、未找到、重复、条件异常。
- 每条查询条件必须唯一匹配 1 个对象。
- 双击重复结果可在 Navisworks 中选中全部重复对象。
- CSV 和诊断日志输出完整条件、状态、匹配数量与说明。

### 安全
- 任意未找到、重复或条件异常都会严格阻止隐藏未选中。
- 条件变更后旧结果立即失效，不能继续导出或操作旧结果。
```

- [ ] **Step 3: 同步 Obsidian 项目知识文档**

Update the result-state, safety invariant and reusable-knowledge sections so they include:

```markdown
搜索已经从“是否存在”判断升级为“基数是否符合约束”：0 表示缺失，1 表示唯一有效，大于等于 2 表示模型值重复。比较方式决定如何匹配，唯一性约束决定匹配数量是否合法，两者不能混为一谈。
```

- [ ] **Step 4: 运行完整自动验证**

```powershell
dotnet test tests\JiePinPai.Navisworks.Tests\JiePinPai.Navisworks.Tests.csproj -c Release
dotnet build NavisworksPlugin.csproj -c Release
git diff --check
```

Expected: all tests pass, plugin build succeeds with 0 errors, and diff check is clean.

- [ ] **Step 5: 检查提交范围**

```powershell
git status --short
git diff --stat
git diff -- HideServiceFixed.cs Properties\AssemblyInfo.cs
```

Expected: pre-existing user changes remain intact; `.codegraph/`、`.superpowers/`、`bin/`、`obj/` are not staged.

- [ ] **Step 6: 在 Navisworks 关闭时安装 Release DLL**

```powershell
$env:NAVISWORKS_2023_PATH = 'F:\Navisworks\Navisworks Manage 2023'
.\scripts\install_2023.ps1
```

Expected: installer reports the destination under `F:\Navisworks\Navisworks Manage 2023\Plugins\傑出品NavisworksPlugin` and copies both DLL and manifest.

- [ ] **Step 7: 执行 Navisworks 手工验收矩阵**

Use one selected model scope and run these cases:

| Case | Input | Expected result |
|---|---|---|
| Unique | one condition matching exactly one object | green “已找到”; hide confirmation remains available |
| Missing | one valid condition matching zero objects | red “未找到”; hide is blocked |
| Duplicate | one condition matching two or more objects | orange “重复”; double-click selects all duplicate objects; hide is blocked |
| Invalid category | property without category and discovery fails | yellow “条件异常”; reason is visible; hide is blocked |
| Mixed | one unique, one missing, one duplicate | default “问题项” filter; summary counts are exact; hide is blocked |
| All unique | several conditions each matching exactly one object | default “全部”; STR protection and hide workflow behave as before |
| Repeated input | two identical conditions that each resolve to the same single object | both rows are “已找到”; this is not a model-value duplicate |
| Stale result | edit a condition after search | result clears; export and selection-set buttons disable |
| Export | query values contain comma and quote characters | CSV opens with intact columns and escaped values |
| DPI | Windows scaling at 100%, 125%, 150% | no text clipping or overlapping controls |

- [ ] **Step 8: 提交文档与验收记录**

```powershell
git add README.md CHANGELOG.md docs\Obsidian\傑出品-Navisworks查找插件-项目知识库.md
git commit -m "docs: document unique result validation"
```

- [ ] **Step 9: 最终提交审计**

```powershell
git log --oneline -8
git status --short
```

Expected: feature commits are present in task order; only pre-existing user changes and ignored/generated local artifacts remain outside commits.

## Implementation Completion Criteria

- Pure policy, snapshot and validator tests pass.
- Release plugin builds against Navisworks Manage 2023 API.
- Every condition produces exactly one of four states.
- Duplicate means one condition matched multiple distinct model objects.
- Problem states are immediately visible and filterable.
- Duplicate rows can select their matched objects.
- Any problem state blocks hiding without override.
- Condition changes invalidate all cached result actions.
- CSV and logs preserve full condition context.
- Existing scope, selection merge, STR protection and hide implementation remain behaviorally intact when all conditions are unique.
