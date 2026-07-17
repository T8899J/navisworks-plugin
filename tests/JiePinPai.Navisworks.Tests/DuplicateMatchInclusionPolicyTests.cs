using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class DuplicateMatchInclusionPolicyTests
    {
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
