using UnityEngine;
using Unity.Netcode;

public class Dungeonenter : NetworkBehaviour // CHANGED: Fixed networkBehaviour typo
{
    // CHANGED: Added serialized field for dungeon respawn location
    [SerializeField] private GameObject dungeonRespawnPoint;

    private DungeonWaveManager dungeonWaveManager;

    public GameObject player1;
    public GameObject player2;

    private readonly NetworkVariable<bool> wallsClosedNetwork = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Awake()
    {
        dungeonWaveManager = GetComponentInParent<DungeonWaveManager>();
        if (dungeonWaveManager == null)
        {
            dungeonWaveManager = GetComponent<DungeonWaveManager>();
        }

        FindPlayers();
    }

    public override void OnNetworkSpawn()
    {
        wallsClosedNetwork.OnValueChanged += OnWallsClosedChanged;
        ApplyWallState(wallsClosedNetwork.Value);
    }

    public override void OnNetworkDespawn()
    {
        wallsClosedNetwork.OnValueChanged -= OnWallsClosedChanged;
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject enteringPlayer = GetPlayerObject(other);
        if (enteringPlayer == null) return;

        bool dungeonCompleted = dungeonWaveManager != null && dungeonWaveManager.IsDungeonCompleted;
        if (dungeonCompleted) DisableWalls();
        else CloseWalls();

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

        if (!dungeonCompleted && dungeonRespawnPoint != null)
        {
            FallResetTrigger.SetOverrideRespawnPoint(dungeonRespawnPoint.transform.position);
        }
        else if (dungeonCompleted)
        {
            FallResetTrigger.ClearOverrideRespawnPoint();
        }

        dungeonWaveManager?.DungeonEntered();
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

    public void DisableWalls()
    {
        FallResetTrigger.ClearOverrideRespawnPoint();
        if (NetworkSpawnUtility.IsNetworkSessionActive && IsSpawned && IsServer)
        {
            wallsClosedNetwork.Value = false;
        }

        ApplyWallState(false);
    }

    private void CloseWalls()
    {
        if (NetworkSpawnUtility.IsNetworkSessionActive && IsSpawned)
        {
            if (IsServer)
            {
                SetWallsClosed(true);
            }
            else
            {
                RequestCloseWallsServerRpc();
            }
            return;
        }

        ApplyWallState(true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCloseWallsServerRpc()
    {
        if (dungeonWaveManager != null && dungeonWaveManager.IsDungeonCompleted) return;
        SetWallsClosed(true);
    }

    private void SetWallsClosed(bool closed)
    {
        if (IsSpawned && IsServer) wallsClosedNetwork.Value = closed;
        ApplyWallState(closed);
    }

    private void OnWallsClosedChanged(bool previousValue, bool newValue)
    {
        ApplyWallState(newValue);
    }

    private void ApplyWallState(bool closed)
    {
        foreach (Transform child in transform)
        {
            if (child.CompareTag("walls"))
            {
                child.gameObject.SetActive(closed);
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
