using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Stops SPACE from firing whichever UI button the viewer last clicked.
///
/// THE BUG
/// -------
/// Space is meant to do exactly one thing: StoryModeController.TogglePause(). In Stage 3
/// it also jumps the tour forward, which reads to a player as three different Space keys
/// across five scenes.
///
/// It is not a second key binding. Unity's EventSystem keeps a button SELECTED after it
/// is clicked, and the InputSystemUIInputModule binds its Submit action to
/// DefaultInputActions UI/Submit - which is Space and Enter. So once the viewer has
/// clicked any button, every later Space press does two things at once:
///
///     StoryModeController.Update()  ->  TogglePause()
///     EventSystem Submit            ->  re-clicks the still-selected button
///
/// In Stage 3 the buttons on screen include TourControls' "Next Stage" and the subtitle
/// bar's "Next", so the second one wins visibly and the tour walks forward. Stages 1, 2
/// and 4 have the identical setup and the identical latent bug - they only behave because
/// nothing has been clicked yet in that run. Click "Next" once in Stage 1 and it does the
/// same thing.
///
/// This is why it looked scene-specific and was not.
///
/// THE FIX
/// -------
/// sendNavigationEvents = false. Keyboard submit / cancel / arrow-navigation events stop
/// being generated; pointer events are untouched, so every button still works with the
/// mouse exactly as before.
///
/// That is the right trade here rather than a regression: this is a mouse-driven film UI,
/// nothing in it is reachable by Tab today, and the on-screen overlay tells the viewer
/// "PRESS SPACE TO EXPLORE" - a promise the Submit binding was quietly breaking.
///
/// If keyboard navigation is ever wanted, the alternative is clearing the selection after
/// every click instead (EventSystem.SetSelectedGameObject(null) on each button's onClick).
/// Same effect on Space, keeps Tab working, but has to be wired per button.
///
/// NO SCENE EDIT. Spawns itself after each scene load, the same pattern TourControls,
/// OrderPanel and KilnShellGrade use. NO existing script is touched - in particular not
/// StoryModeController, TourControls or the explore rig, since explore mode is Sharan's.
/// It also runs in free play, because the bug is in free play too.
/// </summary>
public class TourUISubmitGuard : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnLoaded(Scene s, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<TourUISubmitGuard>() != null) return;
        new GameObject("~TourUISubmitGuard").AddComponent<TourUISubmitGuard>();
    }

    void Start() { Disarm(); }

    // The EventSystem can be created after us (OrderPanel and TourControls both build UI
    // at runtime), and a newly spawned one arrives armed. Cheap enough to keep checking.
    void Update() { Disarm(); }

    static void Disarm()
    {
        foreach (var es in FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (es == null) continue;
            if (es.sendNavigationEvents) es.sendNavigationEvents = false;

            // Anything already selected would still highlight and would still take a
            // stray Enter, so let it go too.
            if (es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
        }
    }
}
