using UnityEngine;

/// <summary>
/// Drives the 3D cyclone's reactive particles from the separation state.
/// Char sweeps up/out and glass falls through when separation is valid;
/// on failure the particles behave wrongly (contamination or blow-out).
/// Fleshed out after the cyclone model is placed in the scene.
/// </summary>
public class SeparationVisualizer : MonoBehaviour
{
    public ParticleSystem charParticles;   // light — should lift out
    public ParticleSystem glassParticles;  // heavy — should fall through

    ParticleSystem charPS;   // light — exits UP through vortex finder
    ParticleSystem glassPS;  // heavy — falls DOWN into hopper
    Transform cyclone;
    bool built;

    void Awake()
    {
        Build();
    }

    void Build()
    {
        if (built) return;
        cyclone = transform;

        // Pin emit points to actual cyclone parts so they always align.
        Transform vortex = FindPart("V2_Cyc_VortexFinder");
        Transform cone   = FindPart("V2_Cyc_Cone");
        Transform hopper = FindPart("V2_Cyc_CharHopper");

        Vector3 top, mid;
        if (vortex != null) {
            var r = vortex.GetComponent<Renderer>();
            top = r != null ? new Vector3(r.bounds.center.x, r.bounds.max.y + 0.2f, r.bounds.center.z)
                            : vortex.position;
        } else {
            var rends0 = GetComponentsInChildren<Renderer>();
            var bb = rends0[0].bounds; foreach (var rr in rends0) bb.Encapsulate(rr.bounds);
            top = new Vector3(bb.center.x, bb.max.y + 0.2f, bb.center.z);
        }
        if (cone != null) {
            var r = cone.GetComponent<Renderer>();
            mid = r != null ? r.bounds.center : cone.position;
        } else mid = top + Vector3.down * 1.5f;

        charPS  = MakePS("CharExit", top,  new Color(0.82f,0.83f,0.88f,0.65f), Vector3.up * 1.4f, 0.22f);
        glassPS = MakePS("GlassFall", mid, new Color(0.55f,0.72f,1f,0.8f),     Vector3.down * 1.6f, 0.3f);
        var gMain = glassPS.main; gMain.startLifetime = 1.0f;   // fades at the hopper, no overshoot
        built = true;
    }

    Transform FindPart(string name)
    {
        foreach (var t in GetComponentsInChildren<Transform>()) if (t.name == name) return t;
        return null;
    }

    ParticleSystem MakePS(string name, Vector3 worldPos, Color col, Vector3 vel, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.8f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = col;
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        var em = ps.emission; em.rateOverTime = 70f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10f; sh.radius = 0.15f;
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.4f; noise.scrollSpeed = 0.3f;
        var vel3 = ps.velocityOverLifetime; vel3.enabled = true;
        vel3.space = ParticleSystemSimulationSpace.World;
        vel3.x = new ParticleSystem.MinMaxCurve(vel.x);
        vel3.y = new ParticleSystem.MinMaxCurve(vel.y);
        vel3.z = new ParticleSystem.MinMaxCurve(vel.z);
        var col2 = ps.colorOverLifetime; col2.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]{ new GradientColorKey(col,0f), new GradientColorKey(col,1f) },
            new GradientAlphaKey[]{ new GradientAlphaKey(0f,0f), new GradientAlphaKey(col.a,0.2f), new GradientAlphaKey(0f,1f) });
        col2.color = grad;
        // soft, properly-transparent particle material
        var sh2 = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh2 == null) sh2 = Shader.Find("Particles/Standard Unlit");
        if (sh2 != null) {
            var m = new Material(sh2);
            m.SetFloat("_Surface", 1f);            // transparent
            m.SetFloat("_Blend", 0f);              // alpha blend
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/SoftParticle.png");
            if (tex != null) { m.SetTexture("_BaseMap", tex); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex); }
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
        }
        // softer render: stretch a touch, smoother
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sortMode = ParticleSystemSortMode.Distance;
        return ps;
    }

    public void SetSeparation(bool ok, float fluidizingVelocity)
    {
        if (!built) Build();
        if (charPS == null || glassPS == null) return;
        var charEm = charPS.emission;
        var glassEm = glassPS.emission;
        if (ok) {
            // clean: char exits UP, glass falls DOWN
            charEm.rateOverTime = 55f;
            glassEm.rateOverTime = 55f;
            SetColor(charPS, new Color(0.82f,0.83f,0.88f,0.65f));   // char pale
            SetColor(glassPS, new Color(0.55f,0.72f,1f,0.8f));     // glass blue
            SetUpward(charPS);      // char always up when ok
            SetDownward(glassPS);   // glass always down when ok
        } else if (fluidizingVelocity <= 0.0032f) {
            // too slow: char can't lift -> char falls with glass (contamination)
            charEm.rateOverTime = 60f;
            glassEm.rateOverTime = 55f;
            SetColor(charPS, new Color(0.75f,0.72f,0.7f,0.75f));
            SetDownward(charPS);    // char now falls
            SetDownward(glassPS);   // glass still falls
        } else {
            // too fast: glass blows out the top with char (yield lost)
            charEm.rateOverTime = 55f;
            glassEm.rateOverTime = 60f;
            SetColor(glassPS, new Color(0.55f,0.72f,1f,0.8f));
            SetUpward(charPS);      // char still up
            SetUpward(glassPS);     // glass now blows up too
        }
    }

    void SetColor(ParticleSystem ps, Color c){ var m = ps.main; m.startColor = c; }

    /// <summary>Scale the visible particles with the particle-size slider (8..26 µm -> visual size).</summary>
    public void SetParticleSize(float microns)
    {
        if (!built) Build();
        if (charPS == null || glassPS == null) return;
        float t = Mathf.InverseLerp(8f, 26f, microns);
        float size = Mathf.Lerp(0.12f, 0.4f, t);
        var cm = charPS.main;  cm.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        var gm = glassPS.main; gm.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.1f);
    }
    void SetDownward(ParticleSystem ps){ var v = ps.velocityOverLifetime; v.y = new ParticleSystem.MinMaxCurve(-1.5f); var n = ps.noise; n.strength = 0.1f; }
    void SetUpward(ParticleSystem ps){ var v = ps.velocityOverLifetime; v.y = new ParticleSystem.MinMaxCurve(1.8f); var n = ps.noise; n.strength = 0.2f; }
}
