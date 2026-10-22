using System.Collections.Generic;
using UnityEngine;

/// <summary>Corrects the imported spider model's sideways local axis without rotating its collider or agent.</summary>
public sealed class SpiderVisualOrientationFix : MonoBehaviour
{
    [SerializeField] private Vector3 modelRotationOffset = new Vector3(90f, 0f, 0f);

    private void Start()
    {
        Transform visualPivot = new GameObject("SpiderVisualOrientation").transform;
        visualPivot.SetParent(transform, false);
        visualPivot.localPosition = Vector3.zero;
        visualPivot.localRotation = Quaternion.Euler(modelRotationOffset);

        List<Transform> visualBranches = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
                visualBranches.Add(child);
        }

        foreach (Transform branch in visualBranches)
            branch.SetParent(visualPivot, true);

        if (visualBranches.Count == 0)
            Destroy(visualPivot.gameObject);
    }
}
