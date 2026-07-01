using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Dashboard for BladeLoop story mode. Displays real CEE numbers,
/// reacts to reactor temperature slider (drives kiln glow + flame size),
/// and recomputes CO2 avoided based on country grid factor.
/// Lives in the _Dashboard scene loaded additively over each stage.
/// </summary>
public class DashboardController : MonoBehaviour
{
    public static DashboardController Instance { get; private set; }

    [Header("KPI Text Refs")]
    public TMP_Text feedText;
    public TMP_Text energyText;
    public TMP_Text co2Text;
    public TMP_Text annualText;

    [Header("Parameter Controls")]
    public Slider reactorTempSlider;
    public TMP_Text reactorTempLabel;
    public TMP_Dropdown countryDropdown;
    public Toggle landfillCompareToggle;

    [Header("Stage Indicator")]
    public TMP_Text stageLabel;

    [Header("Story Controls")]
    public Button btnPauseResume;
    public TMP_Text btnPauseResumeLabel;
    public Button btnBackToMenu;

    [Header("CEE Constants (do not edit)")]
    public float feedRateKgPerHr = 6500f;
    public float glassFracOut = 0.70f;
    public float syngasFracOut = 0.24f;
    public float charFracOut = 0.06f;
    public float burnerDemandKW = 1734.8f;
    public float whrbOutputKW = 857.6f;
    public float shredderDrawKW = 780f;
    public float opHoursPerYear = 8000f;

    // Country grid CO2 (kg/kWh) - Germany / Netherlands / Denmark
    readonly float[] gridCO2 = { 0.358f, 0.328f, 0.135f };
    readonly string[] gridNames = { "Germany", "Netherlands", "Denmark" };

    // Live binding to current stage (assigned by DashboardSceneBinder in each stage)
    [Header("Live Stage Binding (auto-wired per stage)")]
    public Material kilnHotMaterial;
    public ParticleSystem[] flameParticles;
    public StoryModeController activeStoryController;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (reactorTempSlider != null)
        {
            reactorTempSlider.minValue = 550f;
            reactorTempSlider.maxValue = 660f;
            reactorTempSlider.value = 600f;
            reactorTempSlider.onValueChanged.AddListener(OnReactorTempChanged);
        }
        if (countryDropdown != null)
        {
            countryDropdown.ClearOptions();
            countryDropdown.AddOptions(new System.Collections.Generic.List<string>(gridNames));
            countryDropdown.onValueChanged.AddListener(_ => Refresh());
        }
        if (landfillCompareToggle != null)
            landfillCompareToggle.onValueChanged.AddListener(_ => Refresh());

        if (btnPauseResume != null)
            btnPauseResume.onClick.AddListener(OnPauseResumeClicked);
        if (btnBackToMenu != null)
            btnBackToMenu.onClick.AddListener(OnBackToMenuClicked);

        Refresh();
        OnReactorTempChanged(600f);
    }

    public void Refresh()
    {
        float netElec = whrbOutputKW - shredderDrawKW;

        if (feedText != null)
            feedText.text = $"<size=18><b>FEED</b></size>\n<size=42><b>{feedRateKgPerHr:N0}</b></size> <size=14>kg/h</size>\n<size=14>{feedRateKgPerHr * opHoursPerYear / 1000:N0} t/yr</size>";

        if (energyText != null)
            energyText.text = $"<size=18><b>ENERGY</b></size>\n<size=14>Burner: <b>{burnerDemandKW:N0}</b> kW</size>\n<size=14>WHRB: <b>{whrbOutputKW:N0}</b> kW</size>\n<size=14>Net: <color=#4A7CFF><b>+{netElec:N0} kW</b></color></size>";

        int ci = countryDropdown != null ? countryDropdown.value : 0;
        float plantElecPerYear = whrbOutputKW * opHoursPerYear; // kWh
        float co2Avoided = plantElecPerYear * gridCO2[ci] / 1000f; // tonnes

        if (co2Text != null)
            co2Text.text = $"<size=18><b>CO₂ AVOIDED</b></size>\n<size=42><b>{co2Avoided:N0}</b></size> <size=14>t/yr</size>\n<size=12>vs grid ({gridNames[ci]})</size>";

        float glassT = feedRateKgPerHr * glassFracOut * opHoursPerYear / 1000f;
        float charT = feedRateKgPerHr * charFracOut * opHoursPerYear / 1000f;
        float gasT = feedRateKgPerHr * syngasFracOut * opHoursPerYear / 1000f;

        if (annualText != null)
            annualText.text = $"<size=18><b>ANNUAL</b></size>\n<size=14>Glass: <b>{glassT:N0}</b> t</size>\n<size=14>Char: <b>{charT:N0}</b> t</size>\n<size=14>Gas: <b>{gasT:N0}</b> t</size>";
    }

    public void OnReactorTempChanged(float temp)
    {
        if (reactorTempLabel != null)
            reactorTempLabel.text = $"Reactor: <b>{temp:0}</b> °C";

        // Drive kiln glow (if bound)
        if (kilnHotMaterial != null)
        {
            float t = Mathf.InverseLerp(550f, 660f, temp);
            Color hotColor = Color.Lerp(new Color(0.8f, 0.15f, 0f), new Color(1.0f, 0.55f, 0.15f), t);
            float intensity = Mathf.Lerp(4f, 12f, t);
            kilnHotMaterial.SetColor("_EmissionColor", hotColor * Mathf.Pow(2f, intensity));
            kilnHotMaterial.EnableKeyword("_EMISSION");
        }

        // Drive flame sizes
        if (flameParticles != null)
        {
            float scale = Mathf.Lerp(0.6f, 1.4f, Mathf.InverseLerp(550f, 660f, temp));
            foreach (var ps in flameParticles)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startSizeMultiplier = scale;
            }
        }
    }

    public void OnPauseResumeClicked()
    {
        if (activeStoryController == null)
            activeStoryController = FindAnyObjectByType<StoryModeController>();
        if (activeStoryController != null)
        {
            activeStoryController.TogglePause();
            if (btnPauseResumeLabel != null)
                btnPauseResumeLabel.text = activeStoryController.IsPaused ? "Resume" : "Pause";
        }
    }

    public void OnBackToMenuClicked()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void SetStageLabel(string text)
    {
        if (stageLabel != null) stageLabel.text = text;
    }

    /// <summary>
    /// Called by DashboardSceneBinder in each stage to inject the kiln material + flame particles.
    /// </summary>
    public void BindStage(string stageName, Material kilnMat, ParticleSystem[] flames, StoryModeController story)
    {
        SetStageLabel(stageName);
        kilnHotMaterial = kilnMat;
        flameParticles = flames;
        activeStoryController = story;
        OnReactorTempChanged(reactorTempSlider != null ? reactorTempSlider.value : 600f);
    }
}
