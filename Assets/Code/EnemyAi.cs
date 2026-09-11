using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Unity.Netcode;

public enum EnemyAttackType
{
    Melee,
    Ranged
}

public class EnemyAi : NetworkBehaviour
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
    protected int currentHealth;
    protected HealthBarUI healthBarUI;
    protected bool isAttacking;
    protected Depth depth;
    private DungeonWaveManager waveManager;

    protected virtual float MoveSpeed => enemyData != null ? enemyData.walkSpeed : 2.5f;
    protected virtual float StopDistance => enemyData != null ? enemyData.stopDistance : 1.25f;
    protected virtual float RotationSpeed => enemyData != null ? enemyData.rotationSpeed : 5f;
    protected virtual float MinAttackDistance => enemyData != null ? enemyData.minAttackDistance : 0.8f;
    protected virtual float MaxAttackDistance => enemyData != null ? enemyData.maxAttackDistance : 2.2f;
    protected virtual float AttackCooldown => enemyData != null ? enemyData.attackCooldown : 1f;
    protected virtual EnemyAttackType AttackType => enemyData != null ? enemyData.attackType : EnemyAttackType.Melee;
    protected virtual GameObject ProjectilePrefab => enemyData != null ? enemyData.projectilePrefab : null;
    protected virtual WeaponData WeaponData => enemyData != null ? enemyData.weaponData : null;
    protected virtual int MaxHealth
    {
        get
        {
            int baseHealth = enemyData != null ? enemyData.MaxHealth : 100;
            int currentDepth = depth != null ? Mathf.Max(1, depth.depth) : 1;
            return baseHealth * currentDepth;
        }
    }
    public int CurrentHealth => currentHealth;
    public int MaxHealthValue => MaxHealth;
    public string HealthText => currentHealth + "/" + MaxHealth;

    public void SetWaveManager(DungeonWaveManager manager)
    {
        waveManager = manager;
    }

    protected virtual void Awake()
    {
        depth = FindFirstObjectByType<Depth>();
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

    }

    protected virtual void Update()
    {
        if (IsSpawned && !IsServer)
        {
            return;
        }

        if (target == null)
        {
            target = FindClosestTarget();
        }

        if (target == null)
        {
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (isAttacking)
        {
            StopMovement();
        }
        else if (distanceToTarget > StopDistance)
        {
            MoveTowardTarget();
        }
        else
        {
            StopMovement();
        }

        if (distanceToTarget <= MaxAttackDistance && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + AttackCooldown;
            StartCoroutine(PerformAttackAfterDelay());
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

    protected virtual System.Collections.IEnumerator PerformAttackAfterDelay()
    {
        isAttacking = true;
        StopMovement();
        yield return new WaitForSeconds(0.5f);

        if (target != null && Vector3.Distance(transform.position, target.position) <= MaxAttackDistance)
        {
            PerformAttack(target.position);
        }

        isAttacking = false;
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
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * (StopDistance + 0.5f), 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
            {
                continue;
            }

            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(GetAttackDamage());
            }
        }
    }

    protected virtual int GetAttackDamage()
    {
        int baseDamage = WeaponData != null && WeaponData.damage > 0 ? WeaponData.damage : 10;
        int currentDepth = depth != null ? Mathf.Max(1, depth.depth) : 1;
        return baseDamage * currentDepth;
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

    protected virtual void ShootProjectile(Vector3 playerPosition)
    {
        Vector3 spawnPosition = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
        Vector3 direction = playerPosition - spawnPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = transform.forward;
        }

        RaycastHit[] hits = Physics.RaycastAll(spawnPosition, direction.normalized, MaxAttackDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        RaycastHit closestHit = default;
        float closestDistance = Mathf.Infinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.distance < closestDistance)
            {
                closestHit = hit;
                closestDistance = hit.distance;
            }
        }

        if (closestDistance < Mathf.Infinity)
        {
            ApplyDamageToTarget(closestHit.collider.gameObject, GetAttackDamage());
        }

        if (ProjectilePrefab == null)
        {
            return;
        }

        GameObject projectile = Instantiate(ProjectilePrefab, spawnPosition, Quaternion.identity);
        Projectile projectileComponent = projectile.GetComponent<Projectile>();
        if (projectileComponent == null)
        {
            projectileComponent = projectile.AddComponent<Projectile>();
        }

        projectileComponent.SetDirection(direction.normalized);
        projectileComponent.SetOwnerTag(gameObject.tag);
        projectileComponent.SetDamage(GetAttackDamage());

        Vector3 aimDirection = direction.normalized;
        projectile.transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
    }

    public virtual void TakeDamage(int amount)
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

        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth <= 0)
        {
            OnDeath();
        }
    }

    protected virtual void OnDeath()
    {
        CurrencyReward.GiveNearestPlayer(transform.position, 5, 25);

        DungeonWaveManager manager = waveManager != null ? waveManager : GetComponentInParent<DungeonWaveManager>();
        if (manager != null)
        {
            manager.UnregisterEnemy(gameObject);
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

