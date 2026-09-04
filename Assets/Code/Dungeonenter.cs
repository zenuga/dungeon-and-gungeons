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

        FindPlayers();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Activate wall children attached to this GameObject
        if (dungeonWaveManager != null && dungeonWaveManager.IsDungeonCompleted)
        {
            DisableWalls();
        }
        else
        {
            foreach (Transform child in transform)
            {
                if (child.CompareTag("walls"))
                {
                    child.gameObject.SetActive(true);
                }
            }
        }

        // 2. Check the incoming trigger object ('other') and teleport the players
        GameObject enteringPlayer = GetPlayerObject(other);
        if (enteringPlayer != null)
        {
            FindPlayers();

            if (enteringPlayer.CompareTag("Player1") && player2 != null)
            {
                player2.transform.position = enteringPlayer.transform.position;
            }
            else if (enteringPlayer.CompareTag("Player2") && player1 != null)
            {
                player1.transform.position = enteringPlayer.transform.position;
            }
        }

        if (enteringPlayer != null)
        {
            dungeonWaveManager?.DungeonEntered();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        dungeonsDone++;
    }

    public void DisableWalls()
    {
        foreach (Transform child in transform)
        {
            if (child.CompareTag("walls"))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void FindPlayers()
    {
        if (player1 == null)
        {
            player1 = GameObject.FindGameObjectWithTag("Player1");
        }

        if (player2 == null)
        {
            player2 = GameObject.FindGameObjectWithTag("Player2");
        }
    }

    private static GameObject GetPlayerObject(Collider other)
    {
        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag("Player") || current.CompareTag("Player1") || current.CompareTag("Player2"))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }
}