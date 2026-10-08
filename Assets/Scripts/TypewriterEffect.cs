using System.Collections;
using TMPro;
using UnityEngine;

// Shared letter-by-letter reveal for case-file text (notes, intro narration) - reads like the
// player is uncovering the line rather than having it dumped on screen instantly. Runs on
// unscaled time so it still plays correctly while the note/intro panel has paused Time.timeScale.
public static class TypewriterEffect
{
    public static IEnumerator Reveal(TMP_Text text, string content, float charactersPerSecond, System.Func<bool> skip = null)
    {
        if (text == null) yield break;

        text.text = content;
        text.ForceMeshUpdate();
        int totalChars = text.textInfo.characterCount;
        text.maxVisibleCharacters = 0;

        if (charactersPerSecond <= 0f)
        {
            text.maxVisibleCharacters = totalChars;
            yield break;
        }

        float secondsPerChar = 1f / charactersPerSecond;
        float timer = 0f;
        int shown = 0;

        while (shown < totalChars)
        {
            if (skip != null && skip())
                break;

            timer += Time.unscaledDeltaTime;
            while (timer >= secondsPerChar && shown < totalChars)
            {
                timer -= secondsPerChar;
                shown++;
            }

            text.maxVisibleCharacters = shown;
            yield return null;
        }

        text.maxVisibleCharacters = totalChars;
    }
}
