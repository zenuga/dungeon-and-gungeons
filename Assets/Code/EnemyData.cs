using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Dungeon/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("health")]
    public int maxHealth = 100;

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

    [Header("Prefabs")]
    public GameObject weaponPrefab;
    public GameObject projectilePrefab;
}
