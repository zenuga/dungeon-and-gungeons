using UnityEngine;
using Unity.Netcode;

public class WallHealth : NetworkBehaviour
{
    private Depth depth;
    public int Health = 10;

    private void Awake()
    {
        depth = FindFirstObjectByType<Depth>();
        UpdateWallHealth();
    }

    public void SetHealthForCurrentDepth()
    {
        if (depth == null)
        {
            depth = FindFirstObjectByType<Depth>();
        }

        Health = 10 * (depth != null ? Mathf.Max(1, depth.depth) : 1);
    }

    public void UpdateWallHealth()
    {
        SetHealthForCurrentDepth();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (IsSpawned && !IsServer)
        {
            TakeDamageServerRpc(amount);
            return;
        }

        ApplyDamage(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int amount)
    {
        ApplyDamage(amount);
    }

    private void ApplyDamage(int amount)
    {
        Health -= amount;
        if (Health <= 0)
        {
            if ((!IsSpawned || IsServer) && Random.value <= 0.05f)
            {
                CurrencyReward.GiveNearestPlayer(transform.position, 1, 20);
            }

            if (IsSpawned && IsServer)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}