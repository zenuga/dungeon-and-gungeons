using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class Ladder : MonoBehaviour
{
    public Depth depth;
    public ChunkedMineGeneration chunkedMineGeneration;
    public TextMeshProUGUI ladderText;

    [Header("Mine Reset Settings")]
    [SerializeField] private string mineGenerationObjectName = "MineGeneration";

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
                // Delete all children of MineGeneration
                ClearMineChildren();

                if (depth != null)
                {
                    // Increments depth value
                    depth.depth++;
                }

                if (chunkedMineGeneration != null)
                {
                    chunkedMineGeneration.level++;
                    chunkedMineGeneration.GenerateMineAndChunks();
                    Debug.LogWarning("thisworks");
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player1") || other.CompareTag("Player2"))
        {
            if (ladderText != null)
            {
                ladderText.gameObject.SetActive(false);
            }
        }
    }

    private void ClearMineChildren()
    {
        GameObject mineObj = GameObject.Find(mineGenerationObjectName);

        if (mineObj != null)
        {
            // Iterate backwards through transform children to safely destroy them
            for (int i = mineObj.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(mineObj.transform.GetChild(i).gameObject);
            }
        }
        else
        {
            Debug.LogWarning($"Could not find GameObject named '{mineGenerationObjectName}' to delete children.");
        }
    }
}