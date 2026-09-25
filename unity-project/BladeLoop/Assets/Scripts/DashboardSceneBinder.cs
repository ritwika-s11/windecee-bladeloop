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
            // NOT AN ERROR ANY MORE, AND NOT WORTH A WARNING.
            //
            // The dashboard was the stage-info panel from the first build. It was
            // superseded by OrderPanel, which shows the same thing against the live
            // order, and _DashboardBootstrap in MainMenu was deactivated then - so
            // _Dashboard is never loaded and this controller is never going to appear.
            //
            // The binder objects were left in Stage 1 and Stage 2. Harmless in
            // themselves, but this warning fired once per stage in every run, in the
            // shipped player's log, pointing at a feature that was retired on purpose.
            // A log a reader can trust is one where every line means something.
            //
            // Removing the binder objects would be the tidier fix; it is also two scene
            // edits for zero behaviour change, which is not a trade worth making.
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
