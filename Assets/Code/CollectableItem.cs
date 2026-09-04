using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    [Header("Item Type (bombs, melee, ranged, misc, potion)")]
    public string itemType; 

    public PotionType potionType;
    
    [Header("Stackable Settings (Bombs / Potions)")]
    public int quantity = 1;

    [Header("Weapon Settings (Melee / Ranged)")]
    public WeaponData weaponData;
}