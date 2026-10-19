// Tiny shared holder for the capstone's "measurable learning outcome" - whether the player correctly
// ruled the case in the Final Inspection (FinalInspection.ComputeResult). Static/session-scoped rather
// than persisted, since it only needs to survive to wherever a debrief/results screen later reads it.
public static class AssessmentResults
{
    public static bool HasAnswered { get; private set; }
    public static bool ClassifiedCorrectly { get; private set; }

    public static void RecordClassification(bool correct)
    {
        HasAnswered = true;
        ClassifiedCorrectly = correct;
    }
}
