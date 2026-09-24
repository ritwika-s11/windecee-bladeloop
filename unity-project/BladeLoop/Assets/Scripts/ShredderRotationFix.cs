using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the Stage 2 shredder shafts spin about their own axis, biting inward.
///
/// WHY THIS WAS REWRITTEN
/// ----------------------
/// The first version of this component read RotatingPart.axis and reasoned about it as
/// if it were a world direction. It is not:
///
///     transform.Rotate(axis, rpm * 6f * Time.deltaTime, Space.Self);
///
/// Space.Self means axis is in the object's LOCAL frame. That only coincides with world
/// space when the transform is unrotated, and two of the four shafts are not:
///
///     Shaft_0 / Shaft_1               localEuler (0,   0,  0)  -> local X == world X
///     S2_ShredderShaft_0 / _1         localEuler (0, 270, 90)  -> local X == world Y
///
/// So the CEE shafts were spinning about a VERTICAL axis while their bodies lie along
/// world X - turning like a carousel rather than a shaft. Negating axis.x, which is all
/// the first version did, moved them from world +Y to world -Y and changed nothing that
/// mattered. The fix only ever reached the two shafts that happened to be unrotated.
///
/// Measured, at rest:
///
///     S2_ShredderShaft_0  bounds (2.00, 0.64, 0.64)  long axis world X  spins about +Y
///     S2_ShredderShaft_1  bounds (2.00, 0.59, 0.59)  long axis world X  spins about -Y
///     Shaft_0             bounds (2.20, 0.77, 0.77)  long axis world X  spins about +X
///     Shaft_1             bounds (2.20, 0.77, 0.77)  long axis world X  spins about -X
///
/// HOW THIS VERSION WORKS
/// ----------------------
/// It stops trusting the authored axis completely.
///
///   1. Measure the shaft's long axis from its own renderer bounds. A shaft rotates
///      about the line it is built along; that is a fact about the geometry, not about
///      what someone typed in the Inspector.
///   2. Decide the WORLD axis it needs: the long axis, signed so the top of the shaft
///      travels toward the nip - +z shaft's top moves -z, -z shaft's top moves +z.
///      (Rotation about +X carries +Y toward +Z, so the +z shaft needs -X.)
///   3. Convert that world axis back into the shaft's LOCAL frame with
///      InverseTransformDirection, because that is the frame Space.Self will use.
///
/// Step 3 is the one that was missing. It makes the result independent of how the shaft,
/// its parent, or the whole rig happens to be rotated - which is the only way this stays
/// correct if the CEE model is ever re-exported with different orientations.
///
/// THE CONVEYOR ROLLERS - NOW ON, AXIS ONLY
/// ----------------------------------------
/// Measuring turned up a second fault:
///
///     S2_Conveyor_Roller_0  bounds (0.24, 0.24, 0.70)  long axis world Z  spins about +X
///     S2_Conveyor_Roller_1  bounds (0.24, 0.24, 0.70)  long axis world Z  spins about +X
///
/// Both rollers lie along world Z but rotate about world X, so they tumble end over end
/// instead of rolling. The AXIS is clearly wrong and the geometry proves it. The SIGN is
/// not something the geometry can tell me.
///
/// This was left off pending Ritwika's confirmation, on the theory that the short belt
/// might not be in any shot. IT IS.
///
/// MEASURING THIS CORRECTLY TAKES TWO CORRECTIONS, and both bit me:
///
///   1. The vCams' AUTHORED rotations are placeholders. LookAt is null on all five and the
///      authored forward vectors point at the sky. PlantBootstrap wires each one to
///      LookTarget_<suffix> at runtime. Reading the transform gives nonsense - the control
///      test is that the SHREDDER must be in frame on the shredder shots, and it was not.
///   2. PlantBootstrap also SETS THE FOV by name: 60 deg for wide shots, 35 for close-ups.
///      The authored 40 is never used. Getting this wrong understates the close shots.
///
/// With both applied (positions confirmed unchanged - the CAM_ anchors match), at 1920x1080:
///
///     vCam_S2_01_PlantEntry      60 deg   both rollers in frame,  12 x  18 px
///     vCam_S2_02_TruckDump       35 deg   both rollers in frame,  46 x  42 px
///     vCam_S2_03_TeethSpin       35 deg   both rollers in frame,  56 x 107 px   <- ~10% of frame height
///     vCam_S2_04_OutputGranules  35 deg   behind camera
///     vCam_S2_05_FeedToKiln      35 deg   off frame
///
/// So it is on screen for three of five shots, and biggest on TeethSpin - the shot that is
/// ABOUT things rotating, where a roller tumbling end over end beside correctly spinning
/// shafts is exactly what an eye is drawn to.
///
/// WHY THE DIRECTION IS STILL ARBITRARY, AND WHY THAT IS FINE
/// ---------------------------------------------------------
/// I tried to settle the sign from the belt's own stripe motion, which would at least make
/// the roller agree with what the audience can see moving. It does not help: the belt
/// mesh's +U axis maps to world (-0.04, +1.00, +0.07) - essentially straight UP. The
/// stripes scroll ACROSS the belt, not along it, so the along-belt X component is 0.044,
/// which is numerically noise. ConveyorBeltScroller carries no directional information.
///
/// There is also no process flow to infer it from: this is the short decorative belt on
/// the far side of the shredder, not S2_FeedConveyor_ToKiln, and the granules sitting on
/// it are static props from the CEE FBX rather than anything our code moves.
///
/// That makes the sign a coin flip - but an unimportant one. EITHER sign produces a roller
/// that ROLLS about its own long axis. Only the axis error is visible as wrong; a roller
/// turning the "wrong" way just looks like a roller turning, especially at 49 x 93 px with
/// no material visibly moving along the belt to contradict it. So the axis fix is taken and
/// the direction is left as a documented arbitrary choice rather than a claim.
///
/// If Ritwika wants the other direction it is one toggle, and nothing else changes.
/// </summary>
[DefaultExecutionOrder(50)]
public class ShredderRotationFix : MonoBehaviour
{
    const string SceneName = "Stage2_StoryMode";

