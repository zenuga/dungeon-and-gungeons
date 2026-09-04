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
    [SerializeField] private string[] enemyTags = { "Enemy", "enemy", "Boss", "boss" };

    private float nextFireTime;

    private void Awake()
    {
        if (muzzlePoint == null)
        {
            muzzlePoint = transform;
        }
    }

    private void Update()
    {
        if (projectilePrefab == null)
        {
            return;
        }

        bool firePressed = Input.GetKeyDown(KeyCode.Mouse0);
        if (!firePressed && Keyboard.current != null)
        {
            firePressed = Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        if (!firePressed)
        {
            AimAtNearestVisibleEnemy();
            return;
        }

        if (Time.time < nextFireTime)
        {
            return;
        }

        nextFireTime = Time.time + fireRate;
        Fire();
    }

    private void AimAtNearestVisibleEnemy()
    {
        Transform owner = GetOwnerTransform();
        if (owner == null)
        {
            return;
        }

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

        owner.rotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
    }

    private Transform FindNearestVisibleEnemy()
    {
        Transform nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (string tag in enemyTags)
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject enemy in enemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                if (!HasLineOfSight(enemy.transform.position))
                {
                    continue;
                }

                float dist = Vector3.Distance(muzzlePoint.position, enemy.transform.position);
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    nearest = enemy.transform;
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

    private void Fire()
    {
        Transform owner = GetOwnerTransform();
        if (owner == null)
        {
            return;
        }

        Vector3 fireDirection = owner.forward;
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
    }

    private Transform GetOwnerTransform()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.CompareTag("Player1") || current.CompareTag("Player2") || current.CompareTag("Player") || current.CompareTag("Enemy") || current.CompareTag("enemy"))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }
}
