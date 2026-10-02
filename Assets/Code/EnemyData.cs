using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Dungeon/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("health")]
    public int maxHealth = 100;
    [Min(0)] public int minimumCurrencyReward = 5;
    [Min(0)] public int maximumCurrencyReward = 25;
    [Header("Audio")]
    public AudioClip hurtSound;
    public AudioClip deathSound;

    public int MaxHealth
    {
        get => maxHealth;
        set => maxHealth = value;
    }

    [Header("Movement")]
    public float walkSpeed = 2.5f;
    public float stopDistance = 1.25f;
    public float rotationSpeed = 5f;

    [Header("Attack")]
    public EnemyAttackType attackType = EnemyAttackType.Melee;
    public float minAttackDistance = 0.8f;
    public float maxAttackDistance = 2.2f;
    public float attackCooldown = 1f;
    public WeaponData weaponData;

    [Header("Prefabs")]
    public GameObject projectilePrefab;
    [Tooltip("Weapon dropped when this enemy is spawned as a boss.")]
    public WeaponData specialBossWeaponDrop;
}
