using System;
using System.Collections.Generic;
using System.Linq;
using JiePinPai.Navisworks.TrayMeasurement;
using JiePinPai.Navisworks.TrayMeasurement.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JiePinPai.Navisworks.Tests
{
    [TestClass]
    public class TrayMeasurementTests
    {
        private sealed class Node
        {
            public Node Parent;
        }

        [TestMethod]
        public void ManualMeasurementDoesNotRequireSearchOrFollowSearchEdits()
        {
            var stamp = new MeasurementSourceStamp(MeasurementSourceKind.ManualSelection, 4, 2);
            Assert.IsTrue(stamp.IsCurrent(4, 2, true, false));
            Assert.IsTrue(stamp.IsCurrent(4, 50, true, true));
            Assert.IsTrue(stamp.IsCurrent(4, 51, true, false));
        }

        [TestMethod]
        public void BatchMeasurementExpiresWhenSearchIsEditedOrCleared()
        {
            var stamp = new MeasurementSourceStamp(MeasurementSourceKind.SearchResults, 4, 2);
            Assert.IsTrue(stamp.IsCurrent(4, 2, true, true));
            Assert.IsFalse(stamp.IsCurrent(4, 3, true, true));
            Assert.IsFalse(stamp.IsCurrent(4, 2, true, false));
        }

        [TestMethod]
        public void BothSourcesExpireWhenModelChangesOrDocumentIsUnavailable()
        {
            foreach (MeasurementSourceKind kind in Enum.GetValues(typeof(MeasurementSourceKind)))
            {
                var stamp = new MeasurementSourceStamp(kind, 4, 2);
                Assert.IsFalse(stamp.IsCurrent(5, 2, true, true));
                Assert.IsFalse(stamp.IsCurrent(4, 2, false, true));
            }
        }

        [TestMethod]
        public void ManualTargetsAreDeduplicatedAndDoNotFollowLaterSelectionChanges()
        {
            var first = new Node(); var second = new Node(); var third = new Node();
            var selection = new List<Node> { first, second, first, null };
            List<Node> snapshot = MeasurementBatchPolicy.SnapshotTargets(selection);
            selection.Clear(); selection.Add(third);
            CollectionAssert.AreEqual(new[] { first, second }, snapshot);
            CollectionAssert.AreEqual(new[] { third }, MeasurementBatchPolicy.SnapshotTargets(selection));
            Assert.AreEqual(0, MeasurementBatchPolicy.SnapshotTargets<Node>(null).Count);
        }

        [TestMethod]
        public void OverlappingAncestorsAreAllExcludedButSiblingsRemainIndependent()
        {
            var root = new Node(); var parent = new Node { Parent = root };
            var child = new Node { Parent = parent }; var sibling = new Node { Parent = root };
            var overlaps = MeasurementBatchPolicy.FindOverlaps(new[] { parent, child, sibling, child }, n => n.Parent);
            CollectionAssert.AreEquivalent(new[] { parent, child }, overlaps.ToArray());
            Assert.AreEqual(0, MeasurementBatchPolicy.FindOverlaps(new[] { child, sibling }, n => n.Parent).Count);
        }

        [TestMethod]
        public void NonAdjacentHierarchyOverlapIsDetected()
        {
            var root = new Node(); var middle = new Node { Parent = root }; var leaf = new Node { Parent = middle };
            CollectionAssert.AreEquivalent(new[] { root, leaf },
                MeasurementBatchPolicy.FindOverlaps(new[] { root, leaf }, n => n.Parent).ToArray());
        }

        [TestMethod]
        public void TotalsExcludeFittingsFailuresAndUnmeasuredObjects()
        {
            var straight = new MeasurementValue(); straight.Complete(true, 1.237, "");
            var fitting = new MeasurementValue(); fitting.Complete(false, 5, "弯头");
            var failed = new MeasurementValue(); failed.Complete(false, 0, "读取失败");
            var pending = new MeasurementValue(); var stopped = new MeasurementValue(); stopped.Cancel();
            Assert.AreEqual(1.237, MeasurementBatchPolicy.Total(new[] { straight, fitting, failed, pending, stopped }), 1e-10);
            Assert.IsNull(fitting.Metres); Assert.IsNull(failed.Metres);
        }

        [TestMethod]
        public void InvalidNumbersNeverBecomeMeasuredLengths()
        {
            foreach (double length in new[] { 0, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var value = new MeasurementValue(); value.Complete(true, length, "");
                Assert.AreEqual(MeasurementState.Review, value.State); Assert.IsNull(value.Metres);
            }
        }

        [TestMethod]
        public void CancellationPreservesCompletedAndReviewResults()
        {
            var measured = new MeasurementValue(); measured.Complete(true, 2.7, ""); measured.Cancel();
            var review = new MeasurementValue(); review.Complete(false, 8, "配件"); review.Cancel();
            var pending = new MeasurementValue(); pending.Cancel();
            Assert.AreEqual(MeasurementState.Measured, measured.State);
            Assert.AreEqual(MeasurementState.Review, review.State);
            Assert.AreEqual(MeasurementState.Cancelled, pending.State);
            Assert.AreEqual(2.7, MeasurementBatchPolicy.Total(new[] { measured, review, pending }), 1e-10);
        }

        [TestMethod]
        public void ExportEscapesModelTextAndSpreadsheetFormulas()
        {
            Assert.AreEqual("\"桥架,\"\"A\"\"\"", MeasurementBatchPolicy.Csv("桥架,\"A\""));
            Assert.AreEqual("\"'  =1+2\"", MeasurementBatchPolicy.Csv("  =1+2"));
            Assert.AreEqual("\"'@SUM(A1)\"", MeasurementBatchPolicy.Csv("@SUM(A1)"));
            Assert.AreEqual("\"1.237\"", MeasurementBatchPolicy.Csv(MeasurementBatchPolicy.FormatMetres(1.237)));
        }

        [TestMethod]
        public void SearchInclusionFlowsIntoDeduplicatedMeasurementTotal()
        {
            var duplicates = new Dictionary<int, IReadOnlyList<string>> { [2] = new[] { "A", "B" }, [3] = new[] { "B", "C" } };
            var effective = DuplicateMatchInclusionPolicy.ResolveEffectiveItems(new[] { "A" }, duplicates, new[] { 2, 3 });
            var lengths = new Dictionary<string, double> { ["A"] = 1, ["B"] = 2, ["C"] = 3 };
            var values = effective.Select(id => { var v = new MeasurementValue(); v.Complete(true, lengths[id], ""); return v; });
            Assert.AreEqual(6, MeasurementBatchPolicy.Total(values), 1e-10);
            CollectionAssert.AreEquivalent(new[] { "A" }, DuplicateMatchInclusionPolicy.ResolveEffectiveItems(new[] { "A" }, duplicates, new int[0]));
        }

        private static List<Triangle> Box(double length, double width, double height, Vec offset = default(Vec))
        {
            var p = new[] { new Vec(0,0,0), new Vec(length,0,0), new Vec(length,width,0), new Vec(0,width,0),
                new Vec(0,0,height), new Vec(length,0,height), new Vec(length,width,height), new Vec(0,width,height) }.Select(v => v + offset).ToArray();
            int[] faces = { 0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7 };
            var mesh = new List<Triangle>();
            for (int i = 0; i < faces.Length; i += 3) mesh.Add(new Triangle(p[faces[i]], p[faces[i + 1]], p[faces[i + 2]]));
            return mesh;
        }

        private static Vec Rotate(Vec p)
        {
            double a = .713, b = .381;
            var q = new Vec(p.X * Math.Cos(a) - p.Y * Math.Sin(a), p.X * Math.Sin(a) + p.Y * Math.Cos(a), p.Z);
            return new Vec(q.X * Math.Cos(b) + q.Z * Math.Sin(b), q.Y, -q.X * Math.Sin(b) + q.Z * Math.Cos(b));
        }

        private static List<Triangle> Transform(IEnumerable<Triangle> mesh, Func<Vec, Vec> transform) =>
            mesh.Select(t => new Triangle(transform(t.A), transform(t.B), transform(t.C))).ToList();

        [TestMethod]
        public void RotatedTranslatedTrayKeepsItsActualLength()
        {
            var mesh = Transform(Box(1.237, .6, .15), p => Rotate(p) + new Vec(12345, -98654, 3514));
            Measurement result = StraightMeasurement.Measure(mesh);
            Assert.IsTrue(result.IsStraightCandidate); Assert.AreEqual(1.237, result.SpanMetres, 1e-8);
        }

        [TestMethod]
        public void UChannelAndUnequalTriangulationKeepLength()
        {
            var channel = Box(1.237, .6, .005);
            channel.AddRange(Box(1.237, .005, .15)); channel.AddRange(Box(1.237, .005, .15, new Vec(0, .595, 0)));
            Triangle t = channel[0]; channel.RemoveAt(0);
            Vec mid = (t.A + t.B + t.C) * (1.0 / 3);
            channel.Add(new Triangle(t.A, t.B, mid)); channel.Add(new Triangle(t.B, t.C, mid)); channel.Add(new Triangle(t.C, t.A, mid));
            Measurement result = StraightMeasurement.Measure(Transform(channel, Rotate));
            Assert.IsTrue(result.IsStraightCandidate); Assert.AreEqual(1.237, result.SpanMetres, 1e-8);
        }

        [TestMethod]
        public void TeeElbowGapAndReducerAreNotStraightLengths()
        {
            var tee = Box(3, .2, .1); tee.AddRange(Box(.2, 1, .1, new Vec(1.4, 0, 0)));
            var elbow = Box(2, .2, .1); elbow.AddRange(Box(.2, 1.5, .1, new Vec(1.8, 0, 0)));
            var gap = Box(1, .2, .1); gap.AddRange(Box(1, .2, .1, new Vec(2, 0, 0)));
            var reducer = Transform(Box(3, .3, .1), p => new Vec(p.X, p.Y * (1 + p.X * .2), p.Z));
            foreach (var mesh in new[] { tee, elbow, gap, reducer, Box(.4, .6, .15), Box(1, 1, 1) })
                Assert.IsFalse(StraightMeasurement.Measure(mesh).IsStraightCandidate);
        }

        [TestMethod]
        public void InstancePathFilteringUsesIntegerSegments()
        {
            Assert.IsTrue(GeometryTransform.IsWithinPath(new[] { 1, 2 }, new[] { 1, 2, 3 }));
            Assert.IsFalse(GeometryTransform.IsWithinPath(new[] { 1, 2 }, new[] { 1, 20, 3 }));
            Assert.IsFalse(GeometryTransform.IsWithinPath(new int[0], new[] { 1, 2 }));
        }

        [TestMethod]
        public void WorldMatrixHandlesOneBasedSafeArrayAndScaledInstance()
        {
            double[] matrix = { 0,2,0,0,-3,0,0,0,0,0,4,0,100,200,300,1 };
            Array oneBased = Array.CreateInstance(typeof(double), new[] { 16 }, new[] { 1 });
            for (int i = 0; i < 16; i++) oneBased.SetValue(matrix[i], i + 1);
            double[] loaded = GeometryTransform.ReadArray(oneBased, 16);
            Measurement result = StraightMeasurement.Measure(Transform(Box(1.237, .6, .15), p => GeometryTransform.Apply(p, loaded)));
            Assert.AreEqual(2 * 1.237, result.SpanMetres, 1e-8);
        }
    }
}
