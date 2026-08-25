using UnityEngine;

public class Dungeonenter : MonoBehaviour
{
    DungeonWaveManager DungeonWaveManager;
    
    
    public GameObject player1;
    public GameObject player2;

    private int dungeonsDone;

    private void OnTriggerEnter(Collider other)
    {
        // 1. Activate wall children attached to this GameObject
        foreach (Transform child in transform)
        {
            if (child.CompareTag("walls"))
            {
                child.gameObject.SetActive(true);
            }
        }

        // 2. Check the incoming trigger object ('other') and teleport the players
        if (other.CompareTag("Player1"))
        {
            // Teleport Player 1 to Player 2's position
            player2.transform.position = player1.transform.position;
        }
        else if (other.CompareTag("Player2"))
        {
            // Teleport Player 2 to Player 1's position
            player1.transform.position = player2.transform.position;
        }
        {
            DungeonWaveManager.DungeonEntered();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        dungeonsDone++;
    }
}