    [Tooltip("Shafts closer to the rig centreline than this are treated as single shafts, " +
             "not counter-rotating pairs, and are left alone.")]
    public float minOffsetFromCentre = 0.05f;

    [Header("Conveyor rollers")]
    [Tooltip("ON. The rollers lie along world Z but rotate about world X, so they tumble " +
             "end over end instead of rolling. Measured, the short belt IS on screen for " +
             "three of the five story shots - biggest on TeethSpin at 56 x 107 px, about a " +
             "tenth of frame height - so this is worth correcting. Fixing the axis is the " +
             "whole of the visible fault.")]
    public bool fixConveyorRollers = true;

    [Tooltip("ARBITRARY, and deliberately labelled as such. Which way this decorative belt " +
             "runs is not recoverable: its UV +U axis points straight up in world space, so " +
             "the stripe scroll says nothing about along-belt travel, and no material our " +
             "code moves travels on it. Either value makes the roller ROLL rather than " +
             "tumble, which is the part anyone can see. Flip it if Ritwika prefers.")]
    public bool beltTravelsTowardPositiveX = true;

    [Tooltip("Turn off to compare against the authored behaviour without recompiling.")]
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
        int shafts = 0, rollers = 0;

        foreach (var rp in FindObjectsByType<RotatingPart>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None))
        {
            if (rp == null) continue;
            string n = rp.gameObject.name;
            bool isShaft  = n.StartsWith("S2_ShredderShaft_") || n.StartsWith("Shaft_");
            bool isRoller = n.StartsWith("S2_Conveyor_Roller_");

            if (isShaft)  { if (CorrectShaft(rp))  shafts++;  }
            else if (isRoller && fixConveyorRollers) { if (CorrectRoller(rp)) rollers++; }
        }

        Debug.Log($"[ShredderRotationFix] {shafts} shaft(s) set to spin about their own axis " +
                  $"and bite inward" + (fixConveyorRollers ? $"; {rollers} roller(s) corrected." : "."));
    }

    /// <summary>The long axis of this object, in WORLD space, measured from its geometry.</summary>
    static bool LongAxis(Transform t, out Vector3 worldAxis)
    {
        worldAxis = Vector3.right;
        var rends = t.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return false;

        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        Vector3 s = b.size;

        worldAxis = (s.x >= s.y && s.x >= s.z) ? Vector3.right
                  : (s.y >= s.z)               ? Vector3.up
                                               : Vector3.forward;
        return true;
    }

    bool CorrectShaft(RotatingPart rp)
    {
        var t = rp.transform;
        if (!LongAxis(t, out Vector3 axisWorld)) return false;

        // Which side of the nip? Measured across whichever axis is NOT the shaft's length.
        // For a shaft lying along X the pair is separated in Z, and vice versa.
        float side = Mathf.Abs(axisWorld.x) > 0.5f ? t.position.z : t.position.x;
        if (Mathf.Abs(side) < minOffsetFromCentre) return false;

        // Rotation about +X carries a point at +Y toward +Z. So the shaft on the POSITIVE
        // side needs the NEGATIVE axis for its top to travel back toward the nip.
        Vector3 wantWorld = axisWorld * (side > 0f ? -1f : 1f);

        // The part that was missing: Space.Self rotates in the LOCAL frame.
        rp.axis = t.InverseTransformDirection(wantWorld).normalized;
        return true;
    }

    bool CorrectRoller(RotatingPart rp)
    {
        var t = rp.transform;
        if (!LongAxis(t, out Vector3 axisWorld)) return false;

        // Both rollers share a direction - they drive one belt, they do not oppose
        // each other. Rotation about +Z carries +Y toward -X, so belt travel toward
        // +X means rotation about -Z.
        Vector3 wantWorld = axisWorld * (beltTravelsTowardPositiveX ? -1f : 1f);
        rp.axis = t.InverseTransformDirection(wantWorld).normalized;
        return true;
    }
}
