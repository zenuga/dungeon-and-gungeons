using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Netcode;

public class RangedWeapon : NetworkBehaviour
{
    [Header("Weapon Setup")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private float fireRate = 0.5f;
    [SerializeField] private GameObject objectToDisableAfterShooting;

    [Header("Reload UI Setup")]
    [SerializeField] private string reloadTag = "Reload";
    private Image reloadImage;
    private GameObject reloadUIObject;

    private WeaponData weaponData;
    private float nextFireTime;
    private bool isPickedUp;

    public void SetWeaponData(WeaponData data)
    {
        weaponData = data;
        if (data != null)
        {
            fireRate = data.cooldown > 0f ? data.cooldown : fireRate;
        }

        ActivateWeaponUI();
    }

    private void Awake()
    {
        if (muzzlePoint == null)
        {
            muzzlePoint = transform;
        }

        FindReloadUI();
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
            if (isPickedUp)
            {
                DeactivateWeaponUI();
            }
            return;
        }

        // Activate UI if picked up and not yet active
        if (!isPickedUp)
        {
            ActivateWeaponUI();
        }

        UpdateReloadUI();

        if (projectilePrefab == null || Keyboard.current == null || Mouse.current == null)
        {
            return;
        }

        // Changed to isPressed so holding the button works and the UI loops smoothly
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

    private void FindReloadUI()
    {
        if (reloadImage != null) return;

        GameObject reloadObj = GameObject.FindGameObjectWithTag(reloadTag);
        if (reloadObj != null)
        {
            reloadUIObject = reloadObj;
            
            // Search all child images to find the one meant for filling (avoids grabbing backgrounds)
            Image[] images = reloadObj.GetComponentsInChildren<Image>(true);
            foreach (Image img in images)
            {
                if (img.type == Image.Type.Filled)
                {
                    reloadImage = img;
                    break;
                }
            }

            // Fallback: If no image was set to filled, grab the first one and force it to be filled
            if (reloadImage == null)
            {
                reloadImage = reloadObj.GetComponentInChildren<Image>(true);
                if (reloadImage != null)
                {
                    reloadImage.type = Image.Type.Filled;
                    reloadImage.fillMethod = Image.FillMethod.Radial360; // Or whatever style you prefer
                }
            }

            // Ensure it starts disabled until picked up
            if (!isPickedUp && reloadUIObject != null)
            {
                reloadUIObject.SetActive(false);
            }
        }
    }

    private void ActivateWeaponUI()
    {
        isPickedUp = true;
        if (reloadUIObject == null)
        {
            FindReloadUI();
        }

        if (reloadUIObject != null)
        {
            reloadUIObject.SetActive(true);
        }
    }

    private void DeactivateWeaponUI()
    {
        isPickedUp = false;
        if (reloadUIObject != null)
        {
            reloadUIObject.SetActive(false);
        }
    }

    private void UpdateReloadUI()
    {
        if (reloadImage == null)
        {
            FindReloadUI();
            if (reloadImage == null) return;
        }

        if (fireRate <= 0f)
        {
            reloadImage.fillAmount = 1f;
            return;
        }
        if (reloadImage.fillAmount == 1f)
        {
            objectToDisableAfterShooting?.SetActive(true);
            return;
        }

        float timeRemaining = nextFireTime - Time.time;
        if (timeRemaining <= 0f || timeRemaining == 0f)
        {
            reloadImage.fillAmount = 1f;
        }
        else
        {
            float fillRatio = 1f - (timeRemaining / fireRate);
            reloadImage.fillAmount = Mathf.Clamp01(fillRatio);
        }
    }

    private void Fire(Transform owner)
    {
        Vector3 fireDirection = GetPlayerFacingDirection(owner);
        Vector3 fireOrigin = owner.position + fireDirection * 0.75f;

        GameObject projectileObj = Instantiate(projectilePrefab, fireOrigin, Quaternion.LookRotation(fireDirection, Vector3.up));
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