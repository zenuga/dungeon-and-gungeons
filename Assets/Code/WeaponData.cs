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
    public PotionType potionType;
}