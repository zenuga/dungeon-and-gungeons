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
        // 1. Completely ignore the owner of the explosion
        if (!string.IsNullOrEmpty(ownerTag) && (other.CompareTag(ownerTag) || other.transform.root.CompareTag(ownerTag)))
        {
            return;
        }

        // 2. Determine the primary parent object to prevent hitting multiple child colliders on the same entity
        GameObject targetRoot = GetPrimaryTargetObject(other);
        if (hitTargets.Contains(targetRoot))
        {
            return;
        }

        hitTargets.Add(targetRoot);

        // 3. Apply damage to health components or via SendMessage
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
        // Fixed: Target hit collider for PlayerHealth instead of 'this'
        PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            int playerDamage = Mathf.RoundToInt(damage * 0.5f);
            player.TakeDamage(playerDamage);
            return;
        }

        EnemyAi enemyAi = hit.GetComponentInParent<EnemyAi>();
        if (enemyAi != null)
        {
            enemyAi.TakeDamage(damage);
            return;
        }

        WallHealth wallHealth = hit.GetComponentInParent<WallHealth>();
        if (wallHealth != null)
        {
            wallHealth.TakeDamage(damage);
            return;
        }

        // Fallback for bosses, destructible walls using SendMessage
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