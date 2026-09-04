using UnityEngine;

public class ExplosionArea : MonoBehaviour
{
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float lifeTime = 0.2f;
    [SerializeField] private string ownerTag;

    public void Setup(int newDamage, float newRadius, string newOwnerTag)
    {
        damage = newDamage;
        radius = newRadius;
        ownerTag = newOwnerTag;
        Destroy(gameObject, lifeTime);

        Collider[] hits = Physics.OverlapSphere(transform.position, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit == null || hit.gameObject == null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(ownerTag) && hit.CompareTag(ownerTag))
            {
                continue;
            }

            if (hit.CompareTag("Player") || hit.CompareTag("Player1") || hit.CompareTag("Player2") || hit.CompareTag("Enemy") || hit.CompareTag("enemy") || hit.CompareTag("Boss") || hit.CompareTag("boss") || hit.CompareTag("Wall") || hit.CompareTag("wall") || hit.CompareTag("walls"))
            {
                PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage);
                    continue;
                }

                EnemyAi enemyAi = hit.GetComponentInParent<EnemyAi>();
                if (enemyAi != null)
                {
                    enemyAi.TakeDamage(damage);
                    continue;
                }

                WallHealth wallHealth = hit.GetComponentInParent<WallHealth>();
                if (wallHealth != null)
                {
                    wallHealth.TakeDamage(damage);
                    continue;
                }

                hit.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
