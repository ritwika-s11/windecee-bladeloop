using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads the _Dashboard scene additively whenever a stage scene becomes active,
/// and excludes MainMenu (and any other listed scenes). Survives scene loads via DontDestroyOnLoad.
/// </summary>
public class DashboardBootstrap : MonoBehaviour
{
    public string dashboardSceneName = "_Dashboard";
    public string[] excludedScenes = { "MainMenu" };

    static DashboardBootstrap _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        // Don't react when the dashboard itself just loaded
        if (s.name == dashboardSceneName) return;

        bool excluded = false;
        for (int i = 0; i < excludedScenes.Length; i++)
            if (s.name == excludedScenes[i]) { excluded = true; break; }

        bool alreadyLoaded = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).name == dashboardSceneName) { alreadyLoaded = true; break; }

        if (excluded)
        {
            // Unload if user went back to MainMenu
            if (alreadyLoaded) SceneManager.UnloadSceneAsync(dashboardSceneName);
            return;
        }

        if (!alreadyLoaded)
            SceneManager.LoadSceneAsync(dashboardSceneName, LoadSceneMode.Additive);
    }
}
