using System;
using System.Collections.Generic;
using JiePinPai.Navisworks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class ResultExportPolicyTests
    {
        [TestMethod]
        public void SetVisibleSelection_SelectsVisibleAndPreservesHiddenChecks()
        {
            HashSet<int> result = ResultExportPolicy.SetVisibleSelection(
                new[] { 2, 8 },
                new[] { 1, 2, 3 },
                true);

            CollectionAssert.AreEquivalent(
                new[] { 1, 2, 3, 8 },
                new List<int>(result));
        }

        [TestMethod]
        public void SetVisibleSelection_ClearsOnlyVisibleChecks()
        {
            HashSet<int> result = ResultExportPolicy.SetVisibleSelection(
                new[] { 1, 2, 8 },
                new[] { 1, 2, 3 },
                false);

            CollectionAssert.AreEquivalent(
                new[] { 8 },
                new List<int>(result));
        }

        [TestMethod]
        public void ResolveExportIds_UsesExplicitScopeAndDropsStaleChecks()
        {
            int[] all = { 1, 2, 3, 4 };
            int[] visible = { 2, 3 };
            int[] checkedIds = { 1, 3, 99 };

            CollectionAssert.AreEquivalent(
                new[] { 1, 3 },
                new List<int>(ResultExportPolicy.ResolveExportIds(
                    ResultExportScope.Checked,
                    all,
                    visible,
                    checkedIds)));
            CollectionAssert.AreEquivalent(
                new[] { 2, 3 },
                new List<int>(ResultExportPolicy.ResolveExportIds(
                    ResultExportScope.CurrentFilter,
                    all,
                    visible,
                    checkedIds)));
            CollectionAssert.AreEquivalent(
                all,
                new List<int>(ResultExportPolicy.ResolveExportIds(
                    ResultExportScope.All,
                    all,
                    visible,
                    checkedIds)));
        }

        [TestMethod]
        public void ResolveExportIds_RejectsUnknownScope()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ResultExportPolicy.ResolveExportIds(
                    (ResultExportScope)999,
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    Array.Empty<int>()));
        }
    }
}
