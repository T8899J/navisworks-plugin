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
                SearchResultStatus.Found,
                SearchResultStatus.Duplicate,
            }));
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
