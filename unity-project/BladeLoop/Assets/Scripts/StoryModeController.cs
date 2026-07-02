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
        if (pauseHintUI != null) pauseHintUI.SetActive(paused);
    }

    public void BackToMenu() { SceneManager.LoadScene("MainMenu"); }
}
