using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class TourSceneSequencer : MonoBehaviour
{
    public string[] sceneSequence = {
        "Stage1_StoryMode",
        "Stage2_StoryMode",
        "Stage3_StoryMode",
        "Stage4_StoryMode",
        "Stage5_StoryMode"
    };
    public float[] sceneDurations = { 38f, 38f, 50f, 50f, 48f };
    public CanvasGroup fadeCanvas;
    public float fadeDuration = 0.5f;

    IEnumerator Start()
    {
        for (int i = 0; i < sceneSequence.Length; i++)
        {
            yield return StartCoroutine(FadeOut(fadeDuration));
            yield return SceneManager.LoadSceneAsync(sceneSequence[i], LoadSceneMode.Single);
            yield return StartCoroutine(FadeIn(fadeDuration));
            float dur = (i < sceneDurations.Length) ? sceneDurations[i] : 30f;
            yield return new WaitForSeconds(dur - fadeDuration);
        }
        yield return StartCoroutine(FadeOut(fadeDuration));
        SceneManager.LoadScene("MainMenu");
    }

    IEnumerator FadeOut(float dur)
    {
        if (fadeCanvas == null) yield break;
        float t = 0;
        while (t < dur) { fadeCanvas.alpha = t / dur; t += Time.deltaTime; yield return null; }
        fadeCanvas.alpha = 1f;
    }

    IEnumerator FadeIn(float dur)
    {
        if (fadeCanvas == null) yield break;
        float t = 0;
        while (t < dur) { fadeCanvas.alpha = 1f - (t / dur); t += Time.deltaTime; yield return null; }
        fadeCanvas.alpha = 0f;
    }
}
