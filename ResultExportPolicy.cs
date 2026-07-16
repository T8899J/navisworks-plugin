using System;
using System.Collections.Generic;

namespace JiePinPai.Navisworks
{
    public enum ResultExportScope
    {
        Checked,
        CurrentFilter,
        All,
    }

    public static class ResultExportPolicy
    {
        public static HashSet<int> SetVisibleSelection(
            IEnumerable<int> checkedIds,
            IEnumerable<int> visibleIds,
            bool selected)
        {
            var result = new HashSet<int>(checkedIds ?? Array.Empty<int>());
            foreach (int id in visibleIds ?? Array.Empty<int>())
            {
                if (selected)
                    result.Add(id);
                else
                    result.Remove(id);
            }

            return result;
        }

        public static HashSet<int> ResolveExportIds(
            ResultExportScope scope,
            IEnumerable<int> allIds,
            IEnumerable<int> visibleIds,
            IEnumerable<int> checkedIds)
        {
            var all = new HashSet<int>(allIds ?? Array.Empty<int>());
            IEnumerable<int> candidates;
            switch (scope)
            {
                case ResultExportScope.Checked:
                    candidates = checkedIds;
                    break;
                case ResultExportScope.CurrentFilter:
                    candidates = visibleIds;
                    break;
                case ResultExportScope.All:
                    candidates = all;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scope));
            }

            var result = new HashSet<int>();
            foreach (int id in candidates ?? Array.Empty<int>())
            {
                if (all.Contains(id))
                    result.Add(id);
            }

            return result;
        }
    }
}
