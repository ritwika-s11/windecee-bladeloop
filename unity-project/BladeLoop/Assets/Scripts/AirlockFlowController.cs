using UnityEngine;

/// <summary>
/// Drives the visible material flow through the vertical airlock tower, synced
/// to the AirlockDoorCycle 6-second clock:
///  - Room 1 pile grows while the upper door is closed (phases 1 and 3)
///  - Phase 2: Room 1 batch drops into Room 2 (burst particles + pile handoff)
///  - Phase 3: Room 2 batch drops through the duct + 50 deg chute into the kiln,
///             while the N2 purge flushes the chambers (green wisp)
/// All particle systems are pulsed via their emission module.
/// </summary>
public class AirlockFlowController : MonoBehaviour
{
    public AirlockDoorCycle cycle;

    [Header("Piles (scaled with fill level)")]
    public Transform room1Pile;
    public Transform room2Pile;
    public float room1PileMaxHeight = 0.35f;
    public float room2PileMaxHeight = 0.35f;
    public float pileRadius = 0.30f;

    [Header("Particle systems")]
    public ParticleSystem feedDribble;    // star valve -> Room 1, continuous metering
    public ParticleSystem dropR1toR2;     // phase 2 burst
    public ParticleSystem dropR2toChute;  // phase 3 burst (falls through duct)
    public ParticleSystem chuteToKiln;    // phase 3, sliding down the 50 deg chute
    public ParticleSystem n2Purge;        // phase 3 green purge wisp

    float r1Fill = 0.4f;
    float r2Fill = 0f;

    void Update()
    {
        if (cycle == null) return;
        float t = cycle.CycleT;

        // ---- fill levels ----
        if (!cycle.UpperOpen)
        {
            // star valve keeps metering material onto the closed upper door
            r1Fill = Mathf.MoveTowards(r1Fill, 1f, Time.deltaTime / 3.4f);
        }
        else
        {
            // upper drop: Room 1 empties fast, Room 2 receives
            r1Fill = Mathf.MoveTowards(r1Fill, 0f, Time.deltaTime * 3.0f);
            r2Fill = Mathf.MoveTowards(r2Fill, 1f, Time.deltaTime * 2.6f);
        }
        if (cycle.LowerOpen)
        {
            // lower discharge: Room 2 empties into the chute
            r2Fill = Mathf.MoveTowards(r2Fill, 0f, Time.deltaTime * 1.4f);
        }

        SetPile(room1Pile, r1Fill, room1PileMaxHeight);
        SetPile(room2Pile, r2Fill, room2PileMaxHeight);

        // ---- particle pulses ----
        Emit(feedDribble, !cycle.UpperOpen);              // metered dribble onto the pile
        Emit(dropR1toR2, cycle.UpperOpen);
        Emit(dropR2toChute, cycle.LowerOpen && r2Fill > 0.02f);
        Emit(chuteToKiln, t >= 4.25f);                     // slight lag: batch reaches chute
        Emit(n2Purge, cycle.N2PurgeActive);
    }

    void SetPile(Transform pile, float fill, float maxH)
    {
        if (pile == null) return;
        if (fill <= 0.01f) { if (pile.gameObject.activeSelf) pile.gameObject.SetActive(false); return; }
        if (!pile.gameObject.activeSelf) pile.gameObject.SetActive(true);
        float spread = Mathf.Lerp(0.45f, 1f, fill);
        pile.localScale = new Vector3(pileRadius * 2f * spread, maxH * fill, pileRadius * 2f * spread);
    }

    void Emit(ParticleSystem ps, bool on)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.enabled = on;
    }
}
