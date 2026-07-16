using JiePinPai.Navisworks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class SelectionEquivalencePolicyTests
    {
        [TestMethod]
        public void AreEquivalent_SameSetInDifferentOrder_ReturnsTrue()
        {
            Assert.IsTrue(SelectionEquivalencePolicy.AreEquivalent(
                new[] { "A", "B", "C" },
                new[] { "C", "A", "B" }));
        }

        [TestMethod]
        public void AreEquivalent_MissingOrExtraItem_ReturnsFalse()
        {
            Assert.IsFalse(SelectionEquivalencePolicy.AreEquivalent(
                new[] { "A", "B" },
                new[] { "A" }));
            Assert.IsFalse(SelectionEquivalencePolicy.AreEquivalent(
                new[] { "A" },
                new[] { "A", "B" }));
        }

        [TestMethod]
        public void AreEquivalent_DuplicateItemsDoNotChangeSetMembership()
        {
            Assert.IsTrue(SelectionEquivalencePolicy.AreEquivalent(
                new[] { "A", "B", "B" },
                new[] { "B", "A" }));
        }

        [TestMethod]
        public void AreEquivalent_NullSequenceReturnsFalseAndNullElementIsCompared()
        {
            Assert.IsFalse(SelectionEquivalencePolicy.AreEquivalent<string>(null, new[] { "A" }));
            Assert.IsFalse(SelectionEquivalencePolicy.AreEquivalent(new[] { "A" }, null));
            Assert.IsTrue(SelectionEquivalencePolicy.AreEquivalent(
                new string[] { "A", null },
                new string[] { null, "A" }));
            Assert.IsFalse(SelectionEquivalencePolicy.AreEquivalent(
                new string[] { "A", null },
                new[] { "A" }));
        }
    }
}
