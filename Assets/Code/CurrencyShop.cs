using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
public class CurrencyShop : MonoBehaviour
{
    [System.Serializable]
    private class ShopSlot
    {
        public Image itemImage;
        public TMP_Text costText;
        public TMP_Text statsText;
        public Button buyButton;
        [HideInInspector] public WeaponData item;
    }

    [Header("Shop Setup")]
    [SerializeField] private GameObject shopUI;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1f, 1.5f);
    [SerializeField] private List<WeaponData> itemPool = new List<WeaponData>();
    [SerializeField] private ShopSlot[] slots = new ShopSlot[3];

    [Header("Reroll Setup")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollCostText;
    [SerializeField] private int baseRerollCost = 40;
    [SerializeField] private int depthMultiplier = 1;

    private readonly List<PlayerCurrency> playersInRange = new List<PlayerCurrency>();
    private int rerollCount;
    private PlayerCurrency activeCurrency;

    private void Awake()
    {
        if (shopUI != null)
        {
            shopUI.SetActive(false);
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.AddListener(Reroll);
        }
    }

    private void Start()
    {
        ShopResetRegistry.Register(this);
        RefreshShop();
    }

    private void OnDestroy()
    {
        ShopResetRegistry.Unregister(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerCurrency currency = other.GetComponentInParent<PlayerCurrency>();
        if (currency == null || playersInRange.Contains(currency))
        {
            return;
        }

        playersInRange.Add(currency);
        activeCurrency = currency;
        if (shopUI != null)
        {
            shopUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerCurrency currency = other.GetComponentInParent<PlayerCurrency>();
        if (currency == null)
        {
            return;
        }

        playersInRange.Remove(currency);
        if (activeCurrency == currency)
        {
            activeCurrency = playersInRange.Count > 0 ? playersInRange[0] : null;
        }

        if (activeCurrency == null && shopUI != null)
        {
            shopUI.SetActive(false);
        }
    }

    public void BuySlot(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index].item == null || activeCurrency == null)
        {
            return;
        }

        ShopSlot slot = slots[index];
        WeaponData item = slot.item;

        if (item.weaponPrefab == null)
        {
            return;
        }

        // Try spending gold
        if (!activeCurrency.TrySpend(Mathf.Max(0, item.currencyAmount)))
        {
            return;
        }

        // Spawn weapon at shop position + spawnOffset
        Vector3 spawnPosition = transform.position + spawnOffset;
        GameObject spawnedWeapon = Instantiate(item.weaponPrefab, spawnPosition, Quaternion.identity);

        // Ensure the spawned weapon has its CollectibleItem data set
        CollectibleItem collectible = spawnedWeapon.GetComponent<CollectibleItem>();
        if (collectible == null)
        {
            collectible = spawnedWeapon.AddComponent<CollectibleItem>();
        }
        collectible.weaponData = item;

        // Turn off the buy button for this purchased slot
        if (slot.buyButton != null)
        {
            slot.buyButton.gameObject.SetActive(false);
        }
    }

    public void Reroll()
    {
        if (activeCurrency == null)
        {
            return;
        }

        int cost = GetRerollCost();
        if (activeCurrency.TrySpend(cost))
        {
            rerollCount++;
            RefreshShop();
        }
    }

    public void ResetRerollPrice()
    {
        rerollCount = 0;
        UpdateRerollText();
    }

    private void RefreshShop()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            ShopSlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            slot.item = itemPool.Count > 0 ? itemPool[Random.Range(0, itemPool.Count)] : null;
            if (slot.item != null)
            {
                if (slot.itemImage != null) slot.itemImage.sprite = slot.item.weaponImage;
                if (slot.costText != null) slot.costText.text = slot.item.currencyAmount.ToString();
                if (slot.statsText != null) slot.statsText.text = "Damage: " + slot.item.damage + "\nCooldown: " + slot.item.cooldown.ToString("0.##");
            }

            if (slot.buyButton != null)
            {
                int slotIndex = i;
                slot.buyButton.onClick.RemoveAllListeners();
                slot.buyButton.onClick.AddListener(() => BuySlot(slotIndex));
                
                // Re-enable and show buy button on reroll/refresh
                slot.buyButton.gameObject.SetActive(true);
                slot.buyButton.interactable = true;
            }
        }

        UpdateRerollText();
    }

    private int GetRerollCost()
    {
        Depth depth = FindFirstObjectByType<Depth>();
        int depthCost = depth != null ? Mathf.Max(1, depth.depth) : Mathf.Max(1, depthMultiplier);
        return baseRerollCost * depthCost * (int)Mathf.Pow(2, rerollCount);
    }

    private void UpdateRerollText()
    {
        if (rerollCostText != null)
        {
            rerollCostText.text = GetRerollCost().ToString();
        }
    }
}

public static class ShopResetRegistry
{
    private static readonly List<CurrencyShop> shops = new List<CurrencyShop>();

    public static void Register(CurrencyShop shop)
    {
        if (shop != null && !shops.Contains(shop)) shops.Add(shop);
    }

    public static void Unregister(CurrencyShop shop)
    {
        shops.Remove(shop);
    }

    public static void ResetAll()
    {
        foreach (CurrencyShop shop in shops)
        {
            if (shop != null) shop.ResetRerollPrice();
        }
    }
}