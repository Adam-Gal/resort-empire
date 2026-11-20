using System.Collections.Generic;
using ResortEmpire.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Wave overview at the top of the screen for the island in the middle of the view:
// which wave it is, how long guests keep coming and how far the wave got.
// Tapping it shows the wave's guests. Also runs the start button and says why a wave cannot start.
// During a wave a warning card in the notice stack says when the resort is overloaded,
// tapping it opens the busiest service.
public class WaveStatusUI : MonoBehaviour
{
    [SerializeField] private IslandCameraController cameraController;
    [SerializeField] private WaveHud waveHud;
    [SerializeField] private IslandWaveSwitcher switcher;
    [SerializeField] private ServiceCardsUI serviceCards;

    [Header("Overview")]
    [FormerlySerializedAs("stateText")]
    [SerializeField] private TMP_Text titleText;

    //Time left while guests come, the phase after that
    [SerializeField] private TMP_Text timeText;

    //Shown in place of the progress bar when no wave runs
    [SerializeField] private TMP_Text idleText;

    [SerializeField] private Image icon;
    [SerializeField] private Sprite runningIcon;
    [SerializeField] private Sprite idleIcon;

    [SerializeField] private GameObject progressRoot;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private Image progressFillImage;

    [SerializeField] private Color normalColor = new Color32(0x66, 0x78, 0x7A, 0xFF);
    [SerializeField] private Color busyColor = new Color32(0xC9, 0x72, 0x1E, 0xFF);
    [SerializeField] private Color doneColor = new Color32(0x08, 0x7F, 0x82, 0xFF);
    [SerializeField] private Color runningFill = new Color32(0x08, 0x7F, 0x82, 0xFF);
    [SerializeField] private Color finishingFill = new Color32(0xDF, 0x9E, 0x27, 0xFF);

    //How long a finished wave stays on the bar before it goes back to waiting
    [SerializeField, Min(0f)] private float endedSeconds = 3f;

    [Header("Guests")]
    //The whole bar, tapping it opens the guests of the wave
    [SerializeField] private Button barButton;
    [SerializeField] private WaveGuestsPopup guestsPopup;

    [Header("Start")]
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject noticeRoot;
    [SerializeField] private TMP_Text noticeText;

    [Header("Overload")]
    //The notices on the left, the same as the path errors use
    [SerializeField] private NoticeStack notices;

    //Loads change all the time, the warning checks them this often
    [SerializeField, Min(.05f)] private float overloadCheckSeconds = .25f;

    private string islandId;
    private WavePhase? shownPhase;
    private bool isDirty = true;
    private int shownSeconds = -1;

    // The wave that just ended on some island and its guests' final counts, shown while the timer runs.
    private WaveSession endedSession;
    private GuestSimulation endedGuests;
    private float endedLeft;

    private OverloadWarning overloadWarningInstance;
    private List<WaveSettings.Requirement> overloaded = new List<WaveSettings.Requirement>();
    private float sinceOverloadCheck;

    //Overload text on screen now, null when none
    private string overloadNotice;

    private void Start()
    {
        startButton.onClick.AddListener(OnStartClicked);
        barButton.onClick.AddListener(OnBarClicked);

        WaveManager.Instance.WavesChanged += MarkDirty;
        WaveManager.Instance.WaveEnded += OnWaveEnded;
        if (GuestManager.Instance != null) GuestManager.Instance.GuestsFinished += OnGuestsFinished;
    }

