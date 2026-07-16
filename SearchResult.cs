using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    public class SearchResult
    {
        public SearchConditionSnapshot Condition { get; set; }
        public SearchResultStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public int MatchCount { get; set; }
        public List<ModelItem> MatchedItems { get; set; }

        public string QueryValue => Condition?.Value ?? string.Empty;
        public bool IsProblem => Status != SearchResultStatus.Found;

        public SearchResult()
        {
            Status = SearchResultStatus.ConditionInvalid;
            StatusMessage = string.Empty;
            MatchedItems = new List<ModelItem>();
        }
    }
}
