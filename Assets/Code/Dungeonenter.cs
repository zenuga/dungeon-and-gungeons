using UnityEngine;

public class Dungeonenter : MonoBehaviour
{
    private DungeonWaveManager dungeonWaveManager;

    public GameObject player1;
    public GameObject player2;

    private int dungeonsDone;

    private void Awake()
    {
        dungeonWaveManager = GetComponentInParent<DungeonWaveManager>();
        if (dungeonWaveManager == null)
        {
            dungeonWaveManager = GetComponent<DungeonWaveManager>();
        }
    }

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
            if (player2 != null && player1 != null)
            {
                player2.transform.position = player1.transform.position;
            }
        }
        else if (other.CompareTag("Player2"))
        {
            if (player1 != null && player2 != null)
            {
                player1.transform.position = player2.transform.position;
            }
        }

        if (other.CompareTag("Player") || other.CompareTag("Player1") || other.CompareTag("Player2"))
        {
            dungeonWaveManager?.DungeonEntered();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        dungeonsDone++;
    }
}