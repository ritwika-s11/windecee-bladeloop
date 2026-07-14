using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public class StoryModeController : MonoBehaviour
{
    public PlayableDirector director;
    public CinemachineCamera freeOrbitCam;
    public GameObject pauseHintUI;

    bool paused = false;
    public bool IsPaused => paused;

    void Start()
    {
        if (freeOrbitCam != null) freeOrbitCam.Priority = 0;
        if (pauseHintUI != null) pauseHintUI.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.spaceKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
            TogglePause();
        if (kb.escapeKey.wasPressedThisFrame)
            BackToMenu();
    }

    public void TogglePause()
    {
        paused = !paused;
        if (director != null && director.playableGraph.IsValid())
            director.playableGraph.GetRootPlayable(0).SetSpeed(paused ? 0 : 1);
        if (freeOrbitCam != null)
            freeOrbitCam.Priority = paused ? 100 : 0;
        // The Timeline's Cinemachine track keeps overriding the Brain even at
        // speed 0, so priority alone never frees the camera. Disable the Brain
        // while paused: ExploreOrbitCamera then drives Camera.main directly,
        // and re-enabling snaps cleanly back to the story shot on resume.
        var mainCam = Camera.main;
        var brain = mainCam != null ? mainCam.GetComponent<CinemachineBrain>() : null;
        if (brain != null) brain.enabled = !paused;
        // Stage animations (trucks, cranes, kiln, conveyors) run on script clocks,
        // not the timeline — freeze game time so the whole scene pauses, not just
        // the director. This also keeps script clocks in sync with the timeline
        // on resume, and pauses all audio.
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        if (pauseHintUI != null) pauseHintUI.SetActive(paused);
    }

    public void BackToMenu()
    {
        // never leave the scene with frozen time/audio
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene("MainMenu");
    }
}
