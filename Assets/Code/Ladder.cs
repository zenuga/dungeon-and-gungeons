using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class Ladder : MonoBehaviour
{
    public Depth depth;
    public ChunkedMineGeneration chunkedMineGeneration;
    public TextMeshProUGUI ladderText;
    private bool isTransitioning;

    private void Awake()
    {
        // Find the scripts anywhere active in the scene
        if (depth == null)
        {
            depth = FindFirstObjectByType<Depth>();
        }

        if (chunkedMineGeneration == null)
        {
            chunkedMineGeneration = FindFirstObjectByType<ChunkedMineGeneration>();
        }

        // Find the UI Text by GameObject name if not already assigned
        if (ladderText == null)
        {
            GameObject textObj = GameObject.Find("LadderText");
            if (textObj != null)
            {
                ladderText = textObj.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                // Fallback: search scene for any TextMeshProUGUI component with this name
                TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var txt in allTexts)
                {
                    if (txt.gameObject.name == "LadderText")
                    {
                        ladderText = txt;
                        break;
                    }
                }
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Check if the triggering object has either Player tag
        if (other.CompareTag("Player1") || other.CompareTag("Player2"))
        {
            if (ladderText != null)
            {
                ladderText.gameObject.SetActive(true);
            }

            // Check input via New Input System
            if (Keyboard.current != null && 
               (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.semicolonKey.wasPressedThisFrame))
            {
                if (isTransitioning)
                {
                    return;
                }

                isTransitioning = true;

                DungeonWaveManager[] waveManagers = FindObjectsByType<DungeonWaveManager>(FindObjectsSortMode.None);
                foreach (DungeonWaveManager waveManager in waveManagers)
                {
                    waveManager.ClearDungeonRewardsAndLadder();
                }

                if (depth != null)
                {
                    // Increments depth value
                    depth.depth++;
                }

                if (chunkedMineGeneration != null)
                {
                    chunkedMineGeneration.level++;
                    chunkedMineGeneration.RegenerateMine();
                    ShopResetRegistry.ResetAll();
                }

                if (ladderText != null)
                {
                    ladderText.gameObject.SetActive(false);
                }

                Destroy(gameObject);
            }
        }
    }
}