using System;
using System.Collections.Generic;

namespace JiePinPai.Navisworks
{
    public static class DuplicateMatchInclusionPolicy
    {
        public static List<T> ResolveEffectiveItems<T>(
            IEnumerable<T> foundItems,
            IReadOnlyDictionary<int, IReadOnlyList<T>> duplicateItemsByResult,
            IEnumerable<int> includedDuplicateResultIds,
            IEqualityComparer<T> comparer = null)
        {
            var result = new List<T>();
            var seen = new HashSet<T>(comparer ?? EqualityComparer<T>.Default);

            AddUnique(result, seen, foundItems);

            if (duplicateItemsByResult == null)
                return result;

            foreach (int resultId in includedDuplicateResultIds ?? Array.Empty<int>())
            {
                if (duplicateItemsByResult.TryGetValue(
                    resultId,
                    out IReadOnlyList<T> duplicateItems))
                {
                    AddUnique(result, seen, duplicateItems);
                }
            }

            return result;
        }

        private static void AddUnique<T>(
            ICollection<T> destination,
            ISet<T> seen,
            IEnumerable<T> items)
        {
            foreach (T item in items ?? Array.Empty<T>())
            {
                if (!ReferenceEquals(item, null) && seen.Add(item))
                    destination.Add(item);
            }
        }
    }
}
