using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FallResetTrigger : MonoBehaviour
{
    [SerializeField] private ChunkedMineGeneration mineGeneration;
    [SerializeField] private float resetCooldown = 0.5f;

    private float nextResetTime;

    private void Awake()
    {
        if (mineGeneration == null)
        {
            mineGeneration = FindFirstObjectByType<ChunkedMineGeneration>();
        }

        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
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
        if (Time.time < nextResetTime || mineGeneration == null)
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
        mineGeneration.TeleportPlayerToMineSpawn(player);
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