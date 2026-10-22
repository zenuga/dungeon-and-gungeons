using System.Collections.Generic;
using UnityEngine;

public class BombExplosionTrigger : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.2f;
    private HashSet<GameObject> hitEntities = new HashSet<GameObject>();

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // 1. Damage Player (20% of max health)
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player != null && hitEntities.Add(player.gameObject))
        {
            int damage = Mathf.RoundToInt(player.MaxHealthValue * 0.2f);
            player.TakeDamage(damage);
            return;
        }

        // 2. Damage Enemy (40% of current health)
        EnemyAi enemy = other.GetComponentInParent<EnemyAi>();
        if (enemy != null && hitEntities.Add(enemy.gameObject))
        {
            int damage = Mathf.RoundToInt(enemy.CurrentHealth * 0.4f);
            enemy.TakeDamage(damage);
            return;
        }

        // 3. Destroy Wall (1 shot no matter the health)
        if (other.CompareTag("wall"))
        {
            WallHealth wallHealth = other.GetComponentInParent<WallHealth>();
            GameObject breakable = wallHealth != null ? wallHealth.gameObject : other.gameObject;
            // Wall colliders can be children of a dungeon prefab root. Never destroy the
            // hierarchy root: only damage the object that owns the breakable component.
            if (breakable.GetComponent<DungeonWaveManager>() != null || breakable.GetComponent<Dungeonenter>() != null)
            {
                return;
            }

            if (!hitEntities.Add(breakable)) return;
            if (wallHealth != null)
                wallHealth.TakeDamage(999999);
            else
                breakable.SendMessage("TakeDamage", 999999, SendMessageOptions.DontRequireReceiver);
        }
    }
}
