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

    public void SetSeparation(bool ok, float fluidizingVelocity)
    {
        // Placeholder — particle wiring added once the cyclone model is in the scene.
        // Kept minimal so the controller compiles and can be tested UI-first.
    }
}
