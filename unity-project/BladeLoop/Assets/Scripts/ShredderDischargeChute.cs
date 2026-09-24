using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives the Stage 2 shredder a visible path from its cutters to the conveyor that takes
/// the shred away.
///
/// THE PROBLEM, AS AN AUDIENCE SEES IT
/// -----------------------------------
/// Shred falls out of the rotors and lands on S2_ShredderChamber_Floor - a SOLID 2.4 x 2.0
/// plate with no opening, sitting in mid-air under the machine. It stops there. The belt
/// that carries material to the kiln starts 1.9 m away with nothing between them, so the
/// granules simply appear on the belt.
///
/// It reads as a table someone left under the shredder, and the obvious question - how
/// does the shred get from the machine onto the belt - has no answer on screen. A viewer
/// concludes the shredder is decorative.
///
/// Measured:
///
///     chamber floor (flat plate)   x 2.30 -> 4.70   y 2.33 -> 2.38
///     conveyor tail roller         x 6.60           y 1.95      (S2_FeedConveyor_ToKiln)
///
/// so the crossing is 1.9 m out and only 0.35 m down - a shallow run, which is exactly
/// what a discharge pan and chute should look like.
///
/// NOTE ON WHICH BELT
/// ------------------
/// Stage 2 has two. S2_Conveyor_Belt is a short horizontal belt at x -0.7..1.1, on the far
/// side of the shredder. The one that carries shred to the kiln is S2_FeedConveyor_ToKiln,
/// tail roller at (6.6, 1.95), climbing to the kiln funnel at (17, 6.6). An earlier version
/// of this component aimed at the first one and built a chute running away from the belt
/// that is actually in shot.
///
/// WHAT IT BUILDS
/// --------------
///   1. A sloped collection PAN in place of the flat plate, falling toward the conveyor,
///      with side walls. The flat plate's renderer is switched off behind it - that plate
///      is the "table".
///   2. A CHUTE from the pan's lip out over the conveyor tail roller, so the hand-off is
///      a thing you can see rather than infer.
///   3. A support leg, so neither is floating.
///
/// Everything is derived from the objects themselves, so it stays correct if the shredder
/// or the conveyor moves.
///
/// NO SCENE EDIT - built at runtime, Stage 2 is a tour scene and .unity files do not merge.
/// NO NEW MATERIALS - it samples the chamber floor's own material.
/// </summary>
[DefaultExecutionOrder(55)]
public class ShredderDischargeChute : MonoBehaviour
{
    const string SceneName = "Stage2_StoryMode";

    [Header("Pan")]
    [Tooltip("How high the back of the collection pan sits above the authored plate. " +
             "This is what gives it a visible fall toward the conveyor.")]
    public float panRise = 0.30f;
    [Tooltip("Hide the authored flat plate. It is the 'table' - a solid sheet with no " +
             "opening that shred lands on and stays on.")]
    public bool hideFlatPlate = true;

    [Tooltip("Length of the DISCHARGE OPENING at the low end of the pan, as a fraction of " +
             "the pan's run.\n\n" +
             "The pan stops short of its own lip and the chute passes underneath, so there " +
             "is a real gap you can see through - shred slides down the pan, drops through " +
             "the opening, and lands on the chute below.\n\n" +
             "Without this the pan is still a solid sheet: the first version just replaced " +
             "a flat table with a tilted one, and the material had no visible way through it.")]
    [Range(0.05f, 0.5f)] public float openingFraction = 0.22f;

    [Tooltip("How far the chute passes below the pan's discharge opening. This is the gap " +
             "the shred visibly falls through.")]
    public float openingDrop = 0.34f;

    [Header("Chute")]
    [Tooltip("Height of the pan's discharge lip above the authored plate.\n\n" +
             "This has to stay ABOVE where the chute meets the conveyor, or the chute " +
             "slopes uphill and the shred would have to climb to reach the belt. The first " +
             "attempt did exactly that: lip at y 2.23, chute end at 2.36.")]
    public float panLipOffset = 0.02f;
    [Tooltip("How far above the belt the chute lip ends, so it overhangs rather than " +
             "cutting into the belt surface.")]
    public float lipClearance = 0.10f;
    public float cheekHeight = 0.38f;
    public float plateThickness = 0.07f;

