using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public enum EnemyAttackType
{
    Melee,
    Ranged
}

public class EnemyAi : MonoBehaviour
{
    [Header("Enemy Data")]
    [SerializeField] protected EnemyData enemyData;

    [Header("Runtime")]
    [SerializeField] protected Transform projectileSpawnPoint;
    [SerializeField] protected string[] targetTags = {"Player1", "Player2" };
    [SerializeField] protected Image enemyHealthBarbackgroundImagePrefab;

    protected Transform target;
    protected NavMeshAgent navMeshAgent;
    protected float nextAttackTime;
    protected GameObject currentWeapon;
    protected int currentHealth;
    protected HealthBarUI healthBarUI;

    protected virtual float MoveSpeed => enemyData != null ? enemyData.walkSpeed : 2.5f;
    protected virtual float StopDistance => enemyData != null ? enemyData.stopDistance : 1.25f;
    protected virtual float RotationSpeed => enemyData != null ? enemyData.rotationSpeed : 5f;
    protected virtual float MinAttackDistance => enemyData != null ? enemyData.minAttackDistance : 0.8f;
    protected virtual float MaxAttackDistance => enemyData != null ? enemyData.maxAttackDistance : 2.2f;
    protected virtual float AttackCooldown => enemyData != null ? enemyData.attackCooldown : 1f;
    protected virtual EnemyAttackType AttackType => enemyData != null ? enemyData.attackType : EnemyAttackType.Melee;
    protected virtual GameObject ProjectilePrefab => enemyData != null ? enemyData.projectilePrefab : null;
    protected virtual GameObject WeaponPrefab => enemyData != null ? enemyData.weaponPrefab : null;
    protected virtual int MaxHealth => enemyData != null ? enemyData.MaxHealth : 100;
    public int CurrentHealth => currentHealth;
    public int MaxHealthValue => MaxHealth;
    public string HealthText => currentHealth + "/" + MaxHealth;

    protected virtual void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
        }

        currentHealth = MaxHealth;

        CreateHealthBar();

        navMeshAgent.speed = MoveSpeed;
        navMeshAgent.stoppingDistance = StopDistance;
        navMeshAgent.angularSpeed = RotationSpeed * 45f;
        navMeshAgent.autoBraking = true;
        navMeshAgent.updateRotation = true;

        if (projectileSpawnPoint == null)
        {
            projectileSpawnPoint = transform;
        }

        if (weaponPrefabExists() && currentWeapon == null)
        {
            SpawnWeaponVisual();
        }
    }

    protected virtual void Update()
    {
        Debug.Log(name + " health: " + HealthText, this);

        if (target == null)
        {
            target = FindClosestTarget();
        }

        if (target == null)
        {
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget > StopDistance)
        {
            MoveTowardTarget();
        }
        else
        {
            StopMovement();
        }

        if (distanceToTarget >= MinAttackDistance && distanceToTarget <= MaxAttackDistance && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + AttackCooldown;
            PerformAttack(target.position);
        }
    }

    protected virtual void MoveTowardTarget()
    {
        if (navMeshAgent == null)
        {
            return;
        }

        navMeshAgent.speed = MoveSpeed;
        navMeshAgent.stoppingDistance = StopDistance;
        navMeshAgent.angularSpeed = RotationSpeed * 45f;
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(target.position);
    }

    protected virtual void StopMovement()
    {
        if (navMeshAgent == null)
        {
            return;
        }

        navMeshAgent.isStopped = true;
        navMeshAgent.ResetPath();
    }

    protected virtual void PerformAttack(Vector3 playerPosition)
    {
        if (AttackType == EnemyAttackType.Melee)
        {
            SwingAttack();
        }
        else if (AttackType == EnemyAttackType.Ranged)
        {
            ShootProjectile(playerPosition);
        }
    }

    protected virtual void SwingAttack()
    {
        Debug.Log(name + " performs a melee swing attack.");

        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * (StopDistance + 0.5f), 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
            {
                continue;
            }

            string targetTag = hit.gameObject.tag;
            if (targetTag == "Player" || targetTag == "Player1" || targetTag == "Player2")
            {
                ApplyDamageToTarget(hit.gameObject, 10);
                Debug.Log("Enemy hit player: " + hit.gameObject.name);
            }
        }
    }

    protected virtual void ApplyDamageToTarget(GameObject target, int damageAmount)
    {
        if (target == null)
        {
            return;
        }

        PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damageAmount);
            return;
        }

        EnemyAi enemyAi = target.GetComponentInParent<EnemyAi>();
        if (enemyAi != null)
        {
            enemyAi.TakeDamage(damageAmount);
            return;
        }

        WallHealth wallHealth = target.GetComponentInParent<WallHealth>();
        if (wallHealth != null)
        {
            wallHealth.TakeDamage(damageAmount);
            return;
        }

        target.SendMessage("TakeDamage", damageAmount, SendMessageOptions.DontRequireReceiver);
    }

    protected virtual void SpawnWeaponVisual()
    {
        if (!weaponPrefabExists())
        {
            return;
        }

        currentWeapon = Instantiate(WeaponPrefab, projectileSpawnPoint != null ? projectileSpawnPoint : transform);
    }

    protected virtual bool weaponPrefabExists()
    {
        return WeaponPrefab != null;
    }

    protected virtual void ShootProjectile(Vector3 playerPosition)
    {
        if (ProjectilePrefab == null)
        {
            Debug.LogWarning(name + " is a ranged enemy but no projectile prefab was assigned.");
            return;
        }

        Vector3 spawnPosition = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
        Vector3 direction = playerPosition - spawnPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = transform.forward;
        }

        GameObject projectile = Instantiate(ProjectilePrefab, spawnPosition, Quaternion.identity);
        Projectile projectileComponent = projectile.GetComponent<Projectile>();
        if (projectileComponent == null)
        {
            projectileComponent = projectile.AddComponent<Projectile>();
        }

        projectileComponent.SetDirection(direction.normalized);
        projectileComponent.SetOwnerTag(gameObject.tag);
        projectileComponent.SetDamage(10);

        Vector3 aimDirection = direction.normalized;
        projectile.transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
    }

    public virtual void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log(name + " health: " + HealthText, this);

        if (currentHealth <= 0)
        {
            OnDeath();
        }
    }

    protected virtual void OnDeath()
    {
        var waveManager = GetComponentInParent<DungeonWaveManager>();
        if (waveManager != null)
        {
            waveManager.UnregisterEnemy(gameObject);
        }

        Destroy(gameObject);
    }

    protected virtual Transform FindClosestTarget()
    {
        Transform closestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (string tag in targetTags)
        {
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);

            foreach (GameObject obj in taggedObjects)
            {
                if (obj == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = obj.transform;
                }
            }
        }

        return closestTarget;
    }
    protected virtual void CreateHealthBar()
    {
        if (enemyHealthBarbackgroundImagePrefab == null)
        {
            Debug.LogWarning(name + " has no enemy health-bar background Image assigned.", this);
            return;
        }

        Image spawnedHealthBar = Instantiate(enemyHealthBarbackgroundImagePrefab, transform);
        spawnedHealthBar.gameObject.SetActive(true);
        healthBarUI = spawnedHealthBar.GetComponent<HealthBarUI>();

        if (healthBarUI == null)
        {
            healthBarUI = spawnedHealthBar.gameObject.AddComponent<HealthBarUI>();
        }

        healthBarUI.Initialize(spawnedHealthBar, this);
    }
}

