# Task 1 报告：建立纯逻辑结果策略与测试基线

## Implementation

- 新增 `SearchResultStatus` 四态枚举：`Found`、`NotFound`、`Duplicate`、`ConditionInvalid`。
- 新增 `SearchResultFilter`：`All`、`Problems`、`Found`、`NotFound`、`Duplicate`、`ConditionInvalid`。
- 新增纯逻辑 `SearchResultPolicy`：
  - `Classify(bool conditionExecuted, int matchCount)` 按未执行、零个、恰好一个、多于一个分类，并拒绝负数。
  - `CanHide(IEnumerable<SearchResultStatus>)` 要求至少一个结果且全部为 `Found`。
  - `MatchesFilter(...)` 实现全部、问题及各状态筛选。
  - `GetDisplayName(...)` 返回稳定中文标签。
- 新增 net48 / C# 7.3 MSTest 测试项目，并通过 Compile Link 复用两个纯逻辑生产文件。
- 将两个生产文件加入 `NavisworksPlugin.csproj` 编译项。

## Files changed

- `SearchResultStatus.cs`
- `SearchResultPolicy.cs`
- `NavisworksPlugin.csproj`
- `tests/JiePinPai.Navisworks.Tests/JiePinPai.Navisworks.Tests.csproj`
- `tests/JiePinPai.Navisworks.Tests/SearchResultPolicyTests.cs`
- `.superpowers/sdd/task-1-report.md`

## RED evidence

Command:

```powershell
dotnet test tests\JiePinPai.Navisworks.Tests\JiePinPai.Navisworks.Tests.csproj -c Release
```

After using the repository's SDK PATH, the test project failed before production types existed with restore error `NU1301: 无法加载源 https://api.nuget.org/v3/index.json 的服务索引。` Network permission was requested and restore then succeeded with the specified package versions.

## GREEN evidence

Command:

```powershell
$env:PATH = "C:\Users\BOY\AppData\Local\Microsoft\dotnet;" + $env:PATH
dotnet test tests\JiePinPai.Navisworks.Tests\JiePinPai.Navisworks.Tests.csproj -c Release --no-restore
```

Result: passed 17, failed 0, skipped 0, total 17.

The specified MSTest 4.3.2 package does not expose `Assert.ThrowsException`; the test uses the equivalent `Assert.Throws` API while preserving the required exception assertion and package version.

## Release build

Command:

```powershell
$env:PATH = "C:\Users\BOY\AppData\Local\Microsoft\dotnet;" + $env:PATH
dotnet build NavisworksPlugin.csproj -c Release --no-restore
```

Result: succeeded, 0 warnings, 0 errors. Output: `bin\Release\傑出品NavisworksPlugin.dll`.

## Self-review

- Confirmed the policy has no Navisworks, COM, UI, or filesystem dependency.
- Confirmed negative counts and invalid enum values are rejected with `ArgumentOutOfRangeException`.
- Confirmed `CanHide` rejects null, empty, and any non-`Found` status.
- Confirmed `git diff --check` reports no whitespace errors.
- Existing unrelated untracked/generated files were not staged or modified.

## Commit

`test: define unique search result policy` (final commit; short SHA is reported with the task result).

## Concerns

- MSTest reports three `MSTEST0044` warnings because `DataTestMethod` is deprecated in the specified framework version. Tests pass and the brief's test structure was retained.
- The initial plain `dotnet` command could not find the SDK until the repository-documented PATH was applied; subsequent test and build commands succeeded.
