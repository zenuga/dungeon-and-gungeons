using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem; // Added for keyboard input checking

[RequireComponent(typeof(Collider))]
public class FallResetTrigger : MonoBehaviour
{
    [SerializeField] private ChunkedMineGeneration mineGeneration;
    [SerializeField] private float resetCooldown = 0.01f;
    
    // CHANGED: Fixed CS0246 type name error from TMP_TextMeshProUGUI to TextMeshProUGUI
    [SerializeField] private TextMeshProUGUI standstillstucktext;

    // CHANGED: Added hold-to-respawn timer settings
    [SerializeField] private float holdToRespawnTime = 7f;
    private float rHoldTimer = 0f;

    // Static position override for active dungeon respawns
    private static Vector3? activeOverrideRespawnPosition = null;

    private float nextResetTime;
    private GameObject player1Ref;

    public static void SetOverrideRespawnPoint(Vector3 position)
    {
        activeOverrideRespawnPosition = position;
    }

    public static void ClearOverrideRespawnPoint()
    {
        activeOverrideRespawnPosition = null;
    }

    private void Awake()
    {
        if (mineGeneration == null)
        {
            mineGeneration = FindFirstObjectByType<ChunkedMineGeneration>();
        }

        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private void Start()
    {
        if (standstillstucktext != null)
        {
            StartCoroutine(CheckPlayerStuckRoutine(10f));
        }
    }

    // CHANGED: Added Update loop to monitor holding R for 7 seconds to respawn
    private void Update()
    {
        HandleHoldToRespawn();
    }

    private void HandleHoldToRespawn()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.rKey.isPressed)
        {
            rHoldTimer += Time.deltaTime;
            if (rHoldTimer >= holdToRespawnTime)
            {
                rHoldTimer = 0f;
                RespawnLocalPlayer();
            }
        }
        else
        {
            rHoldTimer = 0f;
        }
    }

    private void RespawnLocalPlayer()
    {
        GameObject localPlayer = GetLocalPlayerObject();
        if (localPlayer != null)
        {
            Vector3 targetPosition;
            if (activeOverrideRespawnPosition.HasValue)
            {
                targetPosition = activeOverrideRespawnPosition.Value;
            }
            else if (mineGeneration != null)
            {
                targetPosition = mineGeneration.GetMineSpawnPosition(localPlayer);
            }
            else
            {
                return;
            }

            RequestPlayerTeleport(localPlayer, targetPosition);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryResetPlayer(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryResetPlayer(collision.collider);
    }

    private void TryResetPlayer(Collider other)
    {
        if (Time.time < nextResetTime)
        {
            return;
        }

        GameObject player = GetPlayerObject(other);
        if (player == null)
        {
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        nextResetTime = Time.time + resetCooldown;

        Vector3 targetPosition;
        if (activeOverrideRespawnPosition.HasValue)
        {
            targetPosition = activeOverrideRespawnPosition.Value;
        }
        else if (mineGeneration != null)
        {
            targetPosition = mineGeneration.GetMineSpawnPosition(player);
        }
        else
        {
            return;
        }

        RequestPlayerTeleport(player, targetPosition);
    }

    private static GameObject GetLocalPlayerObject()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
        {
            return manager.LocalClient != null && manager.LocalClient.PlayerObject != null
                ? manager.LocalClient.PlayerObject.gameObject
                : null;
        }

        GameObject player = GameObject.FindWithTag("Player1");
        return player != null ? player : GameObject.FindWithTag("Player");
    }

    private static void RequestPlayerTeleport(GameObject player, Vector3 targetPosition)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.RequestNetworkTeleport(targetPosition);
        }
        else
        {
            TeleportPlayer(player, targetPosition);
        }
    }

    private static void TeleportPlayer(GameObject playerObj, Vector3 targetPosition)
    {
        if (playerObj == null) return;

        CharacterController[] controllers = playerObj.GetComponentsInChildren<CharacterController>(true);
        foreach (CharacterController controller in controllers)
        {
            controller.enabled = false;
        }

        playerObj.transform.position = targetPosition;
        Physics.SyncTransforms();

        foreach (CharacterController controller in controllers)
        {
            controller.enabled = true;
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

    private IEnumerator CheckPlayerStuckRoutine(float checkInterval)
    {
        while (true)
        {
            if (player1Ref == null)
            {
                player1Ref = GameObject.FindWithTag("Player1");
                if (player1Ref == null) player1Ref = GameObject.FindWithTag("Player");
            }

            if (player1Ref != null)
            {
                Vector3 oldPosition = player1Ref.transform.position;
                yield return new WaitForSeconds(checkInterval);
                Vector3 newPosition = player1Ref.transform.position;

                bool isStuck = Vector3.Distance(oldPosition, newPosition) < 0.05f;
                if (standstillstucktext != null)
                {
                    standstillstucktext.gameObject.SetActive(isStuck);
                }
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }
        }
    }
}