    [Header("Conveyor legs")]
    [Tooltip("Shorten the conveyor support legs that currently stand through the belt. " +
             "Three of the four do, and the highest reads as a dark cube among the granules.")]
    public bool trimConveyorLegs = true;
    [Tooltip("Gap left between the top of each leg and the underside of the belt.")]
    public float legClearance = 0.04f;

    [Tooltip("Off to see the scene as authored.")]
    public bool apply = true;

    Transform root;
    Material mat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnLoaded(Scene s, LoadSceneMode mode)
    {
        if (s.name != SceneName) return;
        if (FindAnyObjectByType<ShredderDischargeChute>() != null) return;
        new GameObject("~ShredderDischargeChute").AddComponent<ShredderDischargeChute>();
    }

    void Start()
    {
        if (!apply) return;

        // ShredderRig/Platform, NOT S2_ShredderChamber_Floor.
        //
        // This is the object a viewer calls "the table": a 2.8 x 2.8 dark plate at
        // (5, 4.15, 0), sitting directly under the rotors at y 5.3, so shred falls
        // straight onto it and stops. S2_ShredderChamber_Floor is a different part of the
        // CEE model at y 2.35, nearly two metres lower and to one side - four earlier
        // attempts rebuilt that one, which is why nothing appeared to change.
        var floor = FindUnder("ShredderRig", "Platform");
        var tail  = Find("RollerTail");
        if (floor == null || tail == null)
        {
            Debug.LogWarning("[ShredderDischargeChute] chamber floor or conveyor tail not found.");
            return;
        }

        var fr = floor.GetComponent<Renderer>();
        var br = tail.GetComponent<Renderer>();
        if (fr == null || br == null) return;

        mat = fr.sharedMaterial;
        root = new GameObject("~ShredderChute").transform;

        float side = Mathf.Sign(br.bounds.center.x - fr.bounds.center.x);   // conveyor is this way
        float z    = fr.bounds.center.z;
        float wPan = fr.bounds.size.z * 0.96f;
        float wOut = br.bounds.size.z * 0.95f;

        // ---- 1. sloped collection pan, replacing the flat plate ----
        // Heights are chained so the whole path falls continuously:
        //     pan back  >  pan lip  >  chute end  >  belt
        float lipY  = fr.bounds.max.y + panLipOffset;
        Vector3 panBack = new Vector3(fr.bounds.center.x - side * fr.bounds.extents.x, lipY + panRise, z);
        Vector3 panLip  = new Vector3(fr.bounds.center.x + side * fr.bounds.extents.x, lipY, z);
        // The pan STOPS SHORT of its lip. What is left is the discharge opening: a real
        // gap, with the chute running underneath it, so the shred has a visible way out
        // instead of resting on a sheet.
        Vector3 panEnd = Vector3.Lerp(panBack, panLip, 1f - openingFraction);
        BuildRamp(panBack, panEnd, wPan, "Pan", cheekHeight * 1.3f);

        // A collar round the opening, so the gap reads as an engineered mouth rather than
        // a missing piece of geometry.
        for (int i = -1; i <= 1; i += 2)
            MakeBox("Pan_OpeningCheek_" + (i > 0 ? "N" : "S"),
                    new Vector3(Mathf.Lerp(panEnd.x, panLip.x, 0.5f),
                                Mathf.Lerp(panEnd.y, panLip.y, 0.5f) + cheekHeight * 0.3f,
                                z + wPan * 0.5f * i),
                    Quaternion.identity,
                    new Vector3(Mathf.Abs(panLip.x - panEnd.x), cheekHeight * 0.9f, plateThickness));

        if (hideFlatPlate) fr.enabled = false;

        // ---- 2. chute, passing UNDER the opening and on to the tail roller ----
        Vector3 chuteStart = new Vector3(panEnd.x - side * 0.10f, panEnd.y - openingDrop, z);
        Vector3 chuteEnd   = new Vector3(br.bounds.center.x - side * br.bounds.extents.x * 1.1f,
                                         br.bounds.max.y + lipClearance, z);
        BuildRamp(chuteStart, chuteEnd, Mathf.Lerp(wPan, wOut, 0.55f), "Chute", cheekHeight);

        // ---- 3. a leg, so the chute is visibly carried ----
        Vector3 legAt = Vector3.Lerp(panLip, chuteEnd, 0.6f);
        MakeBox("Chute_Leg", new Vector3(legAt.x, legAt.y * 0.5f, z), Quaternion.identity,
                new Vector3(0.14f, legAt.y, 0.14f));

        AimFallout(panEnd, panLip, br);
        TrimConveyorLegs();

        Debug.Log($"[ShredderDischargeChute] pan {panBack:0.##}->{panEnd:0.##}, " +
                  $"chute {chuteStart:0.##}->{chuteEnd:0.##}, flat plate hidden: {hideFlatPlate}.");
    }

