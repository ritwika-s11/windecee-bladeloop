using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Additive helper: returns to the Main Menu from any stage scene.
///
/// Put this on the same GameObject as the Button — it wires itself up on Awake,
/// so no persistent listener needs to be serialised in the scene.
///
/// It handles three things that a plain SceneManager.LoadScene would get wrong:
///  1. The Full Plant Tour runs a persistent TourSceneSequencer (DontDestroyOnLoad).
///     If it is left alive it will keep loading the next stage on top of the menu,
///     so it is stopped and destroyed first.
///  2. Explore mode pauses a stage with Time.timeScale = 0 and AudioListener.pause,
///     both of which must be restored or the menu would load frozen and silent.
///  3. Stage scenes do not all contain an EventSystem, without which no UI button
///     is clickable. One is created only if the scene has none.
/// </summary>
[RequireComponent(typeof(Button))]
public class BackToMenuButton : MonoBehaviour
{
    [Tooltip("Scene to return to. Must be in Build Settings.")]
    public string menuSceneName = "MainMenu";

    void Awake()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem),
                           typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(GoToMainMenu);
    }

    public void GoToMainMenu()
    {
        // stage may have been paused by Explore mode
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // stop the chained tour, otherwise it loads the next stage over the menu
        var seq = Object.FindFirstObjectByType<TourSceneSequencer>();
        if (seq != null)
        {
            seq.StopAllCoroutines();
            if (seq.fadeCanvas != null)
            {
                var fadeRoot = seq.fadeCanvas.transform.root.gameObject;
                if (fadeRoot != seq.transform.root.gameObject) Destroy(fadeRoot);
            }
            Destroy(seq.transform.root.gameObject);
        }

        SceneManager.LoadScene(menuSceneName);
    }
}
