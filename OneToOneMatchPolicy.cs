using System;
using System.Collections.Generic;

namespace JiePinPai.Navisworks
{
    public static class OneToOneMatchPolicy
    {
        public static Dictionary<int, int> FindRepeatedSingleMatches<T>(
            IReadOnlyList<IReadOnlyList<T>> matchesByCondition,
            IEqualityComparer<T> comparer = null)
        {
            if (matchesByCondition == null)
                throw new ArgumentNullException(nameof(matchesByCondition));

            var firstOwnerByItem = new Dictionary<T, int>(
                comparer ?? EqualityComparer<T>.Default);
            var repeatedOwners = new Dictionary<int, int>();

            for (int conditionIndex = 0;
                conditionIndex < matchesByCondition.Count;
                conditionIndex++)
            {
                IReadOnlyList<T> matches = matchesByCondition[conditionIndex];
                if (matches == null || matches.Count != 1)
                    continue;

                T item = matches[0];
                if (ReferenceEquals(item, null))
                    continue;

                if (firstOwnerByItem.TryGetValue(item, out int firstConditionIndex))
                {
                    repeatedOwners[conditionIndex] = firstConditionIndex;
                    continue;
                }

                firstOwnerByItem[item] = conditionIndex;
            }

            return repeatedOwners;
        }
    }
}
