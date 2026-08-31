using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Inventory/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public Sprite weaponImage;
    public GameObject weaponPrefab;
    public int damage;
    public float cooldown;
}