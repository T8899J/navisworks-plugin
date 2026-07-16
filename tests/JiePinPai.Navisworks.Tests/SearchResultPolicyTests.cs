using System;
using JiePinPai.Navisworks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class SearchResultPolicyTests
    {
        [TestMethod]
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
            Assert.Throws<ArgumentOutOfRangeException>(
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
                SearchResultStatus.NotFound,
            }));
            Assert.IsFalse(SearchResultPolicy.CanHide(new[]
            {
                SearchResultStatus.ConditionInvalid,
            }));
            Assert.IsFalse(SearchResultPolicy.CanHide(new[]
            {
                SearchResultStatus.Found,
                SearchResultStatus.Duplicate,
            }));
        }

        [TestMethod]
        public void CanProceedWithHide_AllowsProblemsOnlyAfterExplicitOverride()
        {
            var validStatuses = new[]
            {
                SearchResultStatus.Found,
                SearchResultStatus.Found,
            };
            var problemStatuses = new[]
            {
                SearchResultStatus.Found,
                SearchResultStatus.Duplicate,
                SearchResultStatus.NotFound,
            };

            Assert.IsTrue(SearchResultPolicy.CanProceedWithHide(validStatuses, false));
            Assert.IsTrue(SearchResultPolicy.CanProceedWithHide(validStatuses, true));
            Assert.IsFalse(SearchResultPolicy.CanProceedWithHide(problemStatuses, false));
            Assert.IsTrue(SearchResultPolicy.CanProceedWithHide(problemStatuses, true));
            Assert.IsFalse(SearchResultPolicy.CanProceedWithHide(null, true));
            Assert.IsFalse(SearchResultPolicy.CanProceedWithHide(
                Array.Empty<SearchResultStatus>(),
                true));
        }

        [TestMethod]
        public void CanOfferManualHide_RequiresCurrentMatchedContextAndUnusedAction()
        {
            var statuses = new[]
            {
                SearchResultStatus.Found,
                SearchResultStatus.Duplicate,
            };

            Assert.IsTrue(SearchResultPolicy.CanOfferManualHide(
                statuses,
                totalMatchedCount: 2,
                hasScopeSnapshot: true,
                hideAlreadyExecuted: false));
            Assert.IsFalse(SearchResultPolicy.CanOfferManualHide(
                statuses,
                totalMatchedCount: 0,
                hasScopeSnapshot: true,
                hideAlreadyExecuted: false));
            Assert.IsFalse(SearchResultPolicy.CanOfferManualHide(
                statuses,
                totalMatchedCount: 2,
                hasScopeSnapshot: false,
                hideAlreadyExecuted: false));
            Assert.IsFalse(SearchResultPolicy.CanOfferManualHide(
                statuses,
                totalMatchedCount: 2,
                hasScopeSnapshot: true,
                hideAlreadyExecuted: true));
            Assert.IsFalse(SearchResultPolicy.CanOfferManualHide(
                null,
                totalMatchedCount: 2,
                hasScopeSnapshot: true,
                hideAlreadyExecuted: false));
            Assert.IsFalse(SearchResultPolicy.CanOfferManualHide(
                Array.Empty<SearchResultStatus>(),
                totalMatchedCount: 2,
                hasScopeSnapshot: true,
                hideAlreadyExecuted: false));
        }

        [TestMethod]
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

        [TestMethod]
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
