using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

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
    
    // CHANGED: Replaced explicit cooldown timestamps with dynamic state fields.
    private float currentShootTime;
    private float reloadEndTime;
    private bool isReloading;
    private float nextFireTime;

    public bool IsReloading => isReloading;
    
    // CHANGED: Expose accurate countdown ratios for the UI.
    public float ReloadRatio 
    {
        get 
        {
            if (!isReloading) return 0f;
            float totalReload = (weaponData != null && weaponData.isHitscan) 
                ? (weaponData.hitscanReloadTime > 0 ? weaponData.hitscanReloadTime : 2f) 
                : fireRate;
                
            if (totalReload <= 0f) return 0f;
            
            // Calculates remaining time normalized between 1 (just started) and 0 (finished)
            return Mathf.Clamp01((reloadEndTime - Time.time) / totalReload);
        }
    }

    public float AmmoRatio
    {
        get
        {
            if (weaponData != null && weaponData.isHitscan)
            {
                float maxShoot = weaponData.hitscanShootDuration > 0 ? weaponData.hitscanShootDuration : 3f;
                return Mathf.Clamp01(currentShootTime / maxShoot);
            }
            return 1f; // Projectiles are always returned as fully "ready" before they shoot and trigger reload.
        }
    }

    public void SetWeaponData(WeaponData data)
    {
        weaponData = data;
        if (data != null)
        {
            fireRate = data.cooldown > 0f ? data.cooldown : fireRate;
            
            if (data.isHitscan)
            {
                currentShootTime = data.hitscanShootDuration > 0 ? data.hitscanShootDuration : 3f;
            }
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
        if (!NetworkOwnership.CanControl(this)) return;

        Transform owner = GetOwnerTransform();
        if (owner == null || !IsPlayerOwner(owner)) return;

        if (Keyboard.current == null || Mouse.current == null) return;

        if (weaponData != null && !weaponData.isHitscan && projectilePrefab == null) return;

        bool firePressed = Mouse.current.leftButton.isPressed;
        if (owner.CompareTag("Player1")) firePressed |= Keyboard.current.spaceKey.isPressed;
        else if (owner.CompareTag("Player2")) firePressed |= Keyboard.current.enterKey.isPressed;

        // CHANGED: Reload State Block to block firing while returning clip ammo 
        if (isReloading)
        {
            if (Time.time >= reloadEndTime)
            {
                isReloading = false;
                if (weaponData != null && weaponData.isHitscan)
                {
                    currentShootTime = weaponData.hitscanShootDuration > 0 ? weaponData.hitscanShootDuration : 3f;
                }
            }
            return;
        }

        if (!firePressed) return;

        // CHANGED: Firing execution split into independent Hitscan clips vs Projectile cooldowns
        if (weaponData != null && weaponData.isHitscan)
        {
            currentShootTime -= Time.deltaTime; // Drains the hitscan clip length

            if (Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireRate;
                Fire(owner);
                if (objectToDisableAfterShooting != null) objectToDisableAfterShooting.SetActive(false);
            }

            if (currentShootTime <= 0f)
            {
                isReloading = true;
                reloadEndTime = Time.time + (weaponData.hitscanReloadTime > 0 ? weaponData.hitscanReloadTime : 2f);
                currentShootTime = 0f;
            }
        }
        else
        {
            if (Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireRate;
                
                // For a projectile, the cooldown interval acts as the singular shot reload period
                isReloading = true;
                reloadEndTime = nextFireTime;
                
                Fire(owner);
                if (objectToDisableAfterShooting != null) objectToDisableAfterShooting.SetActive(false);
            }
        }
    }

    private void Fire(Transform owner)
    {
        Vector3 fireDirection = GetPlayerFacingDirection(owner);
        Vector3 fireOrigin = muzzlePoint.position;

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
            if (weaponData != null && weaponData.impactParticlePrefab != null)
            {
                GameObject impactEffect = Instantiate(
                    weaponData.impactParticlePrefab,
                    hit.point,
                    Quaternion.LookRotation(hit.normal)
                );
                Destroy(impactEffect, 0.5f);
            }

            if (owner != null && (hit.transform == owner || hit.transform.IsChildOf(owner)))
            {
                return;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
            else
            {
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