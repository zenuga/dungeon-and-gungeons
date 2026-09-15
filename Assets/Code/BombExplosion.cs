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
        GameObject rootObject = other.transform.root.gameObject;
        if  (other.CompareTag("wall"))
        {
            if (hitEntities.Add(other.gameObject) && hitEntities.Add(rootObject))
            {
                other.SendMessage("TakeDamage", 999999, SendMessageOptions.DontRequireReceiver);
                other.SendMessage("DestroyWall", SendMessageOptions.DontRequireReceiver);
                Destroy(other.gameObject);
            }
        }
    }
}