using UnityEngine;

// Which levels the player has solved, kept in PlayerPrefs so the map selection can mark them across
// sessions. Keyed by the level's scene name (the same string the map buttons pass to PlayGame).
public static class LevelProgress
{
    private const string SolvedKey = "LevelSolved_";
    private const string BestScoreKey = "LevelBestScore_";

    public static bool IsSolved(string sceneName) =>
        !string.IsNullOrEmpty(sceneName) && PlayerPrefs.GetInt(SolvedKey + sceneName, 0) == 1;

    // -1 when the level has never been finished.
    public static int BestScore(string sceneName) =>
        string.IsNullOrEmpty(sceneName) ? -1 : PlayerPrefs.GetInt(BestScoreKey + sceneName, -1);

    // Every case is scored out of this.
    public const int MaxScore = 100;

    // The word under a score, shared by the Case Summary and the map cards.
    public static string Rank(int score) =>
        score >= 85 ? "Exceptional" : score >= 60 ? "Solid work" : score >= 30 ? "Case left open" : "Cold trail";

    // Called when a case is closed. Solving is permanent - replaying and doing worse never un-solves it,
    // and only a higher score replaces the best.
    public static void RecordResult(string sceneName, bool solved, int score)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (solved) PlayerPrefs.SetInt(SolvedKey + sceneName, 1);
        if (score > BestScore(sceneName)) PlayerPrefs.SetInt(BestScoreKey + sceneName, score);
        PlayerPrefs.Save();
    }

    public static void Clear(string sceneName)
    {
        PlayerPrefs.DeleteKey(SolvedKey + sceneName);
        PlayerPrefs.DeleteKey(BestScoreKey + sceneName);
        PlayerPrefs.Save();
    }
}
