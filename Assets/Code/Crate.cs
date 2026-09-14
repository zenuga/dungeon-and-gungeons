using UnityEngine;
using Unity.Netcode;

public class Crate : NetworkBehaviour
{
    private Depth depth;
    [SerializeField] public int health = 10;
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private GameObject brokenCratePrefab;
    [SerializeField] private GameObject destructionEffect;

    public void TakeDamage(int amount)
    {
        void awake ()
        {
            maxHealth = 10 * (depth != null ? Mathf.Max(1, depth.depth) : 1);
            health = maxHealth;
        }
        health -= amount;
        if (health <= 0)
        {
            DestroyCrate();
        }
    }

    private void DestroyCrate()
    {
     if (!IsSpawned || IsServer)
        {
        CurrencyReward.GiveNearestPlayer(transform.position, 10,100);
        }

        if (destructionEffect != null)
        {
            Instantiate(destructionEffect, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}