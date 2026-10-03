using System.Collections.Generic;
using System.Linq;

namespace WrathBuildPlanner.Core {
    public enum StepStatus { Applied, AlreadySet, Open }

    public enum OpenReason { None, NotFound, Ambiguous, NotSelectable, SelectionMissing, NoFreeSlot, LeftToPlayer, InternalError }

    public class StepResult {
        public string Label;
        public StepStatus Status;
        public OpenReason Reason;
        public string Detail;
        public List<string> Suggestions = new List<string>();

        public static StepResult Applied(string label, string detail = null) =>
            new StepResult { Label = label, Status = StepStatus.Applied, Detail = detail };

        public static StepResult Already(string label) =>
            new StepResult { Label = label, Status = StepStatus.AlreadySet };

        public static StepResult Open(string label, OpenReason reason, string detail = null, List<string> suggestions = null) =>
            new StepResult { Label = label, Status = StepStatus.Open, Reason = reason, Detail = detail, Suggestions = suggestions ?? new List<string>() };
    }

    public class HistoryCheck {
        public bool Comparable;
        public bool Matches;
        public string Expected;
        public string Actual;
    }

    public class ApplyReport {
        public int Level;
        public bool Mythic;
        public bool NoWindow;
        public bool NoRow;
        public HistoryCheck History;
        public List<StepResult> Steps = new List<StepResult>();

        public int AppliedCount => Steps.Count(s => s.Status == StepStatus.Applied);
        public int OpenCount => Steps.Count(s => s.Status == StepStatus.Open);
    }
}
