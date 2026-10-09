using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using JiePinPai.Navisworks.TrayMeasurement;

namespace JiePinPai.Navisworks
{
    public partial class SearchDialog
    {
        private ThemedButton _btnTrayMeasurement;
        private BatchMeasurementForm _measurementForm = null;
        private long _measurementSourceRevision;
        private long _measurementModelRevision;

        private void InitializeMeasurementLink()
        {
            if (_doc == null) return;
            _doc.Models.CollectionChanged += MeasurementModelChanged;
            _doc.UnitsChanged += MeasurementModelChanged;
            _doc.FilesUpdating += MeasurementModelChanged;
        }

        private void MeasurementModelChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            _measurementModelRevision++;
            _measurementForm?.InvalidateSource("模型已更新，请重新选择构件或重新查找后测量。");
            InvalidateSearchResults("模型已更新，请重新执行搜索。");
        }

        private void NotifyMeasurementSourceChanged()
        {
            _measurementSourceRevision++;
            _measurementForm?.InvalidateSearchSource();
        }

        private bool IsMeasurementSourceCurrent(MeasurementSourceStamp stamp)
        {
            return stamp != null && stamp.IsCurrent(_measurementModelRevision, _measurementSourceRevision,
                !IsDisposed && DocumentIsUsable(_doc) && IsActiveDocument(_doc), _lastResults != null);
        }

        private int GetManualMeasurementSelectionCount()
        {
            if (IsDisposed || !DocumentIsUsable(_doc) || !IsActiveDocument(_doc)) return -1;
            return _doc.CurrentSelection.SelectedItems.Count;
        }

        private MeasurementSource CaptureMeasurementSource(MeasurementSourceKind kind)
        {
            if (!DocumentIsUsable(_doc) || !IsActiveDocument(_doc))
                throw new InvalidOperationException("当前模型已关闭或切换，请重新打开查找窗口。");
            bool manual = kind == MeasurementSourceKind.ManualSelection;
            if (!manual && _lastResults == null)
                throw new InvalidOperationException("请先在查找窗口导入桥架条件并执行搜索，再读取查找结果。");

            // Reuse the same policy as summary, selection sets and hide. STR protection is not a target.
            List<ModelItem> effective = manual
                ? MeasurementBatchPolicy.SnapshotTargets(_doc.CurrentSelection.SelectedItems.Cast<ModelItem>())
                : ResolveEffectiveMatchedItems(_lastResults);
            if (manual && effective.Count == 0)
                throw new InvalidOperationException("请先在模型视图或选择树中选中桥架，可单选或多选。");
            var source = new MeasurementSource
            {
                Stamp = new MeasurementSourceStamp(kind, _measurementModelRevision, _measurementSourceRevision),
                DocumentPath = _doc.FileName,
                Scope = manual ? "模型手动选择" : _currentModelPrefix,
                ExcludedDuplicates = manual ? 0 : _lastResults.Count(r => r.Status == SearchResultStatus.Duplicate
                    && !_includedDuplicateResultIndices.Contains(r.Condition.DisplayIndex)),
                UnmatchedConditions = manual ? 0 : _lastResults.Count(r => r.Status == SearchResultStatus.NotFound
                    || r.Status == SearchResultStatus.ConditionInvalid)
            };
            var queries = new Dictionary<ModelItem, string>();
            foreach (SearchResult result in (manual ? Enumerable.Empty<SearchResult>() : _lastResults).Where(r => r.Status == SearchResultStatus.Found
                || r.Status == SearchResultStatus.Duplicate && _includedDuplicateResultIndices.Contains(r.Condition.DisplayIndex)))
            {
                foreach (ModelItem item in result.MatchedItems)
                    if (item != null && !queries.ContainsKey(item)) queries.Add(item, result.QueryValue);
            }
            HashSet<ModelItem> overlaps = MeasurementBatchPolicy.FindOverlaps(effective, item => item.Parent);
            foreach (ModelItem item in effective)
            {
                string query;
                queries.TryGetValue(item, out query);
                source.Targets.Add(new MeasurementTarget
                {
                    Number = source.Targets.Count + 1,
                    Item = item,
                    Name = item.DisplayName,
                    Query = query,
                    ReviewReason = overlaps.Contains(item) ? (manual
                        ? "父子节点同时选中，请在模型中仅保留构件所在层级后重测。"
                        : "父子节点同时计入，请在查找结果中仅保留构件所在层级。") : null
                });
            }
            return source;
        }

        private void ToggleMeasurementPicker(object sender, EventArgs e)
        {
            TogglePicker(_btnTrayMeasurement, () => new OptionPicker("桥架测量", new[]
            {
                new PickerOption("便捷测量", "简易小窗口，一次测一个选中构件"),
                new PickerOption("批量测量", "导入查找结果，或在模型中手动多选")
            }, -1), alignRight: true, afterClose: index =>
            {
                if (index == 0) SingleMeasurementWindow.Open(_ownerHandle);
                else OpenTrayMeasurement(sender, e);
            }, openBelow: true);
        }

        private void OpenTrayMeasurement(object sender, EventArgs e)
        {
#if DISABLE_BATCH_MEASUREMENT
            MessageBox.Show(this, "当前功能暂不可用", "批量测量", MessageBoxButtons.OK, MessageBoxIcon.Information);
#else
            if (!EnsureDocumentUsable()) return;
            if (_measurementForm != null && !_measurementForm.IsDisposed)
            {
                _measurementForm.WindowState = FormWindowState.Normal;
                _measurementForm.Activate();
                return;
            }
            _measurementForm = new BatchMeasurementForm(_doc, CaptureMeasurementSource, IsMeasurementSourceCurrent,
                GetManualMeasurementSelectionCount, ReturnToMeasurementSearch);
            _measurementForm.FormClosed += (s, args) => _measurementForm = null;
            if (_ownerHandle != IntPtr.Zero) _measurementForm.Show(new WindowWrapper(_ownerHandle));
            else _measurementForm.Show();
#endif
        }

        private void ReturnToMeasurementSearch()
        {
            if (IsDisposed) return;
            WindowState = FormWindowState.Normal;
            SwitchTab(_lastResults == null ? _tabConditions : _tabResults);
            Activate();
            BringToFront();
        }

        private void DisposeMeasurementLink()
        {
            _measurementForm?.Close();
            if (_doc == null || _doc.IsDisposed) return;
            _doc.Models.CollectionChanged -= MeasurementModelChanged;
            _doc.UnitsChanged -= MeasurementModelChanged;
            _doc.FilesUpdating -= MeasurementModelChanged;
        }
    }
}
