using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Segment 3 (Transport): two-shot beat. Shot A - truck rolling down the long road
/// between the fields; Shot B - the truck approaching the recycling-plant entrance.
/// Scene-relative clock so it works inside the chained full-plant tour.
/// </summary>
public class TransportBeats : MonoBehaviour
{
    public CinemachineCamera camA;
    public CinemachineCamera camB;
    public float switchTime = 9f;

    [Tooltip("Optional narration audio, played with a small lead-in delay.")]
    public AudioSource voiceover;
    public float voiceoverDelay = 0.5f;

    float t0;

    void Start()
    {
        t0 = Time.time;
        if (voiceover != null && voiceover.clip != null) voiceover.PlayDelayed(voiceoverDelay);
    }

    void Update()
    {
        bool second = Time.time - t0 >= switchTime;
        if (camA != null) camA.Priority = second ? 0 : 10;
        if (camB != null) camB.Priority = second ? 10 : 0;
    }
}
