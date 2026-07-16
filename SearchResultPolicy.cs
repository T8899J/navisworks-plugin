using System;
using System.Collections.Generic;

namespace JiePinPai.Navisworks
{
    public static class SearchResultPolicy
    {
        public static SearchResultStatus Classify(
            bool conditionExecuted,
            int matchCount)
        {
            if (matchCount < 0)
                throw new ArgumentOutOfRangeException(nameof(matchCount));

            if (!conditionExecuted)
                return SearchResultStatus.ConditionInvalid;
            if (matchCount == 0)
                return SearchResultStatus.NotFound;
            if (matchCount == 1)
                return SearchResultStatus.Found;
            return SearchResultStatus.Duplicate;
        }

        public static bool CanHide(IEnumerable<SearchResultStatus> statuses)
        {
            if (statuses == null)
                return false;

            bool hasAny = false;
            foreach (SearchResultStatus status in statuses)
            {
                hasAny = true;
                if (status != SearchResultStatus.Found)
                    return false;
            }

            return hasAny;
        }

        public static bool MatchesFilter(
            SearchResultStatus status,
            SearchResultFilter filter)
        {
            switch (filter)
            {
                case SearchResultFilter.All:
                    return true;
                case SearchResultFilter.Problems:
                    return status != SearchResultStatus.Found;
                case SearchResultFilter.Found:
                    return status == SearchResultStatus.Found;
                case SearchResultFilter.NotFound:
                    return status == SearchResultStatus.NotFound;
                case SearchResultFilter.Duplicate:
                    return status == SearchResultStatus.Duplicate;
                case SearchResultFilter.ConditionInvalid:
                    return status == SearchResultStatus.ConditionInvalid;
                default:
                    throw new ArgumentOutOfRangeException(nameof(filter));
            }
        }

        public static string GetDisplayName(SearchResultStatus status)
        {
            switch (status)
            {
                case SearchResultStatus.Found:
                    return "已找到";
                case SearchResultStatus.NotFound:
                    return "未找到";
                case SearchResultStatus.Duplicate:
                    return "重复";
                case SearchResultStatus.ConditionInvalid:
                    return "条件异常";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }
    }
}
