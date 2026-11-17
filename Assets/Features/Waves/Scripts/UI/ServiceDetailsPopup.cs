using System.Collections.Generic;
using ResortEmpire.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Buildings of one service on an island with how each copes with the wave.
// Those that do not keep up are red, those that barely do are orange, worst first.
public class ServiceDetailsPopup : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform panel;
    [SerializeField] private Image headerImage;
    [SerializeField] private Image serviceIcon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private ServiceBuildingRow rowTemplate;
    [SerializeField] private Button[] closeButtons;
    [SerializeField] private IslandCameraController cameraController;

    [Header("States")]
    [SerializeField] private Sprite warningIcon;
    [SerializeField] private Sprite speedUpIcon;
    [SerializeField] private ServiceBuildingRow.Look normalLook = new ServiceBuildingRow.Look { background = Color.white, strip = new Color32(0x24, 0x9E, 0xA0, 0xFF), text = new Color32(0x1E, 0x86, 0x88, 0xFF) };
    [SerializeField] private ServiceBuildingRow.Look busyLook = new ServiceBuildingRow.Look { background = new Color32(0xFF, 0xF1, 0xDE, 0xFF), strip = new Color32(0xD9, 0x82, 0x2B, 0xFF), text = new Color32(0xC9, 0x72, 0x1E, 0xFF) };
    [SerializeField] private ServiceBuildingRow.Look overloadedLook = new ServiceBuildingRow.Look { background = new Color32(0xFD, 0xE6, 0xE2, 0xFF), strip = new Color32(0xC9, 0x4A, 0x4A, 0xFF), text = new Color32(0xC9, 0x4A, 0x4A, 0xFF) };

    //A running speed up, the building is being helped
    [SerializeField] private ServiceBuildingRow.Look spedUpLook = new ServiceBuildingRow.Look { background = new Color32(0xEE, 0xEC, 0xE8, 0xFF), strip = new Color32(0x8B, 0x96, 0x99, 0xFF), text = new Color32(0x1E, 0x86, 0x88, 0xFF) };

    [Header("Size")]
    //Panel height around the rows, it grows with them up to maxHeight and then the list scrolls
    [SerializeField] private float headerHeight = 146f;
    [SerializeField] private float footerHeight = 62f;
    [SerializeField] private float rowHeight = 82f;
    [SerializeField] private float rowSpacing = 10f;
    [SerializeField] private float minHeight = 300f;
    [SerializeField] private float maxHeight = 700f;
    [SerializeField, Min(.1f)] private float refreshSeconds = .5f;

    private readonly List<ServiceBuildingRow> rows = new List<ServiceBuildingRow>();
    private string islandId;
    private BuildingCategory category;
    private string serviceName;
    private float sinceRefresh;

    private void Awake()
    {
        rowTemplate.gameObject.SetActive(false);
        foreach (Button close in closeButtons) close.onClick.AddListener(Hide);
        root.SetActive(false);
    }

    private void Update()
    {
        if (!root.activeSelf) return;

        // The wave on this island ended, nothing is left to watch.
        if (!WaveManager.Instance.IsWaveRunning(islandId)) { Hide(); return; }

        sinceRefresh += Time.unscaledDeltaTime;
        if (sinceRefresh >= refreshSeconds) Refresh();
    }

    public void Show(string island, BuildingCategory service, string displayName, Sprite icon, Sprite header)
    {
        islandId = island;
        category = service;
        serviceName = displayName;
        serviceIcon.sprite = icon;
        headerImage.sprite = header;
        root.SetActive(true);
        Refresh();
    }

    public void Hide() => root.SetActive(false);

    private void Refresh()
    {
        sinceRefresh = 0f;
        WaveManager waves = WaveManager.Instance;
        List<PlacedBuilding> buildings = waves.GetBuildings(islandId, category);
        buildings.Sort((a, b) => waves.GetServiceState(islandId, b).CompareTo(waves.GetServiceState(islandId, a)));

        int capacity = 0;
        foreach (PlacedBuilding building in buildings) capacity += building.Definition.CapacityAt(building.Level);
        titleText.text = $"{serviceName} · {IslandProgress.Instance.GetIslandName(islandId)}";
        summaryText.text = $"Budovy: {buildings.Count} · Kapacita {capacity}";

        for (int i = 0; i < buildings.Count; i++)
        {
            if (i == rows.Count) rows.Add(Instantiate(rowTemplate, rowTemplate.transform.parent));

            PlacedBuilding building = buildings[i];
            ServiceState state = waves.GetServiceState(islandId, building);
            float boostLeft = waves.GetSpeedUpSecondsLeft(islandId, building);
            bool spedUp = boostLeft > 0f;

            ServiceBuildingRow.Look look = spedUp ? spedUpLook : LookOf(state);
            Sprite icon = spedUp ? speedUpIcon : state == ServiceState.Normal ? null : warningIcon;
            string words = spedUp ? "Zrýchlené" : StateLabel(state);

            rows[i].Show(
                building.Definition.PictureAt(building.Level),
                building.Definition.displayName,
                building.Level + 1,
                look, icon, words,
                building.Definition.CapacityAt(building.Level),
                state == ServiceState.Overloaded,
                boostLeft,
                waves.GetSpeedUpProblem(islandId, building),
                waves.Settings.speedUpEnergy,
                () => ShowOnMap(building),
                () => SpeedUp(building));
        }
        for (int i = buildings.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);

        float listHeight = Mathf.Max(0, buildings.Count * (rowHeight + rowSpacing) - rowSpacing);
        Vector2 size = panel.sizeDelta;
        size.y = Mathf.Clamp(headerHeight + listHeight + footerHeight, minHeight, maxHeight);
        panel.sizeDelta = size;
    }

    private void ShowOnMap(PlacedBuilding building)
    {
        if (building == null) return;
        Hide();
        BuildingManager manager = BuildingManager.Instance;
        cameraController.FocusOn(manager.Grid.FootprintCenter(building.Origin, building.Definition.size));
    }

    private void SpeedUp(PlacedBuilding building)
    {
        // A second tap finds the building already sped up and does nothing.
        if (building != null && WaveManager.Instance.TrySpeedUp(islandId, building)) Refresh();
    }

    private static string StateLabel(ServiceState state) =>
        state == ServiceState.Overloaded ? "Nestíha" : state == ServiceState.Busy ? "Ledva stíha" : "V poriadku";

    private ServiceBuildingRow.Look LookOf(ServiceState state) =>
        state == ServiceState.Overloaded ? overloadedLook : state == ServiceState.Busy ? busyLook : normalLook;
}