    /// <summary>
    /// Sends the shred stream through the opening and down onto the belt.
    ///
    /// Building the chute was only half the job. S2_PS_ShredderFallout emits at
    /// (5.17, 4.30) - the CENTRE of the platform - and falls straight down under gravity
    /// with collision disabled, so it drops through the pan, misses the opening at
    /// x 5.70-6.40, misses the chute, and rains into open air short of the belt. Unity
    /// particles do not collide with geometry unless told to, so the chute may as well not
    /// exist as far as the stream is concerned.
    ///
    /// Rather than switching on particle collision - which needs colliders, costs
    /// performance, and makes granules skitter unpredictably - the stream is moved to the
    /// opening and given exactly the horizontal drift that lands it on the belt. The
    /// ballistics are solved rather than guessed:
    ///
    ///     fall h  =  0.5 * g * t^2        ->  t = sqrt(2h / g)
    ///     drift   =  (beltX - mouthX) / t
    ///
    /// so it arrives on the belt however the geometry is later moved.
    /// </summary>
    void AimFallout(Vector3 panEnd, Vector3 panLip, Renderer beltTail)
    {
        var fallout = Find("S2_PS_ShredderFallout");
        if (fallout == null) return;
        var ps = fallout.GetComponent<ParticleSystem>();
        if (ps == null) return;

        float mouthX = Mathf.Lerp(panEnd.x, panLip.x, 0.5f);   // centre of the opening
        float mouthY = panEnd.y - 0.06f;                       // just under the pan lip
        fallout.position = new Vector3(mouthX, mouthY, panEnd.z);

        var main = ps.main;
        float g = Mathf.Abs(Physics.gravity.y) * Mathf.Max(main.gravityModifier.constant, 0.01f);
        float h = Mathf.Max(mouthY - beltTail.bounds.max.y, 0.05f);
        float t = Mathf.Sqrt(2f * h / g);

        main.startLifetime = t * 1.02f;                        // land, then stop
        main.startSpeed    = 0.05f;

        // Narrow the emitter to the opening, so the stream reads as coming through it.
        var sh = ps.shape;
        sh.scale = new Vector3(Mathf.Abs(panLip.x - panEnd.x) * 0.7f, 0.04f, sh.scale.z * 0.8f);

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve((beltTail.bounds.center.x - mouthX) / t);
        vol.y = new ParticleSystem.MinMaxCurve(0f);
        vol.z = new ParticleSystem.MinMaxCurve(0f);

        Debug.Log($"[ShredderDischargeChute] fallout re-aimed: mouth ({mouthX:0.##}, {mouthY:0.##}) " +
                  $"-> belt x {beltTail.bounds.center.x:0.##}, fall {h:0.##} m in {t:0.00} s, " +
                  $"drift {(beltTail.bounds.center.x - mouthX) / t:0.00} m/s.");
    }

