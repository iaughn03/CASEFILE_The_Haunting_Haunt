using UnityEngine;
using TMPro;
using System.Text;

public class TaskbarUI : MonoBehaviour
{
    [Header("References")]
    public EvidenceManager evidenceManager;

    [Header("Evidence UI")]
    public TextMeshProUGUI evidenceCountText;
    public TextMeshProUGUI evidenceListText;

    private void Awake()
    {
        // Show default text immediately when the game starts
        if (evidenceCountText != null)
        {
            evidenceCountText.text = "Evidence Found: 0/10";
        }

        if (evidenceListText != null)
        {
            evidenceListText.text = "No clues gathered yet...";
        }
    }

    private void Start()
    {
        if (evidenceManager == null)
        {
            evidenceManager = FindFirstObjectByType<EvidenceManager>();
        }

        RefreshTaskbarData();
    }

    public void RefreshTaskbarData()
    {
        if (evidenceManager == null)
        {
            Debug.LogWarning("TaskbarUI: No EvidenceManager found!");
            return;
        }

        if (evidenceCountText != null)
        {
            evidenceCountText.text =
                $"Evidence Found: {evidenceManager.currentEvidenceCount}/{evidenceManager.maxEvidenceNeeded}";
        }

        if (evidenceListText != null)
        {
            if (evidenceManager.collectedEvidenceNames.Count == 0)
            {
                evidenceListText.text = "No clues gathered yet...";
            }
            else
            {
                StringBuilder sb = new StringBuilder();

                foreach (string clue in evidenceManager.collectedEvidenceNames)
                {
                    sb.AppendLine("• " + clue);
                }

                evidenceListText.text = sb.ToString();
            }
        }
    }
}