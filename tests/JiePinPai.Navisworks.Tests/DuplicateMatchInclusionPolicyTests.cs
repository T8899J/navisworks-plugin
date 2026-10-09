using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class DuplicateMatchInclusionPolicyTests
    {
        [TestMethod]
        public void SetInclusion_SelectAllAddsEveryDuplicateAndDeduplicatesObjects()
        {
            var duplicates = new Dictionary<int, IReadOnlyList<string>>
            {
                [2] = new[] { "A", "B", "B" },
                [3] = new[] { "B", "C" },
                [4] = new[] { "A" },
            };
            var original = new HashSet<int> { 3 };
            HashSet<int> selected = DuplicateMatchInclusionPolicy.SetInclusion(
                original, duplicates.Keys, true);

            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, selected.ToList());
            CollectionAssert.AreEquivalent(new[] { 3 }, original.ToList());
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" },
                DuplicateMatchInclusionPolicy.ResolveEffectiveItems(
                    new[] { "A" }, duplicates, selected));
        }

        [TestMethod]
        public void SetInclusion_ClearAllPreservesFoundObjects()
        {
            var duplicates = new Dictionary<int, IReadOnlyList<string>>
            {
                [2] = new[] { "A", "B" },
                [3] = new[] { "C" },
            };
            HashSet<int> selected = DuplicateMatchInclusionPolicy.SetInclusion(
                duplicates.Keys, duplicates.Keys, false);

            Assert.AreEqual(0, selected.Count);
            CollectionAssert.AreEqual(new[] { "A" },
                DuplicateMatchInclusionPolicy.ResolveEffectiveItems(
                    new[] { "A" }, duplicates, selected));
        }

        [TestMethod]
        public void SetInclusion_VisibleScopePreservesSelectionsOutsideScope()
        {
            HashSet<int> selected = DuplicateMatchInclusionPolicy.SetInclusion(
                new[] { 2, 8 }, new[] { 2, 3 }, true);
            CollectionAssert.AreEquivalent(new[] { 2, 3, 8 }, selected.ToList());

            selected = DuplicateMatchInclusionPolicy.SetInclusion(
                selected, new[] { 2, 3 }, false);
            CollectionAssert.AreEquivalent(new[] { 8 }, selected.ToList());
        }

        [TestMethod]
        public void SetInclusion_SingleAdjustmentAfterSelectAllPreservesOtherDuplicates()
        {
            HashSet<int> selected = DuplicateMatchInclusionPolicy.SetInclusion(
                null, new[] { 2, 3, 4 }, true);
            selected = DuplicateMatchInclusionPolicy.SetInclusion(selected, new[] { 3 }, false);
            CollectionAssert.AreEquivalent(new[] { 2, 4 }, selected.ToList());
            selected = DuplicateMatchInclusionPolicy.SetInclusion(selected, new[] { 3 }, true);
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, selected.ToList());
        }

        [TestMethod]
        public void SetInclusion_EmptyScopeDoesNotChangeExistingSelections()
        {
            CollectionAssert.AreEquivalent(new[] { 2 },
                DuplicateMatchInclusionPolicy.SetInclusion(new[] { 2 }, null, true).ToList());
            CollectionAssert.AreEquivalent(new[] { 2 },
                DuplicateMatchInclusionPolicy.SetInclusion(new[] { 2 }, new int[0], false).ToList());
            Assert.AreEqual(0, DuplicateMatchInclusionPolicy.SetInclusion(null, null, false).Count);
        }

        [TestMethod]
        public void SetInclusion_LargeBatchRetainsAllIdsAndDistinctObjects()
        {
            var duplicates = Enumerable.Range(1, 4000).ToDictionary(
                id => id, id => (IReadOnlyList<string>)new[] { "Shared", "Object-" + id });
            HashSet<int> selected = DuplicateMatchInclusionPolicy.SetInclusion(
                null, duplicates.Keys, true);
            List<string> items = DuplicateMatchInclusionPolicy.ResolveEffectiveItems(
                new[] { "Shared" }, duplicates, selected);

            Assert.AreEqual(4000, selected.Count);
            Assert.AreEqual(4001, items.Count);
            Assert.AreEqual(1, items.Count(item => item == "Shared"));
        }

        [TestMethod]
        public void ResolveEffectiveItems_DefaultExcludesDuplicateContributions()
        {
            var duplicateItems = new Dictionary<int, IReadOnlyList<string>>
            {
                [2] = new List<string> { "B", "C" },
            };

            List<string> result = DuplicateMatchInclusionPolicy.ResolveEffectiveItems(
                new[] { "A" },
                duplicateItems,
                new int[0]);

            CollectionAssert.AreEqual(new[] { "A" }, result);
        }

        [TestMethod]
        public void ResolveEffectiveItems_IncludedDuplicateAddsItsObjectsOnce()
        {
            var duplicateItems = new Dictionary<int, IReadOnlyList<string>>
            {
                [2] = new List<string> { "A", "B", "B" },
                [3] = new List<string> { "C" },
            };

            List<string> result = DuplicateMatchInclusionPolicy.ResolveEffectiveItems(
                new[] { "A" },
                duplicateItems,
                new[] { 2 });

            CollectionAssert.AreEqual(new[] { "A", "B" }, result);
        }

        [TestMethod]
        public void ResolveEffectiveItems_ExcludedDuplicateCannotRemoveFoundObject()
        {
            var duplicateItems = new Dictionary<int, IReadOnlyList<string>>
            {
                [2] = new List<string> { "A" },
            };

            List<string> result = DuplicateMatchInclusionPolicy.ResolveEffectiveItems(
                new[] { "A" },
                duplicateItems,
                new int[0]);

            CollectionAssert.AreEqual(new[] { "A" }, result);
        }

        [TestMethod]
        public void ResolveEffectiveItems_IgnoresUnknownIncludedResultIds()
        {
            var duplicateItems = new Dictionary<int, IReadOnlyList<string>>
            {
                [2] = new List<string> { "B" },
            };

            List<string> result = DuplicateMatchInclusionPolicy.ResolveEffectiveItems(
                new[] { "A" },
                duplicateItems,
                new[] { 99 });

            CollectionAssert.AreEqual(new[] { "A" }, result);
        }
    }
}
