using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace JiePinPai.Navisworks.TrayMeasurement
{
    internal sealed class MeasurementTarget
    {
        public int Number;
        public ModelItem Item;
        public string Name;
        public string Query;
        public string ReviewReason;
        public MeasurementValue Value = new MeasurementValue();
    }

    internal sealed class MeasurementSource
    {
        public MeasurementSourceStamp Stamp;
        public string DocumentPath;
        public string Scope;
        public int ExcludedDuplicates;
        public int UnmatchedConditions;
        public List<MeasurementTarget> Targets = new List<MeasurementTarget>();
    }
}
