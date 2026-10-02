using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Components;

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
    [Header("Animation Clips")]
    [SerializeField] private Animation enemyAnimation;
    [SerializeField] private AnimationClip walkAnimationClip;
    [SerializeField] private AnimationClip attackAnimationClip;

    protected Transform target;
    protected NavMeshAgent navMeshAgent;
    protected float nextAttackTime;
    protected int currentHealth;
    private readonly NetworkVariable<int> replicatedHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    protected HealthBarUI healthBarUI;
    protected bool isAttacking;
    protected Depth depth;
    private DungeonWaveManager waveManager;
    private bool isBoss;
    private int bossHealthMultiplier = 1;
    private int bossDamageMultiplier = 1;
    private bool isWalkingAnimationPlaying;
    private bool externallyControlled;
    public WeaponData SpecialBossWeaponDrop => enemyData != null ? enemyData.specialBossWeaponDrop : null;

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
            float mineMultiplier = Mathf.Pow(1.3f, currentDepth - 1);
            return Mathf.Max(1, Mathf.RoundToInt(baseHealth * mineMultiplier * (isBoss ? bossHealthMultiplier : 1)));
        }
    }
    public int CurrentHealth => IsSpawned ? replicatedHealth.Value : currentHealth;
    public int MaxHealthValue => MaxHealth;
    public string HealthText => currentHealth + "/" + MaxHealth;

    public void SetWaveManager(DungeonWaveManager manager)
    {
        waveManager = manager;
    }

    public void SetExternallyControlled(bool controlled)
    {
        externallyControlled = controlled;
    }

    public void SetBoss(int healthMultiplier = 5, int damageMultiplier = 2)
    {
        isBoss = true;
        bossHealthMultiplier = Mathf.Max(1, healthMultiplier);
        bossDamageMultiplier = Mathf.Max(1, damageMultiplier);
        currentHealth = MaxHealth;
        if (!IsSpawned) GameAudioManager.EnsureInstance().PlayBossMusic();
    }

    protected virtual void Awake()
    {
        if (GetComponent<NetworkObject>() != null && GetComponent<NetworkTransform>() == null)
        {
            gameObject.AddComponent<NetworkTransform>();
        }

        depth = FindFirstObjectByType<Depth>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
        }

        currentHealth = MaxHealth;
        if (enemyAnimation == null) enemyAnimation = GetComponentInChildren<Animation>(true);
        if (enemyAnimation == null && (walkAnimationClip != null || attackAnimationClip != null))
            enemyAnimation = gameObject.AddComponent<Animation>();

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

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            replicatedHealth.Value = MaxHealth;
            currentHealth = MaxHealth;
        }
        else
        {
            currentHealth = replicatedHealth.Value;
        }
        replicatedHealth.OnValueChanged += OnReplicatedHealthChanged;
        if (isBoss && IsServer) SetBossMusicClientRpc();
    }

    public override void OnNetworkDespawn()
    {
        replicatedHealth.OnValueChanged -= OnReplicatedHealthChanged;
    }

    private void OnReplicatedHealthChanged(int previousHealth, int newHealth)
    {
        currentHealth = newHealth;
        if (newHealth < previousHealth && newHealth > 0 && enemyData != null)
            GameAudioManager.EnsureInstance().PlayEffect(enemyData.hurtSound);
        if (newHealth <= 0 && previousHealth > 0 && enemyData != null)
            GameAudioManager.EnsureInstance().PlayEffect(enemyData.deathSound);
    }

    protected virtual void Update()
    {
        if (IsSpawned && !IsServer)
        {
            return;
        }

        if (externallyControlled) return;

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
            PlayWalkAnimation();
        }
        else
        {
            StopMovement();
            StopWalkAnimation();
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
        PlayAttackAnimation();
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
        float mineMultiplier = Mathf.Pow(1.3f, currentDepth - 1);
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * mineMultiplier * (isBoss ? bossDamageMultiplier : 1)));
    }

    private void PlayWalkAnimation()
    {
        if (enemyAnimation == null || walkAnimationClip == null || isWalkingAnimationPlaying) return;
        if (enemyAnimation.GetClip(walkAnimationClip.name) == null)
            enemyAnimation.AddClip(walkAnimationClip, walkAnimationClip.name);
        enemyAnimation[walkAnimationClip.name].wrapMode = WrapMode.Loop;
        enemyAnimation.Play(walkAnimationClip.name);
        isWalkingAnimationPlaying = true;
    }

    private void StopWalkAnimation()
    {
        if (enemyAnimation == null || !isWalkingAnimationPlaying) return;
        enemyAnimation.Stop(walkAnimationClip != null ? walkAnimationClip.name : null);
        isWalkingAnimationPlaying = false;
    }

    private void PlayAttackAnimation()
    {
        if (enemyAnimation != null && attackAnimationClip != null)
        {
            if (walkAnimationClip != null) enemyAnimation.Stop(walkAnimationClip.name);
            if (enemyAnimation.GetClip(attackAnimationClip.name) == null)
                enemyAnimation.AddClip(attackAnimationClip, attackAnimationClip.name);
            enemyAnimation.Play(attackAnimationClip.name);
        }
        isWalkingAnimationPlaying = false;
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
        NetworkSpawnUtility.SpawnIfNetworked(projectile);
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
        if (IsSpawned && IsServer)
        {
            replicatedHealth.Value = currentHealth;
        }
        if (currentHealth <= 0)
        {
            if (!IsSpawned && enemyData != null) GameAudioManager.EnsureInstance().PlayEffect(enemyData.deathSound);
            OnDeath();
        }
        else if (!IsSpawned && enemyData != null)
            GameAudioManager.EnsureInstance().PlayEffect(enemyData.hurtSound);
    }

    protected virtual void OnDeath()
    {
        if (isBoss)
        {
            if (IsSpawned && IsServer) StopBossMusicClientRpc();
            else GameAudioManager.EnsureInstance().StopBossMusic();
        }
        if (!IsSpawned || IsServer)
        {
            int minimum = enemyData != null ? enemyData.minimumCurrencyReward : 5;
            int maximum = enemyData != null ? enemyData.maximumCurrencyReward : 25;
            CurrencyReward.GiveNearestPlayer(transform.position, minimum, Mathf.Max(minimum, maximum));
        }

        DungeonWaveManager manager = waveManager != null ? waveManager : GetComponentInParent<DungeonWaveManager>();
        if (manager != null)
        {
            manager.UnregisterEnemy(gameObject);
        }

        NetworkSpawnUtility.DespawnOrDestroy(gameObject);
    }

    [ClientRpc]
    private void SetBossMusicClientRpc() => GameAudioManager.EnsureInstance().PlayBossMusic();

    [ClientRpc]
    private void StopBossMusicClientRpc() => GameAudioManager.EnsureInstance().StopBossMusic();

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
            healthBarUI = GetComponentInChildren<HealthBarUI>(true);
            if (healthBarUI != null)
            {
                healthBarUI.Initialize(healthBarUI.GetComponent<Image>(), this);
                return;
            }
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
