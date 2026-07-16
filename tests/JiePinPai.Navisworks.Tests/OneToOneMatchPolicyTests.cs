using System;
using System.Collections.Generic;
using JiePinPai.Navisworks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class OneToOneMatchPolicyTests
    {
        [TestMethod]
        public void FindRepeatedSingleMatches_MarksOnlyLaterOwners()
        {
            IReadOnlyList<string>[] matches =
            {
                new[] { "A" },
                new[] { "B" },
                new[] { "A" },
                Array.Empty<string>(),
                new[] { "C", "D" },
                new[] { "B" },
            };

            Dictionary<int, int> repeatedOwners =
                OneToOneMatchPolicy.FindRepeatedSingleMatches(matches);

            Assert.AreEqual(2, repeatedOwners.Count);
            Assert.AreEqual(0, repeatedOwners[2]);
            Assert.AreEqual(1, repeatedOwners[5]);
        }

        [TestMethod]
        public void FindRepeatedSingleMatches_UsesProvidedComparer()
        {
            IReadOnlyList<string>[] matches =
            {
                new[] { "SPS-M12-010" },
                new[] { "sps-m12-010" },
            };

            Dictionary<int, int> repeatedOwners =
                OneToOneMatchPolicy.FindRepeatedSingleMatches(
                    matches,
                    StringComparer.OrdinalIgnoreCase);

            Assert.AreEqual(1, repeatedOwners.Count);
            Assert.AreEqual(0, repeatedOwners[1]);
        }
    }
}
