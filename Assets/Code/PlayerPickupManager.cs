using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class PlayerPickupManager : MonoBehaviour
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

    [Header("Inventory Limits")]
    [SerializeField] private int maxPotions = 10;
    [SerializeField] private int maxBombs = 10;

    private int currentPotions = 0;
    private int currentBombs = 0;
    
    private GameObject currentMeleeWeapon;
    private WeaponData currentMeleeWeaponData; // Added to store data for dropping
    
    private GameObject currentRangedWeapon;
    private WeaponData currentRangedWeaponData; // Added to store data for dropping
    
    private GameObject currentMiscItem;

    private List<Collider> collidersInRange = new List<Collider>();

    private void Start()
    {
        AutoFindUIReferences();

        if (pickupRangeSphere != null)
        {
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
    }

    /// <summary>
    /// Searches the scene hierarchy for the Pickup UI container and individual UI elements by name.
    /// Supports names formatted with Player1/Player2 or P1_/P2_ prefixes.
    /// </summary>
    public void AutoFindUIReferences()
    {
        string fullName = playerType.ToString(); // "Player1" or "Player2"
        string prefix = (playerType == PlayerType.Player1) ? "P1_" : "P2_";

        // 1. Search for main pickupUI root if not assigned
        if (pickupUI == null)
        {
            pickupUI = GameObject.Find(fullName + "_PickupUI");
            if (pickupUI == null) pickupUI = GameObject.Find(fullName + "_UI");
            if (pickupUI == null) pickupUI = GameObject.Find(prefix + "PickupUI");
            if (pickupUI == null) pickupUI = GameObject.Find(prefix + "UI");
        }

        // 2. Helper method to search within pickupUI or scene-wide by name variants
        T FindUIComponent<T>(params string[] possibleNames) where T : Component
        {
            foreach (string name in possibleNames)
            {
                // Search inside pickupUI children first
                if (pickupUI != null)
                {
                    Transform targetTransform = pickupUI.transform.Find(name);
                    if (targetTransform != null)
                    {
                        T comp = targetTransform.GetComponent<T>();
                        if (comp != null) return comp;
                    }

                    // Deep recursive search inside pickupUI
                    T deepComp = SearchDeep<T>(pickupUI.transform, name);
                    if (deepComp != null) return deepComp;
                }

                // Search scene-wide with player prefix/fullname
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

        // 3. Auto-assign individual UI elements if missing
        if (weaponOrMiscImage == null) weaponOrMiscImage = FindUIComponent<Image>("WeaponOrMiscImage", "WeaponImage", "MiscImage");
        if (weaponOrMiscText == null)  weaponOrMiscText  = FindUIComponent<TextMeshProUGUI>("WeaponOrMiscText", "WeaponText", "MiscText");
        if (potionImage == null)       potionImage       = FindUIComponent<Image>("PotionImage");
        if (potionText == null)        potionText        = FindUIComponent<TextMeshProUGUI>("PotionText");
        if (bombImage == null)         bombImage         = FindUIComponent<Image>("BombImage");
        if (bombText == null)          bombText          = FindUIComponent<TextMeshProUGUI>("BombText");
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
            CollectibleItem item = col.GetComponent<CollectibleItem>();
            if (item == null) continue;

            string tagType = item.itemType.ToLower();

            if (tagType == "potion")
            {
                if (currentPotions >= maxPotions) continue;
                currentPotions += item.quantity;
                UpdatePotionUI();
                Destroy(col.gameObject);
                break;
            }
            else if (tagType == "bombs")
            {
                if (currentBombs >= maxBombs) continue;
                currentBombs += item.quantity;
                UpdateBombUI();
                Destroy(col.gameObject);
                break;
            }
            else if (tagType == "melee")
            {
                if (currentMeleeWeapon != null)
                {
                    DropItem(currentMeleeWeapon, "melee", currentMeleeWeaponData);
                }

                if (item.weaponData != null && item.weaponData.weaponPrefab != null)
                {
                    currentMeleeWeapon = Instantiate(item.weaponData.weaponPrefab, handTransform);
                    currentMeleeWeapon.transform.localPosition = Vector3.zero;
                    currentMeleeWeapon.transform.localRotation = Quaternion.identity;
                    
                    // Save the weapon data so we can re-assign it if we drop it later
                    currentMeleeWeaponData = item.weaponData;

                    UpdateWeaponUI(item.weaponData);
                }
                Destroy(col.gameObject);
                break;
            }
            else if (tagType == "ranged")
            {
                if (currentRangedWeapon != null)
                {
                    DropItem(currentRangedWeapon, "ranged", currentRangedWeaponData);
                }

                if (item.weaponData != null && item.weaponData.weaponPrefab != null)
                {
                    currentRangedWeapon = Instantiate(item.weaponData.weaponPrefab, handTransform);
                    currentRangedWeapon.transform.localPosition = Vector3.zero;
                    currentRangedWeapon.transform.localRotation = Quaternion.identity;

                    // Save the weapon data so we can re-assign it if we drop it later
                    currentRangedWeaponData = item.weaponData;

                    UpdateWeaponUI(item.weaponData);
                }
                Destroy(col.gameObject);
                break;
            }
            else if (tagType == "misc")
            {
                if (currentMiscItem != null)
                {
                    DropItem(currentMiscItem, "misc", null);
                }

                GameObject miscPrefab = col.gameObject;
                currentMiscItem = Instantiate(miscPrefab, handTransform);
                currentMiscItem.transform.localPosition = Vector3.zero;
                currentMiscItem.transform.localRotation = Quaternion.identity;

                if (currentMiscItem.TryGetComponent<Rigidbody>(out Rigidbody rbHeld))
                {
                    Destroy(rbHeld);
                }

                UpdateMiscUI(miscPrefab.name);
                Destroy(col.gameObject);
                break;
            }
        }
    }

    // Updated to accept itemType and WeaponData so we can rebuild the CollectibleItem component
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

        // Re-attach or retrieve the CollectibleItem script and assign its original data
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
        if (potionImage != null) potionImage.gameObject.SetActive(currentPotions > 0);
    }

    private void UpdateBombUI()
    {
        if (bombText != null) bombText.text = currentBombs.ToString();
        if (bombImage != null) bombImage.gameObject.SetActive(currentBombs > 0);
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