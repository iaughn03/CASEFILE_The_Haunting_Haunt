// Tiny shared holder for the capstone's "measurable learning outcome" - whether the player correctly
// classified the case in CaseClassificationQuiz. Static/session-scoped rather than persisted, since
// it only needs to survive from the quiz to wherever a debrief/results screen later reads it.
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