    private void OnDestroy()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.WavesChanged -= MarkDirty;
            WaveManager.Instance.WaveEnded -= OnWaveEnded;
        }
        if (GuestManager.Instance != null) GuestManager.Instance.GuestsFinished -= OnGuestsFinished;
    }

    private void MarkDirty() => isDirty = true;

    // Made on first use, so it also exists again after scripts reload during play.
    private OverloadWarning overloadWarning
    {
        get
        {
            if (overloadWarningInstance == null)
            {
                WaveSettings settings = WaveManager.Instance.Settings;
                overloadWarningInstance = new OverloadWarning(settings.overloadWarningDelay, settings.overloadWarningHold);
            }
            return overloadWarningInstance;
        }
    }

    // Both end events come in the same frame, in either order, so each keeps what the other set.
    private void OnWaveEnded(WaveSession session)
    {
        endedSession = session;
        endedLeft = endedSeconds;
        isDirty = true;
    }

    private void OnGuestsFinished(GuestSimulation guests)
    {
        endedGuests = guests;
        isDirty = true;
    }

    // Final guests of the wave that just ended, null when they belong to another wave.
    private GuestSimulation EndedGuests =>
        endedGuests != null && endedSession != null
        && endedGuests.Data.islandId == endedSession.IslandId && endedGuests.Data.waveNumber == endedSession.Number
            ? endedGuests : null;

    private void Update()
    {
        if (endedSession != null)
        {
            endedLeft -= Time.unscaledDeltaTime;
            if (endedLeft <= 0f)
            {
                endedSession = null;
                endedGuests = null;
                isDirty = true;
            }
        }

        WaveManager waves = WaveManager.Instance;
        string viewed = IslandProgress.Instance.GetOpenIslandIdAt(cameraController.ViewCenter);
        WaveSession session = waves.GetSession(viewed);
        WavePhase? phase = session?.Phase;

        if (viewed != islandId) ResetOverload();

        if (isDirty || viewed != islandId || phase != shownPhase)
        {
            isDirty = false;
            islandId = viewed;
            shownPhase = phase;
            shownSeconds = -1;
            Refresh(waves, session);
        }

        if (session != null && session.Phase == WavePhase.Running)
        {
            SetProgress(session.WindowProgress);
            ShowTimeLeft(session);
        }

        if (session != null) CheckOverload(waves);
        else if (overloadNotice != null) ResetOverload();
    }

    private void ResetOverload()
    {
        overloadWarning.Reset();
        overloaded.Clear();
        ShowOverload();
    }

    // Guests keep asking more than the basic services manage.
    private void CheckOverload(WaveManager waves)
    {
        sinceOverloadCheck += Time.deltaTime;
        if (sinceOverloadCheck < overloadCheckSeconds) return;

        List<WaveSettings.Requirement> now = waves.GetOverloadedServices(islandId);
        // While it calms down the warning keeps naming what was overloaded last.
        if (now.Count > 0) overloaded = now;
        overloadWarning.Update(now.Count > 0, sinceOverloadCheck);
        sinceOverloadCheck = 0f;
        ShowOverload();
    }

    // A warning card while the overload lasts, a new text replaces the old card.
    private void ShowOverload()
    {
        string text = null;
        if (overloadWarning.Visible && overloaded.Count > 0)
        {
            var names = new List<string>();
            foreach (WaveSettings.Requirement requirement in overloaded) names.Add(requirement.displayName);
            text = $"Rezort je preťažený, nestíha: {string.Join(", ", names)}";
        }

        if (notices == null || text == overloadNotice) return;

        // Shown once per text, so a card the player closed stays closed while the same services lag.
        if (overloadNotice != null) notices.Hide(overloadNotice);
        overloadNotice = text;
        if (text != null) notices.Show(text, NoticeLevel.Warning, OnOverloadClicked, true);
    }

    private void OnOverloadClicked()
    {
        if (overloaded.Count == 0) return;
        serviceCards.ShowDetails(islandId, overloaded[0].category);
    }

    private void Refresh(WaveManager waves, WaveSession session)
    {
        string name = IslandProgress.Instance.GetIslandName(islandId);
        bool justEnded = session == null && endedSession != null && endedSession.IslandId == islandId;
        WaveSession shown = session ?? (justEnded ? endedSession : null);

        int number = shown != null ? shown.Number : waves.GetWaveCount(islandId) + 1;
        titleText.text = $"{name} · Vlna {number}";
        icon.sprite = shown != null ? runningIcon : idleIcon;

        progressRoot.SetActive(shown != null);
        idleText.gameObject.SetActive(shown == null);
        timeText.gameObject.SetActive(shown != null);
        barButton.interactable = shown != null;

        if (justEnded)
        {
            SetProgress(1f);
            progressFillImage.color = runningFill;
            SetTime("Dokončená", doneColor);
        }
        else if (session != null && session.Phase != WavePhase.Running)
        {
            // Guests stopped coming, the bar waits full for the last orders.
            SetProgress(1f);
            progressFillImage.color = finishingFill;
            SetTime("Dokončuje sa", busyColor);
        }
        else if (session != null)
        {
            progressFillImage.color = runningFill;
            timeText.color = normalColor;
        }
        else
        {
            idleText.text = "Čaká na spustenie";
        }

        GuestSimulation guests = session != null ? GuestManager.Instance?.GetSimulation(islandId) : (justEnded ? EndedGuests : null);
        if (shown == null) guestsPopup.Hide();
        else guestsPopup.SetGuests(guests);

        waveHud.SetWaveActive(session != null);

        string problem = session == null ? waves.GetStartProblem(islandId) : null;
        startButton.interactable = session == null && problem == null;
        // Pinned on top of the notices on the left, so it is never hidden under them.
        if (notices != null)
        {
            noticeRoot.SetActive(false);
            notices.SetPinned(problem);
        }
        else
        {
            noticeRoot.SetActive(problem != null);
            if (problem != null) noticeText.text = problem;
        }

        if (switcher != null) switcher.Refresh(islandId);
        if (session != null) serviceCards.Show(islandId);
    }

    private void SetProgress(float progress)
    {
        Vector2 max = progressFill.anchorMax;
        max.x = progress;
        progressFill.anchorMax = max;
    }

    private void SetTime(string text, Color color)
    {
        timeText.text = text;
        timeText.color = color;
    }

    // Rewrites the countdown only when the shown second changes.
    private void ShowTimeLeft(WaveSession session)
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, session.RequestWindowSeconds - session.ElapsedSeconds));
        if (seconds == shownSeconds) return;
        shownSeconds = seconds;
        timeText.text = $"{seconds / 60}:{seconds % 60:00}";
    }

    public static string StateLabel(WaveSession session)
    {
        if (session == null) return "Čaká sa na vlnu";
        return session.Phase == WavePhase.Running ? "Vlna prebieha" : "Vlna sa končí";
    }

    private void OnBarClicked()
    {
        WaveSession session = WaveManager.Instance.GetSession(islandId);
        GuestSimulation guests = session != null ? GuestManager.Instance?.GetSimulation(islandId) : EndedGuests;
        guestsPopup.Toggle(guests);
    }

    private void OnStartClicked()
    {
        // A second tap finds the wave running and does nothing.
        if (WaveManager.Instance.TryStartWave(islandId)) isDirty = true;
    }
}
