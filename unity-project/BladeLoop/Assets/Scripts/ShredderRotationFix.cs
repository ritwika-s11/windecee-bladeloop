using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the Stage 2 shredder shafts turn the way a real twin-shaft shredder turns:
/// inward and downward, dragging material into the gap between them.
///
/// WHAT IS WRONG TODAY
/// -------------------
/// Four RotatingPart components drive the shafts, in two pairs:
///
///     S2_ShredderShaft_0   z = +0.4   axis +X   45 rpm
///     S2_ShredderShaft_1   z = -0.4   axis -X   45 rpm
///     Shaft_0              z = +0.2   axis +X   60 rpm
///     Shaft_1              z = -0.2   axis -X   60 rpm
///
/// Each pair counter-rotates, which is why this looks correct at a glance. It is not.
///
/// A positive rotation about +X carries a point at the top of the shaft (+Y) toward +Z.
/// So the shaft sitting at POSITIVE z turns its top further +Z - away from its partner -
/// and the shaft at NEGATIVE z turns its top further -Z, also away. Both shafts are
/// therefore lifting material up and throwing it OUT of the mouth.
///
/// A twin-shaft shredder does the opposite. The teeth bite at the top and pull downward
/// into the nip between the two shafts; that is the whole mechanism, and it is why the
/// shafts counter-rotate in the first place. Feeding a blade into the machine as it is
/// authored would spit the blade back out.
///
/// THE FIX
/// -------
/// Negate the axis on all four. They still counter-rotate - the pairing was never the
/// problem - but now the +z shaft's top travels toward -z and the -z shaft's top travels
/// toward +z, so both bite inward.
///
/// Deliberately derived from each shaft's own z position rather than hard-coded per name,
/// so it stays correct if the rig is ever mirrored or the shafts renamed.
///
/// NO SCENE EDIT. Spawns itself after the scene loads and finds the shafts by component,
/// the same pattern TourControls, KilnShellGrade and TourUISubmitGuard already use,
/// because .unity files cannot be merged and Stage 2 is a tour scene.
///
/// Conveyor rollers are left alone: S2_Conveyor_Roller_0 and _1 both use +X, which is
/// correct - a belt's rollers turn the same way as each other, not opposite.
/// </summary>
[DefaultExecutionOrder(50)]
public class ShredderRotationFix : MonoBehaviour
{
    const string SceneName = "Stage2_StoryMode";

    [Tooltip("Shafts nearer the rig centre than this in Z are treated as a single shaft " +
             "and left alone - only genuine counter-rotating pairs are corrected.")]
    public float minOffsetFromCentre = 0.05f;

    [Tooltip("Turn off to compare against the authored direction without recompiling.")]
    public bool apply = true;

    // ---------------------------------------------------------------- injection ----
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
        if (FindAnyObjectByType<ShredderRotationFix>() != null) return;
        new GameObject("~ShredderRotationFix").AddComponent<ShredderRotationFix>();
    }

    // -------------------------------------------------------------------- apply ----
    void Start()
    {
        if (!apply) return;

        int fixedCount = 0, alreadyRight = 0;

        foreach (var rp in FindObjectsByType<RotatingPart>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None))
        {
            if (rp == null) continue;

            // Only the shredder shafts. Rollers, fans and anything else keep their author's
            // intent - this component knows about one mechanism, not about rotation in general.
            string n = rp.gameObject.name;
            if (!n.StartsWith("S2_ShredderShaft_") && !n.StartsWith("Shaft_")) continue;

            // Which side of the nip is it on?
            //
            // World z, not localPosition.z: the two pairs sit under different parents
            // (ShredderRig and Shafts), and localPosition would be measured against
            // whichever transform each happens to hang off. Both rigs are built about
            // z = 0, so world z is the honest measure of which side of the gap a shaft is on.
            float side = rp.transform.position.z;
            if (Mathf.Abs(side) < minOffsetFromCentre) continue;   // single shaft, not a pair

            // Top of the shaft must travel TOWARD the nip:
            //   shaft at +z  ->  top must move -z  ->  rotation about -X
            //   shaft at -z  ->  top must move +z  ->  rotation about +X
            float wantX = side > 0f ? -1f : 1f;

            var axis = rp.axis;
            float mag = Mathf.Abs(axis.x) > 0.001f ? Mathf.Abs(axis.x) : 1f;
            var corrected = new Vector3(wantX * mag, axis.y, axis.z);

            if (Mathf.Sign(axis.x) == Mathf.Sign(wantX)) { alreadyRight++; continue; }

            rp.axis = corrected;
            fixedCount++;
        }

        Debug.Log($"[ShredderRotationFix] shafts corrected to bite inward: {fixedCount} flipped, " +
                  $"{alreadyRight} already correct.");
    }
}
