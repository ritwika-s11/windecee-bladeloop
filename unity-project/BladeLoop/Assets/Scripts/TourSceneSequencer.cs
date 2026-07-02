using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using System.Collections;

/// <summary>
/// Full-plant story mode: plays the stage scenes back-to-back as one continuous story.
/// Persists across scene loads (DontDestroyOnLoad) and advances when each scene's
/// PlayableDirector actually finishes (falls back to sceneDurations if none is found).
/// The stage scenes themselves are untouched - this object just chains them.
/// </summary>
public class TourSceneSequencer : MonoBehaviour
{
    public string[] sceneSequence = {
        "Stage1_StoryMode",
        "Stage2_StoryMode",
        "Stage3_StoryMode"
    };
    [Tooltip("Fallback per-scene duration when a scene has no PlayableDirector.")]
    public float[] sceneDurations = { 38f, 38f, 112f };
    public CanvasGroup fadeCanvas;
    public float fadeDuration = 0.5f;
    [Tooltip("Extra hold on the last frame of each stage before cutting.")]
    public float endHold = 1.0f;

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        if (fadeCanvas != null) DontDestroyOnLoad(fadeCanvas.transform.root.gameObject);

        for (int i = 0; i < sceneSequence.Length; i++)
        {
            yield return StartCoroutine(Fade(0f, 1f));
            yield return SceneManager.LoadSceneAsync(sceneSequence[i], LoadSceneMode.Single);
            yield return null; // let Awake/Start run
            yield return StartCoroutine(Fade(1f, 0f));

            var director = FindFirstObjectByType<PlayableDirector>();
            if (director != null)
            {
                double dur = director.duration;
                // wait for the story to reach its end (pausing the story pauses the chain too)
                while (director != null && director.time < dur - 0.1)
                    yield return null;
            }
            else
            {
                float dur = (i < sceneDurations.Length) ? sceneDurations[i] : 30f;
                yield return new WaitForSeconds(dur);
            }
            yield return new WaitForSeconds(endHold);
        }

        yield return StartCoroutine(Fade(0f, 1f));
        var root = fadeCanvas != null ? fadeCanvas.transform.root.gameObject : null;
        SceneManager.LoadScene("MainMenu");
        if (root != null && root != gameObject) Destroy(root);
        Destroy(gameObject);
    }

    IEnumerator Fade(float from, float to)
    {
        if (fadeCanvas == null) yield break;
        float t = 0f;
        while (t < fadeDuration)
        {
            fadeCanvas.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            t += Time.deltaTime;
            yield return null;
        }
        fadeCanvas.alpha = to;
    }
}
