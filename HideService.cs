using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 执行隐藏未选中操作。
    ///
    /// 流程：
    ///   1. 保存已匹配项
    ///   2. InvertSelection() → 所有未匹配项被选中
    ///   3. 构建待隐藏集合
    ///   4. SetSelectionHidden(待隐藏) → 隐藏（先于 CopyFrom）
    ///   5. CopyFrom 恢复已匹配项
    /// </summary>
    public static class HideService
    {
        /// <summary>
        /// 执行隐藏未选中操作。
        /// </summary>
        /// <param name="doc">当前 Navisworks 文档。</param>
        /// <param name="totalMatchedCount">总匹配对象数量。</param>
        /// <param name="totalConditionCount">条件总数。</param>
        /// <returns>操作是否成功。</returns>
        public static bool HideUnselected(
            Document doc,
            int totalMatchedCount,
            int totalConditionCount)
        {
            // ── 保护检查 ──
            int currentSelectionCount = 0;
            try
            {
                foreach (ModelItem _ in doc.CurrentSelection.SelectedItems)
                    currentSelectionCount++;
            }
            catch
            {
                currentSelectionCount = -1;
            }

            if (currentSelectionCount <= 0)
                throw new InvalidOperationException(
                    "褰撳墠閫夋嫨宸蹭涪澶憋紝鏃犳硶鎵ц闅愯棌鏈€変腑銆傝鍏堢‘淇濆尮閰嶅璞′繚鎸侀€変腑鐘舵€併€?);

            if (totalMatchedCount == 0)
                throw new InvalidOperationException(
                    "无匹配对象，无法执行隐藏未选中操作。");

            bool success = false;

            try
            {
                object state = doc.State;
                if (state == null)
                    throw new InvalidOperationException(
                        "无法获取 Navisworks 文档状态对象。");

                Type stateType = state.GetType();

                // 1. 保存已匹配项的 Guid
                var matchedGuids = new HashSet<Guid>();
                var matchedItems = new List<ModelItem>();
                foreach (ModelItem item in doc.CurrentSelection.SelectedItems)
                {
                    matchedItems.Add(item);
                    matchedGuids.Add(item.InstanceGuid);
                }

                // 2. 反转选择（所有未匹配项被选中）
                stateType.InvokeMember(
                    "InvertSelection",
                    BindingFlags.InvokeMethod,
                    null,
                    state,
                    null);

                // 3. 构建待隐藏集合
                ModelItemCollection toHide = new ModelItemCollection();
                foreach (ModelItem item in doc.CurrentSelection.SelectedItems)
                    toHide.Add(item);

                // 4. 隐藏（先于 CopyFrom，确保被隐藏对象还在选中状态中）
                stateType.InvokeMember(
                    "SetSelectionHidden",
                    BindingFlags.InvokeMethod,
                    null,
                    state,
                    new object[] { toHide, true });

                // 5. 恢复选择：已匹配项
                using (var restoreSelection = new ModelItemCollection())
                {
                    foreach (ModelItem item in matchedItems)
                        restoreSelection.Add(item);

                    doc.CurrentSelection.CopyFrom(restoreSelection);
                }

                success = true;
            }
            catch (MissingMethodException ex)
            {
                throw new InvalidOperationException(
                    "Navisworks 隐藏 API 不可用(SetSelectionHidden)，请检查 Navisworks 版本兼容性。");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "执行隐藏未选中操作时出错。",
                    ex);
            }
            return success;
        }
    }
}
