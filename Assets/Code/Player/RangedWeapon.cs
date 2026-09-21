using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

// Interface fallback if your game uses an IDamageable interface
public interface IDamageable
{
    void TakeDamage(int damage);
}

public class RangedWeapon : NetworkBehaviour
{
    [Header("Weapon Setup")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private float fireRate = 0.5f;
    [SerializeField] private GameObject objectToDisableAfterShooting;

    private WeaponData weaponData;
    private float nextFireTime;

    // Exposed for PlayerPickupManager to read for the Reload UI
    public float CurrentFireRate => fireRate;
    public float NextFireTime => nextFireTime;

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
        // Fallback in case muzzlePoint is left unassigned in the inspector
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

        Transform owner = GetOwnerTransform();
        if (owner == null || !IsPlayerOwner(owner))
        {
            return;
        }

        if (Keyboard.current == null || Mouse.current == null)
        {
            return;
        }

        // If not hitscan, standard projectile safety check
        if (weaponData != null && !weaponData.isHitscan && projectilePrefab == null)
        {
            return;
        }

        bool firePressed = Mouse.current.leftButton.isPressed;
        if (owner.CompareTag("Player1"))
        {
            firePressed |= Keyboard.current.spaceKey.isPressed;
        }
        else if (owner.CompareTag("Player2"))
        {
            firePressed |= Keyboard.current.enterKey.isPressed;
        }

        if (!firePressed)
        {
            return;
        }

        if (Time.time < nextFireTime)
        {
            return;
        }

        nextFireTime = Time.time + fireRate;
        Fire(owner);

        if (objectToDisableAfterShooting != null)
        {
            objectToDisableAfterShooting.SetActive(false);
        }
    }

    private void Fire(Transform owner)
    {
        Vector3 fireDirection = GetPlayerFacingDirection(owner);
        Vector3 fireOrigin = muzzlePoint.position;

        // Calculate player damage multiplier
        PlayerPickupManager pickupManager = owner.GetComponentInParent<PlayerPickupManager>();
        float damageMultiplier = pickupManager != null ? pickupManager.DamageMultiplier : 1f;
        int calculatedDamage = weaponData != null ? Mathf.RoundToInt(Mathf.Max(1, weaponData.damage) * damageMultiplier) : 1;

        if (weaponData != null && weaponData.isHitscan)
        {
            PerformHitscan(fireOrigin, fireDirection, owner, calculatedDamage);
        }
        else
        {
            PerformProjectile(fireOrigin, fireDirection, owner, calculatedDamage);
        }
    }

    private void PerformHitscan(Vector3 origin, Vector3 direction, Transform owner, int damage)
    {
        float range = weaponData != null ? weaponData.hitscanRange : 100f;
        LayerMask mask = weaponData != null ? weaponData.hitscanLayers : ~0;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, mask))
        {
            // Spawn impact particle system at the hit location pointing away from the hit surface
            if (weaponData != null && weaponData.impactParticlePrefab != null)
            {
                GameObject impactEffect = Instantiate(
                    weaponData.impactParticlePrefab,
                    hit.point,
                    Quaternion.LookRotation(hit.normal)
                );

                // Destroy particle system after 0.5 seconds
                Destroy(impactEffect, 0.5f); // <--- CHANGED: Reduced particle lifespan to 0.5s
            }

            // <--- ADDED: Apply full damage without reduction to hit target
            // Ignore hitting the owner or the owner's children
            if (owner != null && (hit.transform == owner || hit.transform.IsChildOf(owner)))
            {
                return;
            }

            // Try applying damage via IDamageable interface first
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage); // Full damage with 0 range falloff
            }
            else
            {
                // Fallback: Broadcast TakeDamage message to target component hierarchy
                hit.collider.gameObject.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void PerformProjectile(Vector3 origin, Vector3 direction, Transform owner, int damage)
    {
        if (projectilePrefab == null) return;

        GameObject projectileObj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction, Vector3.up));
        Projectile projectile = projectileObj.GetComponent<Projectile>();
        
        if (projectile == null)
        {
            projectile = projectileObj.AddComponent<Projectile>();
        }

        projectile.SetDirection(direction.normalized);
        projectile.SetOwnerTag(owner.tag);
        projectile.SetDamage(damage);
    }

    private static Vector3 GetPlayerFacingDirection(Transform owner)
    {
        PlayerMouseAim mouseAim = owner.GetComponentInChildren<PlayerMouseAim>(true);
        if (mouseAim != null && mouseAim.AimDirection.sqrMagnitude > 0.001f)
        {
            Vector3 aimDirection = mouseAim.AimDirection;
            aimDirection.y = 0f;
            return aimDirection.normalized;
        }

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

    private static bool IsPlayerOwner(Transform owner)
    {
        return owner.CompareTag("Player1") || owner.CompareTag("Player2");
    }
}