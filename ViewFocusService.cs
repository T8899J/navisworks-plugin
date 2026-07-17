using System;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using NavApp = Autodesk.Navisworks.Api.Application;

namespace JiePinPai.Navisworks
{
    /// <summary>
    /// 通过 COM 桥把当前视图相机聚焦到当前选择（缩放到选中对象）。
    /// COM 失败为非致命：当前选择保持不变，仅跳过缩放（可选记录诊断日志）。
    /// </summary>
    public static class ViewFocusService
    {
        /// <summary>
        /// 将当前视图缩放到 Navisworks 当前选择。
        /// </summary>
        /// <param name="doc">当前 Navisworks 文档。</param>
        /// <param name="diagnosticLog">可选诊断日志会话。</param>
        /// <returns>成功缩放返回 true；文档不可用或 COM 失败返回 false（非致命）。</returns>
        public static bool ZoomToCurrentSelection(
            Document doc,
            DiagnosticLogSession diagnosticLog = null)
        {
            if (doc == null || IsDocumentClear(doc) || !IsActiveDocument(doc))
            {
                diagnosticLog?.LogDecision("相机聚焦跳过：文档不可用或已不是活动文档。");
                return false;
            }

            try
            {
                // ComApiBridge.State 对应当前活动文档；无活动文档时会抛异常，已被下方捕获。
                var state = ComApiBridge.State;
                if (state == null)
                {
                    diagnosticLog?.LogDecision("相机聚焦跳过：COM State 为 null。");
                    return false;
                }

                state.ZoomInCurViewOnCurSel();
                return true;
            }
            catch (Exception ex)
            {
                // 相机缩放失败不影响选择结果，静默降级。
                diagnosticLog?.LogException("相机聚焦 ZoomInCurViewOnCurSel", ex);
                return false;
            }
        }

        private static bool IsDocumentClear(Document doc)
        {
            try
            {
                return doc.IsClear;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsActiveDocument(Document doc)
        {
            try
            {
                return ReferenceEquals(NavApp.ActiveDocument, doc);
            }
            catch
            {
                return false;
            }
        }
    }
}
