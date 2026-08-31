using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAttack : MonoBehaviour
{
    [Header("Damage & Depth")]
    [Tooltip("Reference depth value or connect to your depth system script.")]
    [SerializeField] private int depth = 1;

    [Header("Cooldown")]
    [SerializeField] private float attackCooldown = 1.0f;
    private float _nextAttackTime = 0f;

    [Header("Target Tags")]
    [SerializeField] private List<string> validTags = new List<string> { "enemy", "wall", "boss", "Enemy", "Wall", "Boss" };

    // Tracks targets currently inside the trigger zone
    private List<Collider> _targetsInTrigger = new List<Collider>();

    private void Update()
    {
        // Check cooldown
        if (Time.time < _nextAttackTime) return;

        if (Keyboard.current == null) return;

        // Read input presses
        bool fPressed = Keyboard.current.fKey.wasPressedThisFrame;
        bool semicolonPressed = Keyboard.current.semicolonKey.wasPressedThisFrame;

        if (!fPressed && !semicolonPressed) return;

        // Check parent tag requirements
        Transform parentTransform = transform.parent;
        if (parentTransform == null) return;

        bool isValidInput = false;

        if (fPressed && parentTransform.CompareTag("Player1"))
        {
            isValidInput = true;
        }
        else if (semicolonPressed && parentTransform.CompareTag("Player2"))
        {
            isValidInput = true;
        }

        // Execute attack if validation passed
        if (isValidInput)
        {
            ExecuteAttack();
        }
    }

    private void ExecuteAttack()
    {
        // Set cooldown timer
        _nextAttackTime = Time.time + attackCooldown;

        // Random damage between 10 and 30 (inclusive) multiplied by depth
        int calculatedDamage = Random.Range(10, 31) * GetDepthValue();

        // Deal damage to any hit targets inside the trigger
        for (int i = _targetsInTrigger.Count - 1; i >= 0; i--)
        {
            Collider col = _targetsInTrigger[i];

            if (col == null)
            {
                _targetsInTrigger.RemoveAt(i);
                continue;
            }

            if (HasValidTargetTag(col.gameObject))
            {
                ApplyDamage(col.gameObject, calculatedDamage);
            }
        }
    }

    private int GetDepthValue()
    {
        // Replace this with your actual Depth Manager reference if needed
        // e.g., return DepthManager.Instance.currentDepth;
        return Mathf.Max(1, depth);
    }

    private bool HasValidTargetTag(GameObject obj)
    {
        foreach (string targetTag in validTags)
        {
            if (obj.CompareTag(targetTag)) return true;
        }
        return false;
    }

    private void ApplyDamage(GameObject target, int damageAmount)
    {
        // Calls TakeDamage(int) on any script attached to the target object
        target.SendMessage("TakeDamage", damageAmount, SendMessageOptions.DontRequireReceiver);
        Debug.Log($"Hit {target.name} on tag '{target.tag}' for {damageAmount} damage!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_targetsInTrigger.Contains(other))
        {
            _targetsInTrigger.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_targetsInTrigger.Contains(other))
        {
            _targetsInTrigger.Remove(other);
        }
    }
}