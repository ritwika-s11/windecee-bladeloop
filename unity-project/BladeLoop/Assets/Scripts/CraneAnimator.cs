using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tower-crane loading cycle using pre-placed static blades:
///   - pileSegments: 4 blades sitting statically on the ground, pre-positioned as
///     children of the BladePile GameObject with rotation baked in Editor.
///   - bedSegments: 4 blades sitting statically on the truck bed, pre-positioned as
///     children of the StyledTruck GameObject with rotation baked in Editor.
///
/// The animator never MOVES a blade. It only toggles their SetActive flag. On each
/// crane cycle, one pile blade hides and one bed blade appears — the illusion of a
/// pick-up-and-load. Because nothing is ever parented dynamically or rotated by
/// script, no rotation drift is possible.
///
/// The crane rig (slew, trolley, hookRig) still visually cycles through pickup and
/// drop motions for the animation, but the blades themselves are decoupled.
/// </summary>
public class CraneAnimator : MonoBehaviour
{
    [Header("Crane rig")]
    public Transform slew;
    public Transform trolley;
    public Transform hookRig;

    [Header("World positions for rig motion")]
    public Vector3 pickupWorldPos = new Vector3(-6f, 0.35f, 22f);
    public Vector3 dropWorldPos = new Vector3(-4f, 1.35f, 14f);

    [Header("Pile blades (children of BladePile, active initially)")]
    public List<Transform> pileSegments = new List<Transform>();

    [Header("Bed blades (children of StyledTruck, INACTIVE initially)")]
    public List<Transform> bedSegments = new List<Transform>();

    [Header("Timing")]
    public float cycleDuration = 6f;
    public float startDelay = 0f;

    float sceneT0; // scene-relative clock (Time.time keeps running across chained scene loads)

    float pickupYaw;
    float dropYaw;
    float pickupTrolleyX;
    float dropTrolleyX;

    const float HOOK_HIGH_Y = -0.4f;
    const float HOOK_LOW_PICKUP_Y = -8.5f;
    const float HOOK_LOW_DROP_Y = -6.5f;

    int loadedCount = 0;
    // Per-cycle guards so we hide/show blades only ONCE per phase transition
    bool grabbedThisCycle = false;
    bool releasedThisCycle = false;
    int lastCycleN = -1;

    void Start()
    {
        if (slew == null || trolley == null || hookRig == null) return;

        sceneT0 = Time.time;
        Vector3 slewWorld = slew.position;
        Vector2 pickupOff = new Vector2(pickupWorldPos.x - slewWorld.x, pickupWorldPos.z - slewWorld.z);
        Vector2 dropOff = new Vector2(dropWorldPos.x - slewWorld.x, dropWorldPos.z - slewWorld.z);

        pickupYaw = -Mathf.Atan2(pickupOff.y, pickupOff.x) * Mathf.Rad2Deg;
        dropYaw = -Mathf.Atan2(dropOff.y, dropOff.x) * Mathf.Rad2Deg;
        pickupTrolleyX = pickupOff.magnitude;
        dropTrolleyX = dropOff.magnitude;

        slew.localRotation = Quaternion.Euler(0f, pickupYaw, 0f);
        var tp = trolley.localPosition; tp.x = pickupTrolleyX; trolley.localPosition = tp;
        var hp = hookRig.localPosition; hp.y = HOOK_HIGH_Y; hookRig.localPosition = hp;

        // All pile blades ACTIVE (visible on ground), all bed blades INACTIVE (empty truck).
        // Also enforce lying-flat local rotation on every blade in case a prior Play
        // session accidentally saved a wrong rotation to the scene.
        Quaternion flat = Quaternion.Euler(0f, 0f, 90f);
        for (int i = 0; i < pileSegments.Count; i++) {
            if (pileSegments[i] != null) {
                pileSegments[i].localRotation = flat;
                pileSegments[i].gameObject.SetActive(true);
            }
        }
        for (int i = 0; i < bedSegments.Count; i++) {
            if (bedSegments[i] != null) {
                bedSegments[i].localRotation = flat;
                bedSegments[i].gameObject.SetActive(false);
            }
        }
    }

