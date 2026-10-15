using UnityEngine;

// The story side of one case: what the player is told at the start, who the people in the file are and
// what they say, why the ruling fits (unlocked once the case is solved), and the full account shown after
// a correct report. Read by CaseFileUI (the Case File in the bag) and FinalInspection (the reveal screen).
[CreateAssetMenu(menuName = "Case/Case Story", fileName = "CaseStory")]
public class CaseStoryData : ScriptableObject
{
    [System.Serializable]
    public class Person
    {
        public string name;
        [Tooltip("Who they are to the victim, one line.")]
        public string relation;
        [Tooltip("Plain facts about them the investigator has on file: age, job, keys, build, where they were.")]
        [TextArea(3, 10)] public string profile;
        [Tooltip("What they told the police, in their own words. A claim, not a fact: the evidence confirms or breaks it.")]
        [TextArea(3, 12)] public string statement;
        public Sprite portrait;
        [Tooltip("Left empty, the person is in the file from the start. Otherwise they appear once any of these clues is in the bag.")]
        public EvidenceData[] revealedBy;
    }

    [Tooltip("Scene this case belongs to; the Guide's case page unlocks once LevelProgress marks it solved.")]
    public string sceneName;
    public string caseTitle;

    [Header("Briefing (shown after the intro, and kept in the Case File)")]
    [TextArea(8, 30)] public string briefing;

    [Header("People in the file")]
    public Person[] persons;

    [Header("Guide page for this case (locked until the case is solved)")]
    public string rulingWhyTitle;
    [TextArea(8, 30)] public string rulingWhy;

    [Header("What really happened (after a correct report, before the summary)")]
    [TextArea(10, 40)] public string storyReveal;
}
