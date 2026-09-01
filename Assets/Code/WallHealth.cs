using UnityEngine;

public class WallHealth : MonoBehaviour
{
    private Depth depth;
    public int Health = 10;

    private void Awake()
    {
        if (Health <= 0)
        {
            Health = 10;
        }
    }

    public void UpdateWallHealth()
    {
        if (depth != null)
        {
            Health = 10 * depth.depth;
        }
        else if (Health <= 0)
        {
            Health = 10;
        }
    }

    public void TakeDamage(int amount)
    {
        Health -= amount;
        if (Health <= 0)
        {
            Destroy(gameObject);
        }
    }
}