using UnityEditor;
using UnityEngine;

// Editor-only menu for wiping the saved level results shown on the map cards (see LevelProgress).
// Each item changes both the Editor's own saves and the built game's saves on this PC, since the two
// are kept apart by Unity. Lives in an Editor folder, so it never ships in a build.
static class LevelProgressTools
{
    const string BestScorePrefix = "LevelBestScore_";
    const string SolvedPrefix = "LevelSolved_";

    [MenuItem("Tools/Haunting Haunt/Reset Best Scores To 0")]
    static void ResetBestScores()
    {
        if (!EditorUtility.DisplayDialog("Reset Best Scores",
            "Set the best score of every level to 0? Solved levels stay solved.", "Reset", "Cancel")) return;

        foreach (var scene in LevelScenes())
            if (PlayerPrefs.HasKey(BestScorePrefix + scene)) PlayerPrefs.SetInt(BestScorePrefix + scene, 0);
        PlayerPrefs.Save();
        int build = EditBuildSaves(BestScorePrefix, false);

        RefreshBadges();
        Debug.Log("Reset Best Scores: every best score is now 0 (Editor saves, plus " + build + " in the built game).");
    }

    [MenuItem("Tools/Haunting Haunt/Clear All Level Progress")]
    static void ClearProgress()
    {
        if (!EditorUtility.DisplayDialog("Clear Level Progress",
            "Forget every best score and solved level, as if no case was ever played?", "Clear", "Cancel")) return;

        foreach (var scene in LevelScenes()) LevelProgress.Clear(scene);
        int build = EditBuildSaves(BestScorePrefix, true) + EditBuildSaves(SolvedPrefix, true);

        RefreshBadges();
        Debug.Log("Clear Level Progress: all scores and solved marks removed (Editor saves, plus " + build + " in the built game).");
    }

    // Every scene in Build Settings, by name, the same key LevelProgress uses.
    static System.Collections.Generic.IEnumerable<string> LevelScenes()
    {
        foreach (var s in EditorBuildSettings.scenes)
            yield return System.IO.Path.GetFileNameWithoutExtension(s.path);
    }

    // The built game saves under Software\Company\Product in the registry, with a hash added to each
    // name, so values are matched by prefix. Returns how many were changed.
    static int EditBuildSaves(string prefix, bool delete)
    {
#if UNITY_EDITOR_WIN
        string path = "Software\\" + PlayerSettings.companyName + "\\" + PlayerSettings.productName;
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path, true))
        {
            if (key == null) return 0;
            int changed = 0;
            foreach (var name in key.GetValueNames())
            {
                if (!name.StartsWith(prefix)) continue;
                if (delete) key.DeleteValue(name, false);
                else key.SetValue(name, 0, Microsoft.Win32.RegistryValueKind.DWord);
                changed++;
            }
            return changed;
        }
#else
        return 0;
#endif
    }

    // Redraws any map card plaques already on screen. Play mode only: outside it, Refresh would build the
    // plaque straight into the open scene.
    static void RefreshBadges()
    {
        if (!EditorApplication.isPlaying) return;
        foreach (var badge in Object.FindObjectsByType<MapSolvedBadge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            badge.Refresh();
    }
}
