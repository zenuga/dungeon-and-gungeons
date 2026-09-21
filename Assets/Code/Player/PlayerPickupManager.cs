using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class PlayerPickupManager : NetworkBehaviour
{
    public enum PlayerType { Player1, Player2 }

    [Header("Player Setup")]
    [SerializeField] private PlayerType playerType = PlayerType.Player1;
    [SerializeField] private GameObject pickupRangeSphere;
    [SerializeField] private Transform handTransform;
    [SerializeField] private Transform dropPoint;

    [Header("UI References")]
    public GameObject pickupUI;
    [SerializeField] private Image weaponOrMiscImage;
    [SerializeField] private TextMeshProUGUI weaponOrMiscText;
    [SerializeField] private Image potionImage;
    [SerializeField] private TextMeshProUGUI potionText;
    [SerializeField] private Image bombImage;
    [SerializeField] private TextMeshProUGUI bombText;
    [SerializeField] private GameObject activePotionEffectImage;
    [SerializeField] private TextMeshProUGUI activePotionEffectCountdown;
    
    [Header("Reload UI")]
    [SerializeField] private GameObject reloadUIObject;
    [SerializeField] private Image reloadImage;

    [Header("Inventory Setup")]
    [SerializeField] private int maxPotions = 5;
    [SerializeField] private int maxBombs = 15;

    private int currentPotions = 0;
    private int currentBombs = 0;
    private WeaponData currentBombData;
    private PotionType currentPotionType;
    private WeaponData currentPotionData;
    private float potionEffectTimeRemaining;
    private PlayerHealth playerHealth;
    private PlayerController playerController;
    private PotionType activePotionType;
    private bool hasActivePotionEffect;
    
    private GameObject currentMeleeWeapon;
    private WeaponData currentMeleeWeaponData;
    
    private GameObject currentRangedWeapon;
    private WeaponData currentRangedWeaponData;
    private RangedWeapon currentRangedWeaponScript;
    
    private GameObject currentMiscItem;

    private List<Collider> collidersInRange = new List<Collider>();

    private void Start()
    {
        playerHealth = GetComponentInParent<PlayerHealth>();
        playerController = GetComponentInParent<PlayerController>();
        AutoFindUIReferences();

        if (pickupRangeSphere != null)
        {
            Rigidbody rangeRb = pickupRangeSphere.GetComponent<Rigidbody>();
            if (rangeRb == null)
            {
                rangeRb = pickupRangeSphere.AddComponent<Rigidbody>();
            }
            rangeRb.isKinematic = true;

            PickupTriggerDetector detector = pickupRangeSphere.GetComponent<PickupTriggerDetector>();
            if (detector == null)
            {
                detector = pickupRangeSphere.AddComponent<PickupTriggerDetector>();
            }
            detector.pickupManager = this;
        }

        UpdatePotionUI();
        UpdateBombUI();
    }

    private void Update()
    {
        if (!NetworkOwnership.CanControl(this))
        {
            return;
        }

        UpdatePotionEffect();
        UpdateReloadUI();
        
        if (Keyboard.current == null) return;

        bool actionPressed = false;
        if (playerType == PlayerType.Player1 && Keyboard.current.fKey.wasPressedThisFrame)
        {
            actionPressed = true;
        }
        else if (playerType == PlayerType.Player2 && Keyboard.current.semicolonKey.wasPressedThisFrame)
        {
            actionPressed = true;
        }

        if (actionPressed)
        {
            TryPickupItem();
        }

        bool bombPressed = playerType == PlayerType.Player1
            ? Keyboard.current.qKey.wasPressedThisFrame
            : Keyboard.current.uKey.wasPressedThisFrame;
        if (bombPressed)
        {
            UseBomb();
        }

        bool potionPressed = playerType == PlayerType.Player1
            ? Keyboard.current.rKey.wasPressedThisFrame
            : Keyboard.current.pKey.wasPressedThisFrame;
        if (potionPressed)
        {
            UsePotion();
        }
    }

    public void AutoFindUIReferences()
    {
        string fullName = playerType.ToString();
        string prefix = (playerType == PlayerType.Player1) ? "P1_" : "P2_";

        if (pickupUI == null)
        {
            pickupUI = GameObject.Find(fullName + "_PickupUI");
            if (pickupUI == null) pickupUI = GameObject.Find(fullName + "_UI");
            if (pickupUI == null) pickupUI = GameObject.Find(prefix + "PickupUI");
            if (pickupUI == null) pickupUI = GameObject.Find(prefix + "UI");
        }

        T FindUIComponent<T>(params string[] possibleNames) where T : Component
        {
            foreach (string name in possibleNames)
            {
                if (pickupUI != null)
                {
                    Transform targetTransform = pickupUI.transform.Find(name);
                    if (targetTransform != null)
                    {
                        T comp = targetTransform.GetComponent<T>();
                        if (comp != null) return comp;
                    }

                    T deepComp = SearchDeep<T>(pickupUI.transform, name);
                    if (deepComp != null) return deepComp;
                }

                GameObject go = GameObject.Find(prefix + name);
                if (go == null) go = GameObject.Find(fullName + "_" + name);
                if (go == null) go = GameObject.Find(name + "_" + fullName);
                if (go == null) go = GameObject.Find(name);

                if (go != null)
                {
                    T comp = go.GetComponent<T>();
                    if (comp != null) return comp;
                }
            }
            return null;
        }

        if (weaponOrMiscImage == null) weaponOrMiscImage = FindUIComponent<Image>("WeaponOrMiscImage", "WeaponImage", "MiscImage");
        if (weaponOrMiscText == null)  weaponOrMiscText  = FindUIComponent<TextMeshProUGUI>("WeaponOrMiscText", "WeaponText", "MiscText");
        if (potionImage == null)       potionImage       = FindUIComponent<Image>("PotionImage");
        if (potionText == null)        potionText        = FindUIComponent<TextMeshProUGUI>("PotionText");
        if (bombImage == null)         bombImage         = FindUIComponent<Image>("BombImage");
        if (bombText == null)          bombText          = FindUIComponent<TextMeshProUGUI>("BombText");
        
        if (activePotionEffectCountdown == null)
        {
            activePotionEffectCountdown = FindUIComponent<TextMeshProUGUI>("PotionEffectCountdown", "EffectCountdown");
        }

        if (activePotionEffectImage == null)
        {
            activePotionEffectImage = FindUIObject("ActivePotionEffect", "PotionEffectImage");
        }

        if (activePotionEffectCountdown == null && activePotionEffectImage != null)
        {
            activePotionEffectCountdown = activePotionEffectImage.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // Setup Reload UI Lookups
        if (reloadUIObject == null)
        {
            reloadUIObject = FindUIObject("ReloadUI", "Reload");
        }

        if (reloadImage == null)
        {
            if (reloadUIObject != null)
            {
                reloadImage = reloadUIObject.GetComponent<Image>();
                if (reloadImage == null) reloadImage = reloadUIObject.GetComponentInChildren<Image>(true);
            }

            if (reloadImage == null)
            {
                reloadImage = FindUIComponent<Image>("ReloadImage", "Reload");
            }
        }

        // Ensure the Reload image is set up correctly to display radial fills
        if (reloadImage != null)
        {
            reloadImage.type = Image.Type.Filled;
            reloadImage.fillMethod = Image.FillMethod.Radial360;
        }
    }

    public float DamageMultiplier => hasActivePotionEffect && activePotionType == PotionType.Strength ? 1.5f : 1f;

    public bool PurchaseWeapon(WeaponData data)
    {
        if (data == null || data.weaponPrefab == null)
        {
            return false;
        }

        return EquipWeapon(data);
    }

    private void UseBomb()
    {
        if (currentBombs <= 0 || currentBombData == null || currentBombData.weaponPrefab == null)
        {
            return;
        }

        currentBombs--;
        UpdateBombUI();

        Instantiate(currentBombData.weaponPrefab, transform.position, Quaternion.identity);

        if (currentBombs <= 0)
        {
            currentBombData = null;
        }
    }

    private void UsePotion()
    {
        if (currentPotions <= 0)
        {
            return;
        }

        currentPotions--;
        UpdatePotionUI();

        if (currentPotionType == PotionType.Health)
        {
            if (playerHealth != null)
            {
                playerHealth.HealPercentOfMax(0.3f);
            }
            return;
        }

        if (activePotionType == PotionType.Speed && playerController != null)
        {
            playerController.SetSpeedMultiplier(1f);
        }

        hasActivePotionEffect = true;
        activePotionType = currentPotionType;
        potionEffectTimeRemaining = 30f;

        if (activePotionType == PotionType.Speed && playerController != null)
        {
            playerController.SetSpeedMultiplier(1.25f);
        }

        UpdatePotionEffectUI();
    }

    private void UpdatePotionEffect()
    {
        if (!hasActivePotionEffect)
        {
            return;
        }

        potionEffectTimeRemaining -= Time.deltaTime;
        UpdatePotionEffectUI();

        if (potionEffectTimeRemaining > 0f)
        {
            return;
        }

        hasActivePotionEffect = false;
        if (activePotionType == PotionType.Speed && playerController != null)
        {
            playerController.SetSpeedMultiplier(1f);
        }

        UpdatePotionEffectUI();
    }

    private void UpdatePotionEffectUI()
    {
        if (activePotionEffectImage != null)
        {
            activePotionEffectImage.SetActive(hasActivePotionEffect);
        }

        if (activePotionEffectCountdown != null)
        {
            activePotionEffectCountdown.text = hasActivePotionEffect ? Mathf.CeilToInt(potionEffectTimeRemaining).ToString() : string.Empty;
        }
    }
    
    private void UpdateReloadUI()
    {
        if (currentRangedWeaponScript == null)
        {
            if (reloadUIObject != null) reloadUIObject.SetActive(false);
            else if (reloadImage != null) reloadImage.gameObject.SetActive(false);
            return;
        }

        if (reloadUIObject != null) reloadUIObject.SetActive(true);
        else if (reloadImage != null) reloadImage.gameObject.SetActive(true);

        if (reloadImage == null) return;

        float fireRate = currentRangedWeaponScript.CurrentFireRate;
        float nextFireTime = currentRangedWeaponScript.NextFireTime;

        if (fireRate <= 0f)
        {
            reloadImage.fillAmount = 1f;
            return;
        }

        float timeRemaining = nextFireTime - Time.time;
        if (timeRemaining <= 0f)
        {
            reloadImage.fillAmount = 1f;
        }
        else
        {
            float fillRatio = 1f - (timeRemaining / fireRate);
            reloadImage.fillAmount = Mathf.Clamp01(fillRatio);
        }
    }

    private void DropPotion()
    {
        if (currentPotionData == null || currentPotionData.weaponPrefab == null)
        {
            return;
        }

        GameObject droppedPotion = Instantiate(currentPotionData.weaponPrefab, dropPoint != null ? dropPoint.position : transform.position + transform.forward * 1.5f, Quaternion.identity);
        CollectibleItem item = droppedPotion.GetComponent<CollectibleItem>();
        if (item == null)
        {
            item = droppedPotion.AddComponent<CollectibleItem>();
        }

        item.itemType = "potion";
        item.potionType = currentPotionType;
        item.weaponData = currentPotionData;
        item.quantity = currentPotions;
        Collider collider = droppedPotion.GetComponentInChildren<Collider>();
        if (collider == null)
        {
            collider = droppedPotion.AddComponent<BoxCollider>();
        }
        collider.isTrigger = true;
    }

    private T SearchDeep<T>(Transform parent, string targetName) where T : Component
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                T comp = child.GetComponent<T>();
                if (comp != null) return comp;
            }
            T result = SearchDeep<T>(child, targetName);
            if (result != null) return result;
        }
        return null;
    }

    public void RegisterItem(Collider other)
    {
        if (!collidersInRange.Contains(other))
        {
            collidersInRange.Add(other);
        }
    }

    public void UnregisterItem(Collider other)
    {
        if (collidersInRange.Contains(other))
        {
            collidersInRange.Remove(other);
        }
    }

    private void TryPickupItem()
    {
        collidersInRange.RemoveAll(item => item == null);
        if (collidersInRange.Count == 0) return;

        foreach (var col in collidersInRange)
        {
            CollectibleItem item = col.GetComponentInParent<CollectibleItem>();
            if (item == null) continue;

            GameObject targetGameObject = item.gameObject;
            string tagType = item.itemType.ToLower();

            if (tagType == "potion")
            {
                if (currentPotions > 0 && currentPotionType != item.potionType)
                {
                    DropPotion();
                    currentPotions = 0;
                }

                if (currentPotions >= maxPotions) continue;
                currentPotionType = item.potionType;
                currentPotionData = item.weaponData;
                currentPotions += item.quantity;
                currentPotions = Mathf.Min(currentPotions, maxPotions);
                if (currentPotionData != null && potionImage != null)
                {
                    potionImage.sprite = currentPotionData.weaponImage;
                }
                UpdatePotionUI();
                Destroy(targetGameObject);
                break;
            }
            else if (tagType == "bombs")
            {
                if (currentBombs >= maxBombs) continue;
                currentBombData = item.weaponData;
                currentBombs += item.quantity;
                currentBombs = Mathf.Min(currentBombs, maxBombs);
                if (currentBombData != null && bombImage != null)
                {
                    bombImage.sprite = currentBombData.weaponImage;
                }
                UpdateBombUI();
                Destroy(targetGameObject);
                break;
            }
            else if (tagType == "melee")
            {
                EquipWeapon(item.weaponData);
                Destroy(targetGameObject);
                break;
            }
            else if (tagType == "ranged")
            {
                EquipWeapon(item.weaponData);
                Destroy(targetGameObject);
                break;
            }
            else if (tagType == "misc")
            {
                if (currentMiscItem != null)
                {
                    DropItem(currentMiscItem, "misc", null);
                }

                currentMiscItem = Instantiate(targetGameObject, handTransform);
                currentMiscItem.transform.localPosition = Vector3.zero;
                currentMiscItem.transform.localRotation = Quaternion.identity;

                if (currentMiscItem.TryGetComponent<Rigidbody>(out Rigidbody rbHeld))
                {
                    Destroy(rbHeld);
                }

                UpdateMiscUI(targetGameObject.name);
                Destroy(targetGameObject);
                break;
            }
        }
    }

    private bool EquipWeapon(WeaponData data)
    {
        if (data == null || data.weaponPrefab == null)
        {
            return false;
        }

        if (currentMeleeWeapon != null)
        {
            DropItem(currentMeleeWeapon, "melee", currentMeleeWeaponData);
            currentMeleeWeapon = null;
            currentMeleeWeaponData = null;
        }

        if (currentRangedWeapon != null)
        {
            DropItem(currentRangedWeapon, "ranged", currentRangedWeaponData);
            currentRangedWeapon = null;
            currentRangedWeaponData = null;
            currentRangedWeaponScript = null; // Clear the script reference
        }

        GameObject equippedWeapon = Instantiate(data.weaponPrefab, handTransform);
        equippedWeapon.transform.localPosition = Vector3.zero;
        equippedWeapon.transform.localRotation = Quaternion.identity;

        WeaponAttack meleeAttack = equippedWeapon.GetComponentInChildren<WeaponAttack>();
        RangedWeapon rangedWeapon = equippedWeapon.GetComponentInChildren<RangedWeapon>();
        if (meleeAttack != null)
        {
            meleeAttack.WeaponData = data;
            currentMeleeWeapon = equippedWeapon;
            currentMeleeWeaponData = data;
        }
        else if (rangedWeapon != null)
        {
            rangedWeapon.SetWeaponData(data);
            currentRangedWeapon = equippedWeapon;
            currentRangedWeaponData = data;
            currentRangedWeaponScript = rangedWeapon; // Track the script reference for UI
        }
        else
        {
            Destroy(equippedWeapon);
            return false;
        }

        UpdateWeaponUI(data);
        return true;
    }

    private void DropItem(GameObject itemObj, string itemType, WeaponData weaponData)
    {
        itemObj.transform.parent = null;
        if (dropPoint != null)
        {
            itemObj.transform.position = dropPoint.position;
        }
        else
        {
            itemObj.transform.position = transform.position + transform.forward * 1.5f;
        }
        
        BoxCollider boxCol = itemObj.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = itemObj.AddComponent<BoxCollider>();
        boxCol.isTrigger = false;
        boxCol.excludeLayers = LayerMask.GetMask("Player");
        
        Rigidbody rb = itemObj.GetComponent<Rigidbody>();
        if (rb == null) rb = itemObj.AddComponent<Rigidbody>();
        rb.isKinematic = false;

        CollectibleItem dropData = itemObj.GetComponent<CollectibleItem>();
        if (dropData == null)
        {
            dropData = itemObj.AddComponent<CollectibleItem>();
        }
        
        dropData.itemType = itemType;
        dropData.weaponData = weaponData;
        dropData.quantity = 1; 
    }

    private void UpdatePotionUI()
    {
        if (potionText != null) potionText.text = currentPotions.ToString();
        UpdateSlotOpacity(potionImage, currentPotions > 0);
    }

    private void UpdateBombUI()
    {
        if (bombText != null) bombText.text = currentBombs.ToString();
        UpdateSlotOpacity(bombImage, currentBombs > 0);
    }

    private static void UpdateSlotOpacity(Image image, bool hasItems)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = hasItems ? 1f : 0.4f;
        image.color = color;
        image.gameObject.SetActive(true);
    }

    private GameObject FindUIObject(params string[] possibleNames)
    {
        string fullName = playerType.ToString();
        string prefix = playerType == PlayerType.Player1 ? "P1_" : "P2_";

        foreach (string name in possibleNames)
        {
            if (pickupUI != null)
            {
                Transform found = SearchDeepObject(pickupUI.transform, name);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            GameObject result = GameObject.Find(prefix + name);
            if (result == null) result = GameObject.Find(fullName + "_" + name);
            if (result == null) result = GameObject.Find(name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static Transform SearchDeepObject(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform result = SearchDeepObject(child, targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void UpdateWeaponUI(WeaponData data)
    {
        if (weaponOrMiscText != null) weaponOrMiscText.text = data.weaponName;
        if (weaponOrMiscImage != null)
        {
            weaponOrMiscImage.sprite = data.weaponImage;
            weaponOrMiscImage.gameObject.SetActive(true);
        }
    }

    private void UpdateMiscUI(string miscName)
    {
        if (weaponOrMiscText != null) weaponOrMiscText.text = miscName;
        if (weaponOrMiscImage != null) weaponOrMiscImage.gameObject.SetActive(true);
    }
}