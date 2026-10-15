using UnityEngine;

// A document the player can read in the world (a witness statement, a scrap of paper) that is NOT
// evidence: it never goes in the bag, never counts toward the clue total and has no card on the case wall.
// It opens in the same note reader as the clue documents, and once read it is kept in the Case File so it
// can be read again. Needs a collider on the Interact layer, like every other interactable.
public class ReadableNote : MonoBehaviour
{
    [Tooltip("Shown in the prompt (\"[E] Read ...\") and as its title in the Case File.")]
    public string title = "Statement";

    [TextArea(8, 24)] public string text;

    public bool HasBeenRead { get; private set; }

    public void Read()
    {
        if (NoteSystem.Instance == null) return;
        HasBeenRead = true;
        CaseFileUI.RegisterDocument(this);
        // No source object: nothing is hidden or collected when the page closes.
        NoteSystem.Instance.InspectNoteInFace(text, null);
    }
}
