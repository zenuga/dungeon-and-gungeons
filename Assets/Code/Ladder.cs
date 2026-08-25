using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class Ladder : MonoBehaviour
{
    public Depth depth;
    public ChunkedMineGeneration ChunkedMineGeneration;
    public Image image;

    // Change to OnTriggerStay2D(Collider2D other) if using 2D Physics
    private void OnTriggerStay(Collider other)
    {
        // Check if the triggering object has either Player tag
        if (other.CompareTag("Player1") || other.CompareTag("Player2"))
        {
            if (image != null)
            {
                image.gameObject.SetActive(true);
            }

            // Check input via New Input System
            if (Keyboard.current != null && 
               (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.semicolonKey.wasPressedThisFrame))
            {
                ChunkedMineGeneration.level++;
                image.gameObject.SetActive(false);
                if (depth != null)
                {
                    // Increments depth value (assuming depth has a public 'depth' field/property)
                    depth.depth++;
                }

                if (ChunkedMineGeneration != null)
                {
                    ChunkedMineGeneration.GenerateMineAndChunks();

                }
            }
        }
    }
}