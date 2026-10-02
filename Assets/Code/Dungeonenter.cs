using UnityEngine;
using Unity.Netcode;

public class Dungeonenter : NetworkBehaviour // CHANGED: Fixed networkBehaviour typo
{
    // CHANGED: Added serialized field for dungeon respawn location
    [SerializeField] private GameObject dungeonRespawnPoint;

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
        if ((dungeonWaveManager != null && dungeonWaveManager.IsDungeonCompleted) || 
            (other.CompareTag("Projectile") || other.CompareTag("Ranged") || other.CompareTag("Melee")))
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
            WallBreakEncounterManager.DismissForTransition();
            FindPlayers();

            if (enteringPlayer.CompareTag("Player1") && player2 != null)
            {
                TeleportPlayer(player2, enteringPlayer.transform.position);
            }
            else if (enteringPlayer.CompareTag("Player2") && player1 != null)
            {
                TeleportPlayer(player1, enteringPlayer.transform.position);
            }

            // CHANGED: Update FallResetTrigger with custom dungeon respawn point upon entering
            if (dungeonRespawnPoint != null)
            {
                FallResetTrigger.SetOverrideRespawnPoint(dungeonRespawnPoint.transform.position);
            }

            dungeonWaveManager?.DungeonEntered();
        }
    }

    private static void TeleportPlayer(GameObject player, Vector3 position)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.RequestNetworkTeleport(position);
        }
        else if (!NetworkSpawnUtility.IsNetworkSessionActive)
        {
            player.transform.position = position;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        dungeonsDone++;
    }

    public void DisableWalls()
    {
        // CHANGED: Reset respawn point back to base mine spawn location when walls open / dungeon completes
        FallResetTrigger.ClearOverrideRespawnPoint();

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
        player1 = null;
        player2 = null;

        foreach (PlayerController controller in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (controller.Type == PlayerController.PlayerType.Player1 && player1 == null)
            {
                player1 = controller.gameObject;
            }
            else if (controller.Type == PlayerController.PlayerType.Player2 && player2 == null)
            {
                player2 = controller.gameObject;
            }
        }
    }

    private static GameObject GetPlayerObject(Collider other)
    {
        Transform current = other.transform;
        while (current != null)
        {
            PlayerController playerController = current.GetComponent<PlayerController>();
            if (playerController != null)
            {
                return playerController.gameObject;
            }

            if (current.CompareTag("Player") || current.CompareTag("Player1") || current.CompareTag("Player2"))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }
}
