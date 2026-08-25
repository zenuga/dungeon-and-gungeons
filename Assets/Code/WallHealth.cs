using UnityEngine;

public class WallHealth : MonoBehaviour
{
    Depth depth;
    public int Health;

    public void UpdateWallHealth()
    {
        if (depth != null)
        {
            // Calculate Health using the integer value from your Depth script
            // Note: Replace 'currentDepth' with the actual variable/property name inside your Depth class (e.g., depth.value or depth.Level)
            Health = 10 * depth.depth;
        }
    }
}