using System.Collections.Generic;

namespace JiePinPai.Navisworks
{
    public static class SelectionEquivalencePolicy
    {
        public static bool AreEquivalent<T>(
            IEnumerable<T> expected,
            IEnumerable<T> actual)
        {
            if (expected == null || actual == null)
                return false;

            var expectedSet = new HashSet<T>(expected);
            return expectedSet.SetEquals(actual);
        }
    }
}
