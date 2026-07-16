using JiePinPai.Navisworks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class SearchConditionSnapshotTests
    {
        [TestMethod]
        public void From_CopiesAllFieldsAndDoesNotTrackLaterEdits()
        {
            var source = new SearchCondition
            {
                CategoryInternal = "LcOaItem",
                CategoryDisplay = "Item",
                PropertyInternal = "LcOaSceneBaseUserName",
                PropertyDisplay = "名称",
                Test = "equals",
                Value = "M14-101",
            };

            SearchConditionSnapshot snapshot = SearchConditionSnapshot.From(3, source);
            source.Value = "CHANGED";

            Assert.AreEqual(3, snapshot.ConditionIndex);
            Assert.AreEqual(4, snapshot.DisplayIndex);
            Assert.AreEqual("Item", snapshot.CategoryDisplay);
            Assert.AreEqual("名称", snapshot.PropertyDisplay);
            Assert.AreEqual("equals", snapshot.Test);
            Assert.AreEqual("M14-101", snapshot.Value);
        }

        [TestMethod]
        public void DisplayNames_UseExplicitPlaceholdersInsteadOfBlankCells()
        {
            var source = new SearchCondition
            {
                CategoryDisplay = "",
                CategoryInternal = "",
                PropertyDisplay = "",
                PropertyInternal = "",
            };

            SearchConditionSnapshot snapshot = SearchConditionSnapshot.From(0, source);

            Assert.AreEqual("（自动识别）", snapshot.GetCategoryName());
            Assert.AreEqual("（缺失）", snapshot.GetPropertyName());
        }

        [TestMethod]
        public void From_NullCondition_PreservesIndexAndUsesEmptyFields()
        {
            SearchConditionSnapshot snapshot = SearchConditionSnapshot.From(2, null);

            Assert.AreEqual(2, snapshot.ConditionIndex);
            Assert.AreEqual(3, snapshot.DisplayIndex);
            Assert.AreEqual(string.Empty, snapshot.CategoryInternal);
            Assert.AreEqual(string.Empty, snapshot.CategoryDisplay);
            Assert.AreEqual(string.Empty, snapshot.PropertyInternal);
            Assert.AreEqual(string.Empty, snapshot.PropertyDisplay);
            Assert.AreEqual(string.Empty, snapshot.Test);
            Assert.AreEqual(string.Empty, snapshot.Value);
        }
    }
}