    /// <summary>A sloped floor plate with a side wall down each edge.</summary>
    void BuildRamp(Vector3 from, Vector3 to, float width, string name, float wallH)
    {
        Vector3 span = to - from;
        float run = span.magnitude;
        if (run < 0.05f) return;

        Vector3 mid = (from + to) * 0.5f;
        float angle = Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);
        Vector3 up = rot * Vector3.up;

        MakeBox(name + "_Floor", mid, rot, new Vector3(run, plateThickness, width));

        for (int i = -1; i <= 1; i += 2)
            MakeBox(name + "_Wall_" + (i > 0 ? "N" : "S"),
                    mid + Vector3.forward * (width * 0.5f * i) + up * (wallH * 0.45f),
                    rot, new Vector3(run, wallH, plateThickness * 0.8f));
    }

    void MakeBox(string name, Vector3 pos, Quaternion rot, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, true);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = size;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);          // set dressing; nothing should collide with it
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    /// <summary>
    /// Shortens the conveyor's support legs so they stop under the belt instead of
    /// standing through it.
    ///
    /// Three of the four legs on S2_FeedConveyor_ToKiln finish ABOVE the belt surface:
    ///
    ///     Leg_1   -0.038 m from the belt plane   (clear)
    ///     Leg_2   +0.043 m                       (through)
    ///     Leg_3   +0.124 m                       (through)
    ///     Leg_4   +0.206 m                       (through)
    ///
    /// A 0.18 x 0.18 dark post standing proud of the belt reads, from the story camera, as
    /// a small dark cube sitting among the pale granules - which is what it was reported as.
    /// It is not a granule at all, and no amount of recolouring the belt load was ever going
    /// to remove it. Anirban spotted it.
    ///
    /// Worth recording WHY it was missed: the belt is tilted 35 degrees, so clearance has to
    /// be measured along the belt's own normal, not vertically. Comparing leg-top height
    /// against belt height says every leg is comfortably clear, and every leg is not.
    ///
    /// Each offending leg is shortened from the top, keeping its foot on the ground, so the
    /// structure still reads as supported.
    /// </summary>
    void TrimConveyorLegs()
    {
        if (!trimConveyorLegs) return;

        Transform belt = null;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.gameObject.name == "Belt" && t.parent != null && t.parent.name == "S2_FeedConveyor_ToKiln")
                belt = t;
        if (belt == null) return;

        Vector3 plane = belt.position, n = belt.up;
        float halfThick = belt.localScale.y * 0.5f;
        float wanted = -(halfThick + legClearance);          // target signed distance
        float upDot = Mathf.Abs(Vector3.Dot(Vector3.up, n));  // legs are vertical; belt is not
        if (upDot < 0.01f) return;

        int trimmed = 0;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.gameObject.name.StartsWith("Leg_")) continue;
            if (t.parent == null || t.parent.name != "S2_FeedConveyor_ToKiln") continue;
            var r = t.GetComponent<Renderer>();
            if (r == null) continue;

            Vector3 top = new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z);
            float d = Vector3.Dot(top - plane, n);
            if (d <= wanted) continue;                        // already clear

            // Convert the overshoot along the belt normal into a vertical shortening.
            float dropY = (d - wanted) / upDot;
            var s = t.localScale;
            float newY = Mathf.Max(s.y - dropY, 0.05f);
            float actual = s.y - newY;
            t.localScale = new Vector3(s.x, newY, s.z);
            t.position -= new Vector3(0f, actual * 0.5f, 0f);   // foot stays put
            trimmed++;
        }

        if (trimmed > 0)
            Debug.Log($"[ShredderDischargeChute] trimmed {trimmed} conveyor leg(s) back below the belt.");
    }

    static Transform Find(string name)
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.gameObject.name == name) return t;
        return null;
    }

    /// <summary>Find by name, but only inside a given rig - "Platform" is too generic to
    /// match on its own.</summary>
    static Transform FindUnder(string rootName, string name)
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.gameObject.name != name) continue;
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == rootName) return t;
        }
        return null;
    }
}
