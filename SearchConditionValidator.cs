using System;

namespace JiePinPai.Navisworks
{
    public static class SearchConditionValidator
    {
        public static bool TryValidate(
            SearchCondition condition,
            out string errorMessage)
        {
            if (condition == null)
            {
                errorMessage = "搜索条件为空。";
                return false;
            }

            string propertyName = !string.IsNullOrWhiteSpace(condition.PropertyDisplay)
                ? condition.PropertyDisplay
                : condition.PropertyInternal;
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                errorMessage = "属性名不能为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(condition.Value))
            {
                errorMessage = "查询值不能为空。";
                return false;
            }

            bool supported = string.Equals(
                    condition.Test,
                    "equals",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    condition.Test,
                    "contains",
                    StringComparison.OrdinalIgnoreCase);
            if (!supported)
            {
                errorMessage = "匹配方式仅支持 equals 或 contains。";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
