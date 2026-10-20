using UnityEngine;
using Unity.Netcode;

public class WallHealth : NetworkBehaviour
{
    private Depth depth;
    [SerializeField] public int Health = 10;

    private void Awake()
    {
        depth = FindFirstObjectByType<Depth>();
        UpdatewallHealth();
    }

    public void SetHealthForCurrentDepth()
    {
        if (depth == null)
        {
            depth = FindFirstObjectByType<Depth>();
        }

        Health = 10 * (depth != null ? Mathf.Max(1, depth.depth) : 1);
    }

    public void UpdatewallHealth()
    {
        SetHealthForCurrentDepth();
    }

    public void TakeDamage(int amount)
    {
        TakeDamageAtPosition(amount, transform.position, Vector3.up);
    }

    public void TakeDamageAtPosition(int amount, Vector3 hitPosition, Vector3 surfaceNormal)
    {
        if (amount <= 0)
        {
            return;
        }

        if (NetworkSpawnUtility.IsNetworkSessionActive)
        {
            if (!IsServer)
            {
                if (IsSpawned)
                {
                    TakeDamageServerRpc(amount, hitPosition, surfaceNormal);
                }
                else
                {
                    NetworkPlayerSpawner.Instance?.RequestWorldDamage(hitPosition, amount);
                }
                return;
            }

            if (IsSpawned)
            {
                PlayRockWallImpactClientRpc(hitPosition, surfaceNormal);
            }
            else
            {
                PlayLocalRockWallImpact(hitPosition, surfaceNormal);
            }
            ApplyDamage(amount);
            if (!IsSpawned)
            {
                NetworkPlayerSpawner.Instance?.BroadcastWorldDamage(hitPosition, amount);
            }
            return;
        }

        if (IsSpawned && !IsServer)
        {
            TakeDamageServerRpc(amount, hitPosition, surfaceNormal);
            return;
        }

        PlayLocalRockWallImpact(hitPosition, surfaceNormal);
        ApplyDamage(amount);
    }

    public void ApplyWorldDamage(int amount)
    {
        if (amount > 0)
        {
            ApplyDamage(amount);
        }
    }

    public void ApplyWorldDamageAtPosition(int amount, Vector3 hitPosition, Vector3 surfaceNormal)
    {
        if (amount <= 0) return;
        PlayLocalRockWallImpact(hitPosition, surfaceNormal);
        ApplyDamage(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int amount, Vector3 hitPosition, Vector3 surfaceNormal)
    {
        PlayRockWallImpactClientRpc(hitPosition, surfaceNormal);
        ApplyDamage(amount);
    }

    [ClientRpc]
    private void PlayRockWallImpactClientRpc(Vector3 hitPosition, Vector3 surfaceNormal)
    {
        PlayLocalRockWallImpact(hitPosition, surfaceNormal);
    }

    private void PlayLocalRockWallImpact(Vector3 hitPosition, Vector3 surfaceNormal)
    {
        if (!gameObject.name.ToLowerInvariant().Contains("rock wall")) return;
        GameAudioManager.EnsureInstance().SpawnRockWallImpact(hitPosition, surfaceNormal);
    }

    private void ApplyDamage(int amount)
    {
        Health -= amount;
        if (Health <= 0)
        {
            GameAudioManager.EnsureInstance().PlayWallBreak();
            WallBreakEncounterManager.TrySpawnFromBrokenWall(transform.position);
            if ((!IsSpawned || IsServer) && Random.value <= 0.05f)
            {
                CurrencyReward.GiveNearestPlayer(transform.position, 1, 20);
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
}
