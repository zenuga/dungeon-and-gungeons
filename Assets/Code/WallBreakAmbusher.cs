using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class WallBreakAmbusher : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float contactDamageCooldown = 1.5f;

    private PlayerHealth target;
    private NavMeshAgent agent;
    private readonly Dictionary<PlayerHealth, float> nextDamageTime = new Dictionary<PlayerHealth, float>();

    public void Initialize(PlayerHealth player)
    {
        target = player;
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (target == null || !target.IsAlive) return;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.SetDestination(target.transform.position);
        }
        else
        {
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.position += direction.normalized * 4f * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(direction.normalized);
            }
        }
    }

    private void OnTriggerEnter(Collider other) => TryDamagePlayer(other);
    private void OnTriggerStay(Collider other) => TryDamagePlayer(other);

    private void TryDamagePlayer(Collider other)
    {
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null || player != target || !player.IsAlive) return;

        if (nextDamageTime.TryGetValue(player, out float nextTime) && Time.time < nextTime) return;
        nextDamageTime[player] = Time.time + contactDamageCooldown;
        player.TakeDamage(Mathf.Max(1, Mathf.CeilToInt(player.MaxHealthValue * 0.5f)));
    }
}
