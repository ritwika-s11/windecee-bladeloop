using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Moves a vCam between two anchors while the scene's PlayableDirector runs
/// from startTime to endTime (smoothstepped). Driven by director.time — NOT
/// Time.time — so it is immune to chained-scene clock drift (TourSceneSequencer)
/// and works correctly when the timeline is scrubbed.
/// Runs in Update(); CinemachineBrain samples in LateUpdate, so no race.
/// </summary>
public class CameraPullback : MonoBehaviour
{
    public PlayableDirector director;
    public Transform startAnchor;
    public Transform endAnchor;
    public float startTime = 66f;
    public float endTime = 79.5f;

    void Update()
    {
        if (director == null || startAnchor == null || endAnchor == null) return;
        float t = Mathf.InverseLerp(startTime, endTime, (float)director.time);
        t = t * t * (3f - 2f * t); // smoothstep
        transform.position = Vector3.Lerp(startAnchor.position, endAnchor.position, t);
    }
}
