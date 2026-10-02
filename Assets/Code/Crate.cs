using UnityEngine;
using Unity.Netcode;

public class Crate : NetworkBehaviour
{
    private Depth depth;
    [SerializeField] public int health = 10;
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private GameObject brokenCratePrefab;
    [SerializeField] private GameObject destructionEffect;

    private void Awake()
    {
        depth = FindFirstObjectByType<Depth>();
        maxHealth = 10 * (depth != null ? Mathf.Max(1, depth.depth) : 1);
        health = Mathf.Min(health, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (NetworkSpawnUtility.IsNetworkSessionActive)
        {
            if (!IsServer)
            {
                NetworkPlayerSpawner.Instance?.RequestWorldDamage(transform.position, amount);
                return;
            }

            ApplyWorldDamage(amount);
            NetworkPlayerSpawner.Instance?.BroadcastWorldDamage(transform.position, amount);
            return;
        }

        ApplyWorldDamage(amount);
    }

    public void ApplyWorldDamage(int amount)
    {
        health -= Mathf.Max(0, amount);
        if (health <= 0)
        {
            DestroyCrate();
        }
    }

    private void DestroyCrate()
    {
     GameAudioManager.EnsureInstance().PlayWallBreak();
     if (!IsSpawned || IsServer)
        {
        CurrencyReward.GiveNearestPlayer(transform.position, 10,100);
        }

        if (destructionEffect != null)
        {
            Instantiate(destructionEffect, transform.position, Quaternion.identity);
        }

        if (IsSpawned && IsServer)
        {
            NetworkObject.Despawn(true);
        }
        else if (!IsSpawned)
        {
            Destroy(gameObject);
        }
    }
}
