using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ExplosionArea : NetworkBehaviour
{
    [SerializeField] private int damage = 60;
    [SerializeField] private float lifeTime = 0.2f;
    [SerializeField] private string ownerTag;

    private readonly HashSet<GameObject> hitTargets = new HashSet<GameObject>();

    public void Setup(int newDamage, string newOwnerTag)
    {
        damage = newDamage;
        ownerTag = newOwnerTag;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        Destroy(gameObject, lifeTime);
    }

    public void Setup(int newDamage, float unusedRadius, string newOwnerTag)
    {
        Setup(newDamage, newOwnerTag);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || other.gameObject == null)
        {
            return;
        }

        // Ignore map boundary walls tagged "walls"
        if (other.CompareTag("walls") || other.transform.root.CompareTag("walls"))
        {
            return;
        }

        // Determine the primary target entity to prevent hitting multiple child colliders
        GameObject targetRoot = GetPrimaryTargetObject(other);
        if (hitTargets.Contains(targetRoot))
        {
            return;
        }

        hitTargets.Add(targetRoot);

        ApplyDamage(other);
    }

    private GameObject GetPrimaryTargetObject(Collider hit)
    {
        PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null) return playerHealth.gameObject;

        EnemyAi enemyAi = hit.GetComponentInParent<EnemyAi>();
        if (enemyAi != null) return enemyAi.gameObject;

        WallHealth wallHealth = hit.GetComponentInParent<WallHealth>();
        if (wallHealth != null) return wallHealth.gameObject;

        return hit.transform.root.gameObject;
    }

    private void ApplyDamage(Collider hit)
    {
        // 1. Player Damage (25% to original shooter, 20% to other player)
        PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            bool isOriginalShooter = !string.IsNullOrEmpty(ownerTag) &&
                                     (hit.CompareTag(ownerTag) ||
                                      hit.transform.root.CompareTag(ownerTag) ||
                                      player.CompareTag(ownerTag));

            float percentMultiplier = isOriginalShooter ? 0.25f : 0.20f;
            int playerDamage = Mathf.RoundToInt(damage * percentMultiplier);
            player.TakeDamage(playerDamage);
            return;
        }

        // 2. Enemy Damage (100% normal damage)
        EnemyAi enemyAi = hit.GetComponentInParent<EnemyAi>();
        if (enemyAi != null)
        {
            enemyAi.TakeDamage(damage);
            return;
        }

        // 3. Wall Damage (100% normal damage)
        WallHealth wallHealth = hit.GetComponentInParent<WallHealth>();
        if (wallHealth != null)
        {
            wallHealth.TakeDamage(damage);
            return;
        }

        // 4. Fallback for destructibles using tag checks ("wall", "enemy", "boss")
        if (IsDamageableTag(hit.gameObject) || IsDamageableTag(hit.transform.root.gameObject))
        {
            hit.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }
    }

    private bool IsDamageableTag(GameObject obj)
    {
        string t = obj.tag;
        return t == "Player1" || t == "Player2" || 
               t == "enemy" || t == "boss" || 
               t == "wall";
    }
}