    void Update()
    {
        if (slew == null || trolley == null || hookRig == null) return;

        int maxLoads = Mathf.Min(pileSegments.Count, bedSegments.Count);

        // All loaded — hold idle at pickup position
        if (loadedCount >= maxLoads) {
            slew.localRotation = Quaternion.Euler(0f, pickupYaw, 0f);
            var tp0 = trolley.localPosition; tp0.x = pickupTrolleyX; trolley.localPosition = tp0;
            var hp0 = hookRig.localPosition; hp0.y = HOOK_HIGH_Y; hookRig.localPosition = hp0;
            UpdateCable();
            return;
        }

        float t2 = Time.time - sceneT0 - startDelay;
        if (t2 < 0f) {
            UpdateCable();
            return;
        }
        float cyclePos = (t2 / cycleDuration) % 1f;
        int cycleN = Mathf.FloorToInt(t2 / cycleDuration);

        // Reset per-cycle flags when a new cycle begins
        if (cycleN != lastCycleN) {
            grabbedThisCycle = false;
            releasedThisCycle = false;
            lastCycleN = cycleN;
        }

        float slewYaw = pickupYaw;
        float trolleyX = pickupTrolleyX;
        float hookY = HOOK_HIGH_Y;

        System.Func<float, float, float, float> ease = (a, b, u) => Mathf.Lerp(a, b, u * u * (3f - 2f * u));

        if (cyclePos < 0.125f) {
            float u = cyclePos / 0.125f;
            hookY = ease(HOOK_HIGH_Y, HOOK_LOW_PICKUP_Y, u);
        } else if (cyclePos < 0.20f) {
            // GRAB — hide the current pile blade
            hookY = HOOK_LOW_PICKUP_Y;
            if (!grabbedThisCycle && loadedCount < pileSegments.Count) {
                if (pileSegments[loadedCount] != null) {
                    pileSegments[loadedCount].gameObject.SetActive(false);
                }
                grabbedThisCycle = true;
            }
        } else if (cyclePos < 0.35f) {
            float u = (cyclePos - 0.20f) / 0.15f;
            hookY = ease(HOOK_LOW_PICKUP_Y, HOOK_HIGH_Y, u);
        } else if (cyclePos < 0.55f) {
            float u = (cyclePos - 0.35f) / 0.20f;
            slewYaw = ease(pickupYaw, dropYaw, u);
            trolleyX = ease(pickupTrolleyX, dropTrolleyX, u);
            hookY = HOOK_HIGH_Y;
        } else if (cyclePos < 0.70f) {
            float u = (cyclePos - 0.55f) / 0.15f;
            slewYaw = dropYaw; trolleyX = dropTrolleyX;
            hookY = ease(HOOK_HIGH_Y, HOOK_LOW_DROP_Y, u);
        } else if (cyclePos < 0.75f) {
            // RELEASE — show the bed blade at the matching slot
            slewYaw = dropYaw; trolleyX = dropTrolleyX; hookY = HOOK_LOW_DROP_Y;
            if (!releasedThisCycle && loadedCount < bedSegments.Count) {
                if (bedSegments[loadedCount] != null) {
                    bedSegments[loadedCount].gameObject.SetActive(true);
                }
                releasedThisCycle = true;
                loadedCount++;
            }
        } else if (cyclePos < 0.85f) {
            float u = (cyclePos - 0.75f) / 0.10f;
            slewYaw = dropYaw; trolleyX = dropTrolleyX;
            hookY = ease(HOOK_LOW_DROP_Y, HOOK_HIGH_Y, u);
        } else {
            float u = (cyclePos - 0.85f) / 0.15f;
            slewYaw = ease(dropYaw, pickupYaw, u);
            trolleyX = ease(dropTrolleyX, pickupTrolleyX, u);
            hookY = HOOK_HIGH_Y;
        }

        slew.localRotation = Quaternion.Euler(0f, slewYaw, 0f);
        var tp = trolley.localPosition; tp.x = trolleyX; trolley.localPosition = tp;
        var hp = hookRig.localPosition; hp.y = hookY; hookRig.localPosition = hp;

        UpdateCable();
    }

    void UpdateCable()
    {
        var cable = hookRig.Find("Cable");
        if (cable == null) return;
        float y = hookRig.localPosition.y;
        var s = cable.localScale; s.y = Mathf.Abs(y) * 0.5f; cable.localScale = s;
        var cp = cable.localPosition; cp.y = -y * 0.5f; cable.localPosition = cp;
    }
}
