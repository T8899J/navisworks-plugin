using JiePinPai.Navisworks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class SearchConditionValidatorTests
    {
        [TestMethod]
        public void TryValidate_AcceptsSupportedCompleteCondition()
        {
            var condition = new SearchCondition
            {
                PropertyDisplay = "名称",
                Test = "equals",
                Value = "M14-101",
            };

            Assert.IsTrue(SearchConditionValidator.TryValidate(condition, out string error));
            Assert.AreEqual(string.Empty, error);
        }

        [TestMethod]
        [DataRow("", "equals", "M14-101", "属性名不能为空。")]
        [DataRow("名称", "equals", "", "查询值不能为空。")]
        [DataRow("名称", "startsWith", "M14", "匹配方式仅支持 equals 或 contains。")]
        public void TryValidate_RejectsIncompleteOrUnsupportedCondition(
            string property,
            string test,
            string value,
            string expectedError)
        {
            var condition = new SearchCondition
            {
                PropertyDisplay = property,
                Test = test,
                Value = value,
            };

            Assert.IsFalse(SearchConditionValidator.TryValidate(condition, out string error));
            Assert.AreEqual(expectedError, error);
        }
    }
}
