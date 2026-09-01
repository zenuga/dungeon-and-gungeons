using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAttack : MonoBehaviour
{
    [Header("Weapon Data")]
    [SerializeField] private WeaponData weaponData;

    [Header("Swing Settings")]
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float attackRadius = 1.2f;
    [SerializeField] private float swingAngle = 120f;
    [SerializeField] private float attackCooldown = 0.5f;

    [Header("Target Tags")]
    [SerializeField] private List<string> validTags = new List<string> { "enemy", "wall", "boss", "Enemy", "Wall", "Boss" };

    private float _nextAttackTime = 0f;
    private HashSet<Collider> _hitThisSwing = new HashSet<Collider>();

    public WeaponData WeaponData
    {
        get => weaponData;
        set => weaponData = value;
    }

    private void Update()
    {
        if (Time.time < _nextAttackTime || Keyboard.current == null) return;

        bool ePressed = Keyboard.current.eKey.wasPressedThisFrame;
        bool oPressed = Keyboard.current.oKey.wasPressedThisFrame;

        if (!ePressed && !oPressed) return;

        Transform ownerTransform = GetOwnerTransform();
        if (ownerTransform == null) return;

        bool isValidInput = false;
        if (ePressed && ownerTransform.CompareTag("Player1"))
        {
            isValidInput = true;
        }
        else if (oPressed && ownerTransform.CompareTag("Player2"))
        {
            isValidInput = true;
        }

        if (isValidInput)
        {
            ExecuteAttack(transform);
        }
    }

    private Transform GetOwnerTransform()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.CompareTag("Player1") || current.CompareTag("Player2"))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private void ExecuteAttack(Transform attackTransform)
    {
        Debug.Log($"Executing attack with weapon: {weaponData?.name ?? "Unknown Weapon"}");
        _hitThisSwing.Clear();
        int damageAmount = GetDamageFromWeapon();
        float cooldown = GetCooldownFromWeapon();
        _nextAttackTime = Time.time + cooldown;

        Vector3 attackOrigin = attackTransform.position + attackTransform.forward * (attackRange * 0.5f);
        Vector3 attackDirection = attackTransform.forward;
        Collider[] hits = Physics.OverlapSphere(attackOrigin, attackRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || col.transform == attackTransform || col.gameObject == attackTransform.gameObject || _hitThisSwing.Contains(col)) continue;

            if (!HasValidTargetTag(col.gameObject)) continue;

            Vector3 directionToTarget = (col.bounds.center - attackTransform.position).normalized;
            float angleToTarget = Vector3.Angle(attackDirection, directionToTarget);

            if (angleToTarget > swingAngle * 0.5f) continue;

            _hitThisSwing.Add(col);
            ApplyDamage(col.gameObject, damageAmount);
        }
    }

    private int GetDamageFromWeapon()
    {
        if (weaponData != null && weaponData.damage > 0)
        {
            return weaponData.damage;
        }

        return 10;
    }

    private float GetCooldownFromWeapon()
    {
        if (weaponData != null && weaponData.cooldown > 0f)
        {
            return weaponData.cooldown;
        }

        return attackCooldown;
    }

    private bool HasValidTargetTag(GameObject obj)
    {
        if (obj == null) return false;

        string targetTag = obj.tag;
        foreach (string validTag in validTags)
        {
            if (string.Equals(targetTag, validTag, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyDamage(GameObject target, int damageAmount)
    {
        target.SendMessage("TakeDamage", damageAmount, SendMessageOptions.DontRequireReceiver);
        Debug.Log($"Hit {target.name} on tag '{target.tag}' for {damageAmount} damage!");
    }
}