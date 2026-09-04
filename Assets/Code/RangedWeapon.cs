using UnityEngine;
using UnityEngine.InputSystem;

public class RangedWeapon : MonoBehaviour
{
    [Header("Weapon Setup")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private float fireRate = 0.5f;
    [SerializeField] private float maxAimDistance = 20f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private string[] EnemyTags = { "Enemy", "Boss"};
    private WeaponData weaponData;

    private float nextFireTime;

    public void SetWeaponData(WeaponData data)
    {
        weaponData = data;
        if (data != null)
        {
            fireRate = data.cooldown > 0f ? data.cooldown : fireRate;
        }
    }

    private void Awake()
    {
        if (muzzlePoint == null)
        {
            muzzlePoint = transform;
        }
    }

    private void Update()
    {
        if (!NetworkOwnership.CanControl(this))
        {
            return;
        }

        if (projectilePrefab == null || Keyboard.current == null || Mouse.current == null)
        {
            return;
        }

        Transform owner = GetOwnerTransform();
        if (owner == null || !IsPlayerOwner(owner))
        {
            return;
        }

        bool firePressed = Mouse.current.leftButton.wasPressedThisFrame;
        if (owner.CompareTag("Player1"))
        {
            firePressed |= Keyboard.current.spaceKey.wasPressedThisFrame;
        }
        else if (owner.CompareTag("Player2"))
        {
            firePressed |= Keyboard.current.enterKey.wasPressedThisFrame;
        }

        if (!firePressed)
        {
            AimAtNearestVisibleEnemy(owner);
            return;
        }

        if (Time.time < nextFireTime)
        {
            return;
        }

        nextFireTime = Time.time + fireRate;
        Fire(owner);
    }

    private void AimAtNearestVisibleEnemy(Transform owner)
    {
        Transform bestTarget = FindNearestVisibleEnemy();
        if (bestTarget == null)
        {
            return;
        }

        Vector3 targetDirection = bestTarget.position - muzzlePoint.position;
        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Transform highestOwner = GetHighestPlayerParent(owner);
        highestOwner.rotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
    }

    private Transform FindNearestVisibleEnemy()
    {
        Transform nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (string tag in EnemyTags)
        {
            GameObject[] Enemies = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject Enemy in Enemies)
            {
                if (Enemy == null)
                {
                    continue;
                }

                if (!HasLineOfSight(Enemy.transform.position))
                {
                    continue;
                }

                float dist = Vector3.Distance(muzzlePoint.position, Enemy.transform.position);
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    nearest = Enemy.transform;
                }
            }
        }

        return nearest;
    }

    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 origin = muzzlePoint.position;
        Vector3 direction = targetPosition - origin;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        float distance = direction.magnitude;
        if (distance > maxAimDistance)
        {
            return false;
        }

        RaycastHit hit;
        if (Physics.Raycast(origin, direction.normalized, out hit, distance, obstacleMask.value, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null)
            {
                return true;
            }

            if (hit.collider.transform.position == targetPosition)
            {
                return true;
            }

            return false;
        }

        return true;
    }

    private void Fire(Transform owner)
    {
        Vector3 fireDirection = GetPlayerFacingDirection(owner);
        Transform target = FindNearestVisibleEnemy();
        if (target != null)
        {
            fireDirection = (target.position - muzzlePoint.position).normalized;
            fireDirection.y = 0f;
        }

        GameObject projectileObj = Instantiate(projectilePrefab, muzzlePoint.position, Quaternion.LookRotation(fireDirection, Vector3.up));
        Projectile projectile = projectileObj.GetComponent<Projectile>();
        if (projectile == null)
        {
            projectile = projectileObj.AddComponent<Projectile>();
        }

        projectile.SetDirection(fireDirection.normalized);
        projectile.SetOwnerTag(owner.tag);
        if (weaponData != null)
        {
            PlayerPickupManager pickupManager = owner.GetComponentInParent<PlayerPickupManager>();
            float damageMultiplier = pickupManager != null ? pickupManager.DamageMultiplier : 1f;
            projectile.SetDamage(Mathf.RoundToInt(Mathf.Max(1, weaponData.damage) * damageMultiplier));
        }
    }

    private static Vector3 GetPlayerFacingDirection(Transform owner)
    {
        PlayerController playerController = owner.GetComponent<PlayerController>();
        if (playerController != null)
        {
            Vector3 facingDirection = playerController.FacingDirection;
            facingDirection.y = 0f;
            if (facingDirection.sqrMagnitude > 0.001f)
            {
                return facingDirection.normalized;
            }
        }

        Vector3 ownerDirection = owner.forward;
        ownerDirection.y = 0f;
        return ownerDirection.sqrMagnitude > 0.001f ? ownerDirection.normalized : Vector3.forward;
    }

    private Transform GetOwnerTransform()
    {
        Transform current = transform;
        Transform highestPlayer = null;
        while (current != null)
        {
            if (current.CompareTag("Player1") || current.CompareTag("Player2"))
            {
                highestPlayer = current;
            }

            current = current.parent;
        }

        return highestPlayer;
    }

    private static Transform GetHighestPlayerParent(Transform owner)
    {
        Transform current = owner;
        Transform highestPlayer = owner;

        while (current != null)
        {
            if (current.CompareTag("Player1") || current.CompareTag("Player2"))
            {
                highestPlayer = current;
            }

            current = current.parent;
        }

        return highestPlayer;
    }

    private static bool IsPlayerOwner(Transform owner)
    {
        return owner.CompareTag("Player1") || owner.CompareTag("Player2");
    }
}
