using UnityEngine;

public enum ProjectileType
{
    Normal,
    Explosive
}

public class Projectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifeTime = 4f;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Normal;
    [SerializeField] private bool explode = false;
    [SerializeField] private float explosionRadius = 2.5f;
    [SerializeField] private int damage = 10;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private LayerMask damageMask = ~0;

    private Vector3 direction;
    private float timer;
    private string ownerTag;
    private float ignorePlayerUntil;

    private void Awake()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }
        body.isKinematic = true;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            collider.isTrigger = true;
        }

        ignorePlayerUntil = Time.time + 0.5f;
    }

    public void SetDirection(Vector3 newDirection)
    {
        direction = newDirection.normalized;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    public void SetOwnerTag(string newOwnerTag)
    {
        ownerTag = newOwnerTag;
    }

    public void SetDamage(int amount)
    {
        damage = amount;
    }

    public void SetProjectileType(ProjectileType newType)
    {
        projectileType = newType;
        explode = newType == ProjectileType.Explosive;
    }

    private void Start()
    {
        timer = lifeTime;
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            TriggerImpact();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (Time.time < ignorePlayerUntil && IsOnPlayerLayer(other.transform))
        {
            return;
        }

        EnemyAi enemy = other.GetComponentInParent<EnemyAi>();
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        WallHealth wall = other.GetComponentInParent<WallHealth>();

        if (wall != null)
        {
            wall.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(damage * 0.1f)));
            return;
        }

        if (IsWall(other.gameObject))
        {
            other.gameObject.SendMessage(
                "TakeDamage",
                Mathf.Max(1, Mathf.RoundToInt(damage * 0.1f)),
                SendMessageOptions.DontRequireReceiver);
            return;
        }

        if (enemy != null)
        {
            if (IsEnemyProjectile())
            {
                enemy.TakeDamage(damage);
                Destroy(gameObject);
            }
            return;
        }

        if (player != null)
        {
            if (!IsEnemyProjectile())
            {
                player.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
    }

    private bool IsDamageableTarget(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        string tag = target.tag;
        return tag == "Player" || tag == "Player1" || tag == "Player2" || tag == "Enemy" || tag == "enemy" || tag == "Boss" || tag == "boss" || tag == "Wall" || tag == "wall" || tag == "walls";
    }

    private bool IsEnemyProjectile()
    {
        return ownerTag == "Enemy" || ownerTag == "enemy" || ownerTag == "Boss" || ownerTag == "boss";
    }

    private static bool IsWall(GameObject target)
    {
        string tag = target.tag;
        return tag == "Wall" || tag == "wall" || tag == "walls";
    }

    private static bool IsOnPlayerLayer(Transform target)
    {
        Transform current = target;
        int playerLayer = LayerMask.NameToLayer("Player");

        if (playerLayer == -1)
        {
            return false;
        }

        while (current != null)
        {
            if (current.gameObject.layer == playerLayer)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void TriggerImpact()
    {
        if (projectileType == ProjectileType.Explosive || explode)
        {
            SpawnExplosion();
            return;
        }

        Destroy(gameObject);
    }

    private void SpawnExplosion()
    {
        if (explosionPrefab != null)
        {
            GameObject explosionObject = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            ExplosionArea explosionArea = explosionObject.GetComponent<ExplosionArea>();
            if (explosionArea == null)
            {
                explosionArea = explosionObject.AddComponent<ExplosionArea>();
            }

            explosionArea.Setup(damage, explosionRadius, ownerTag);
        }
        else
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, damageMask.value, QueryTriggerInteraction.Ignore);
            foreach (Collider col in hits)
            {
                if (col == null || col.gameObject == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(ownerTag) && col.CompareTag(ownerTag))
                {
                    continue;
                }

                if (IsDamageableTarget(col.gameObject))
                {
                    ApplyDamage(col.gameObject);
                }
            }
        }

        Destroy(gameObject);
    }

    private void ApplyDamage(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(ownerTag) && target.CompareTag(ownerTag))
        {
            return;
        }

        PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            return;
        }

        EnemyAi enemyAi = target.GetComponentInParent<EnemyAi>();
        if (enemyAi != null)
        {
            enemyAi.TakeDamage(damage);
            return;
        }

        WallHealth wallHealth = target.GetComponentInParent<WallHealth>();
        if (wallHealth != null)
        {
            wallHealth.TakeDamage(damage);
            return;
        }

        target.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }
}
