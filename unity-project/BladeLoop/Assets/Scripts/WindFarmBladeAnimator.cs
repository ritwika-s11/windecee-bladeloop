using UnityEngine;
using System.Collections.Generic;

public class WindFarmBladeAnimator : MonoBehaviour
{
    public float rotationSpeed = 20f;
    public Vector3 rotationAxis = Vector3.right;
    public string bladeNameContains = "blades";

    private List<Transform> blades = new List<Transform>();

    void Start()
    {
        Transform[] allTransforms = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        Debug.Log($"Total transforms scanned: {allTransforms.Length}");

        string searchTerm = bladeNameContains.ToLower();
        foreach (var t in allTransforms)
        {
            if (t.name.ToLower().Contains(searchTerm))
            {
                blades.Add(t);
                Debug.Log($"  Matched: {t.name} (parent: {(t.parent != null ? t.parent.name : "none")})");
            }
        }
        Debug.Log($"WindFarmBladeAnimator: found {blades.Count} blade meshes to rotate");
    }

    void Update()
    {
        for (int i = 0; i < blades.Count; i++)
        {
            blades[i].Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}