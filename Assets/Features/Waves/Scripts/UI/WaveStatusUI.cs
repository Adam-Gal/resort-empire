using ResortEmpire.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Wave state of the island in the middle of the view, its start button and why it cannot start.
public class WaveStatusUI : MonoBehaviour
{
    [SerializeField] private IslandCameraController cameraController;
    [SerializeField] private WaveHud waveHud;
    [SerializeField] private IslandWaveSwitcher switcher;
    [SerializeField] private ServiceCardsUI serviceCards;

    [Header("Status")]
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private GameObject progressRoot;
    [SerializeField] private RectTransform progressFill;

    [Header("Start")]
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject noticeRoot;
    [SerializeField] private TMP_Text noticeText;

    private string islandId;
    private WavePhase? shownPhase;
    private bool isDirty = true;

    private void Start()
    {
        startButton.onClick.AddListener(OnStartClicked);
        WaveManager.Instance.WavesChanged += MarkDirty;
    }

    private void OnDestroy()
    {
        if (WaveManager.Instance != null) WaveManager.Instance.WavesChanged -= MarkDirty;
    }

    private void MarkDirty() => isDirty = true;

    private void Update()
    {
        WaveManager waves = WaveManager.Instance;
        string viewed = IslandProgress.Instance.GetOpenIslandIdAt(cameraController.ViewCenter);
        WaveSession session = waves.GetSession(viewed);
        WavePhase? phase = session?.Phase;

        if (isDirty || viewed != islandId || phase != shownPhase)
        {
            isDirty = false;
            islandId = viewed;
            shownPhase = phase;
            Refresh(waves, session);
        }

        if (session != null)
        {
            Vector2 max = progressFill.anchorMax;
            max.x = session.Phase == WavePhase.Running ? session.WindowProgress : 1f;
            progressFill.anchorMax = max;
        }
    }

    private void Refresh(WaveManager waves, WaveSession session)
    {
        string name = IslandProgress.Instance.GetIslandName(islandId);
        stateText.text = $"{name} · {StateLabel(session)}".ToUpperInvariant();
        progressRoot.SetActive(session != null);
        waveHud.SetWaveActive(session != null);

        string problem = session == null ? waves.GetStartProblem(islandId) : null;
        startButton.interactable = session == null && problem == null;
        noticeRoot.SetActive(problem != null);
        if (problem != null) noticeText.text = problem;

        if (switcher != null) switcher.Refresh(islandId);
        if (session != null) serviceCards.Show(islandId);
    }

    public static string StateLabel(WaveSession session)
    {
        if (session == null) return "Čaká sa na vlnu";
        return session.Phase == WavePhase.Running ? "Vlna prebieha" : "Vlna sa končí";
    }

    private void OnStartClicked()
    {
        // A second tap finds the wave running and does nothing.
        if (WaveManager.Instance.TryStartWave(islandId)) isDirty = true;
    }
}
