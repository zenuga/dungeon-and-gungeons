using UnityEngine;

public enum RewardItemType
{
    Weapon,
    Bomb,
    Potion
}

public enum PotionType
{
    Strength,
    Health,
    Speed
}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Inventory/WeaponData")]
public class WeaponData : ScriptableObject
{
    public RewardItemType itemType = RewardItemType.Weapon;
    public string weaponName;
    public Sprite weaponImage;
    public GameObject weaponPrefab;
    public int damage;
    public float cooldown;
    public int currencyAmount = 40;
    public PotionType potionType;

    [Header("Hitscan Settings")]
    public bool isHitscan = false;
    public float hitscanRange = 100f;
    public LayerMask hitscanLayers = ~0; 
    public GameObject impactParticlePrefab;
    
    // CHANGED: Added shoot duration and reload time for hitscan clip mechanics
    public float hitscanShootDuration = 3f; 
    public float hitscanReloadTime = 2f; 
}