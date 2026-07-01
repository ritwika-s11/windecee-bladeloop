using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Sits in each stage scene. On Start, searches the scene for:
///   - the hot-zone material (by name match on _kiln/hot/refractory)
///   - any "BurnerFlame" particle systems
///   - the local StoryModeController
/// and pushes them into the DashboardController.Instance once it's available.
/// </summary>
public class DashboardSceneBinder : MonoBehaviour
{
    [Tooltip("Display label shown at the top of the dashboard (e.g. 'Stage 3 – Pyrolysis')")]
    public string stageLabel = "Stage";

    [Tooltip("Optional explicit material override. Leave empty to auto-find.")]
    public Material explicitKilnHotMaterial;

    [Tooltip("Optional explicit flame ParticleSystem list. Leave empty to auto-find.")]
    public ParticleSystem[] explicitFlameParticles;

    IEnumerator Start()
    {
        // Wait until additive dashboard scene has loaded its controller
        float waited = 0f;
        while (DashboardController.Instance == null && waited < 5f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        if (DashboardController.Instance == null)
        {
            Debug.LogWarning($"[DashboardSceneBinder] No DashboardController found after {waited:F1}s — dashboard scene not loaded?");
            yield break;
        }

        Material kilnMat = explicitKilnHotMaterial != null ? explicitKilnHotMaterial : FindKilnHotMaterial();
        ParticleSystem[] flames = (explicitFlameParticles != null && explicitFlameParticles.Length > 0)
            ? explicitFlameParticles
            : FindFlameParticles();
        StoryModeController story = FindAnyObjectByType<StoryModeController>();

        DashboardController.Instance.BindStage(stageLabel, kilnMat, flames, story);
    }

    Material FindKilnHotMaterial()
    {
        // Look for any renderer whose material name contains "Hot" or "Refractory"
        var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            string n = r.sharedMaterial.name.ToLowerInvariant();
            if (n.Contains("kilnhot") || n.Contains("refractoryhot") || n.Contains("hotzone") || n.Contains("hotinnersurface"))
                return r.sharedMaterial;
        }
        return null;
    }

    ParticleSystem[] FindFlameParticles()
    {
        var all = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<ParticleSystem> flames = new List<ParticleSystem>();
        foreach (var ps in all)
        {
            if (ps == null) continue;
            string n = ps.name.ToLowerInvariant();
            if (n.Contains("burnerflame") || n.Contains("flame"))
                flames.Add(ps);
        }
        return flames.ToArray();
    }
}
