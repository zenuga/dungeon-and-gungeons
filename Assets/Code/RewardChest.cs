using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RewardChest : MonoBehaviour
{
    [Header("Rewards")]
    [SerializeField] private int minimumWeapons = 1;
    [SerializeField] private int maximumWeapons = 3;
    [SerializeField] private float weaponSpawnDistance = 1.25f;
    [SerializeField] private float weaponSpawnHeight = 0.25f;
    public Animation chestAnimator;

    private readonly List<GameObject> playersInRange = new List<GameObject>();
    private List<WeaponData> weaponTemplates = new List<WeaponData>();
    private bool isOpen;

    public void Configure(List<WeaponData> templates)
    {
        weaponTemplates = templates ?? new List<WeaponData>();
    }

    private void Awake()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        bool hasTrigger = false;

        foreach (Collider collider in colliders)
        {
            if (collider.isTrigger)
            {
                hasTrigger = true;
                break;
            }
        }

        if (!hasTrigger)
        {
            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.5f;
        }
    }

    private void Update()
    {
        if (isOpen || Keyboard.current == null)
        {
            return;
        }

        playersInRange.RemoveAll(player => player == null);

        foreach (GameObject player in playersInRange)
        {
            if (IsOpenKeyPressed(player))
            {
                Open();
                return;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject player = GetPlayerObject(other);
        if (player != null && !playersInRange.Contains(player))
        {
            playersInRange.Add(player);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject player = GetPlayerObject(other);
        if (player != null)
        {
            playersInRange.Remove(player);
        }
    }

    private bool IsOpenKeyPressed(GameObject player)
    {
        if (player.CompareTag("Player1"))
        {
            return Keyboard.current.fKey.wasPressedThisFrame;
        }

        if (player.CompareTag("Player2"))
        {
            return Keyboard.current.semicolonKey.wasPressedThisFrame;
        }

        return Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.semicolonKey.wasPressedThisFrame;
    }

    private void Open()
    {
        if (chestAnimator != null)
        {
            chestAnimator.Play("Open");
        }

        isOpen = true;

        int amount = Random.Range(minimumWeapons, maximumWeapons + 1);
        for (int i = 0; i < amount; i++)
        {
            SpawnReward(i, amount);
        }
    }

    private void SpawnReward(int index, int amount)
    {
        if (weaponTemplates.Count == 0)
        {
            Debug.LogWarning(name + " has no weapon templates assigned.", this);
            return;
        }

        WeaponData template = weaponTemplates[Random.Range(0, weaponTemplates.Count)];
        if (template == null || template.weaponPrefab == null)
        {
            return;
        }

        WeaponData rewardData = Instantiate(template);
        if (rewardData.itemType == RewardItemType.Weapon)
        {
            rewardData.damage = Random.Range(10, 51);

            float damagePercent = Mathf.InverseLerp(10f, 50f, rewardData.damage);
            float cooldownRoll = Mathf.Pow(Random.value, 1f / (1f + damagePercent * 3f));
            rewardData.cooldown = Mathf.Lerp(0.1f, 1.5f, cooldownRoll);
        }

        Vector3 forwardOffset = transform.forward * weaponSpawnDistance;
        Vector3 sideOffset = transform.right * ((index - (amount - 1) * 0.5f) * 0.65f);
        Vector3 spawnPosition = transform.position + forwardOffset + sideOffset + Vector3.up * weaponSpawnHeight;
        GameObject rewardObject = Instantiate(rewardData.weaponPrefab, spawnPosition, transform.rotation);

        CollectibleItem collectible = rewardObject.GetComponent<CollectibleItem>();
        if (collectible == null)
        {
            collectible = rewardObject.AddComponent<CollectibleItem>();
        }

        collectible.itemType = GetCollectibleType(rewardData);
        collectible.weaponData = rewardData;
        collectible.potionType = rewardData.potionType;
        collectible.quantity = 1;

        Collider rewardCollider = rewardObject.GetComponentInChildren<Collider>();
        if (rewardCollider == null)
        {
            rewardCollider = rewardObject.AddComponent<BoxCollider>();
        }
        rewardCollider.isTrigger = true;
    }

    private static string GetCollectibleType(WeaponData rewardData)
    {
        if (rewardData.itemType == RewardItemType.Bomb)
        {
            return "bombs";
        }

        if (rewardData.itemType == RewardItemType.Potion)
        {
            return "potion";
        }

        return rewardData.weaponPrefab.GetComponentInChildren<WeaponAttack>() != null ? "melee" : "ranged";
    }

    private static GameObject GetPlayerObject(Collider other)
    {
        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag("Player") || current.CompareTag("Player1") || current.CompareTag("Player2"))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }
}
