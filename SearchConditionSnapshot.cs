using System;

namespace JiePinPai.Navisworks
{
    public sealed class SearchConditionSnapshot
    {
        public int ConditionIndex { get; }
        public int DisplayIndex => ConditionIndex + 1;
        public string CategoryInternal { get; }
        public string CategoryDisplay { get; }
        public string PropertyInternal { get; }
        public string PropertyDisplay { get; }
        public string Test { get; }
        public string Value { get; }

        private SearchConditionSnapshot(
            int conditionIndex,
            SearchCondition source)
        {
            ConditionIndex = conditionIndex;
            CategoryInternal = source?.CategoryInternal ?? string.Empty;
            CategoryDisplay = source?.CategoryDisplay ?? string.Empty;
            PropertyInternal = source?.PropertyInternal ?? string.Empty;
            PropertyDisplay = source?.PropertyDisplay ?? string.Empty;
            Test = source?.Test ?? string.Empty;
            Value = source?.Value ?? string.Empty;
        }

        public static SearchConditionSnapshot From(
            int conditionIndex,
            SearchCondition source)
        {
            if (conditionIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(conditionIndex));

            return new SearchConditionSnapshot(conditionIndex, source);
        }

        public string GetCategoryName()
        {
            if (!string.IsNullOrWhiteSpace(CategoryDisplay))
                return CategoryDisplay;
            if (!string.IsNullOrWhiteSpace(CategoryInternal))
                return CategoryInternal;
            return "（自动识别）";
        }

        public string GetPropertyName()
        {
            if (!string.IsNullOrWhiteSpace(PropertyDisplay))
                return PropertyDisplay;
            if (!string.IsNullOrWhiteSpace(PropertyInternal))
                return PropertyInternal;
            return "（缺失）";
        }
    }
}
