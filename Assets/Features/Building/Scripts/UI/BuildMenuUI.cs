using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small round actions for the selected cell, and a side panel shop with categories.
public class BuildMenuUI : MonoBehaviour
{
    [SerializeField] private BuildPlacementController placement;
    [SerializeField] private Button openButton;

    //Hint on the build button after tapping a free cell
    [SerializeField] private float openPulseScale = 1.25f;
    [SerializeField] private float openPulseDuration = .35f;

    [Header("Selection")]
    [SerializeField] private GameObject selectionBar;
    [SerializeField] private TMP_Text selectionTitle;
    [SerializeField] private BuildActionButton actionTemplate;
    [SerializeField] private Sprite buildIcon;
    [SerializeField] private Sprite upgradeIcon;
    [SerializeField] private Sprite moveIcon;
    [SerializeField] private Sprite demolishIcon;
    [SerializeField] private Sprite clearIcon;
    [SerializeField] private Color primaryColor = new Color32(0x27, 0x8E, 0x89, 0xFF);
    [SerializeField] private Color secondaryColor = new Color32(0x16, 0x3E, 0x46, 0xFF);
    [SerializeField] private Color destructiveColor = new Color32(0xCD, 0x3A, 0x3A, 0xFF);

    [Header("Shop")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Button closeShopButton;
    [SerializeField] private BuildShopCard cardTemplate;
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private Color activeTabColor = new Color32(0x27, 0x8E, 0x89, 0xFF);
    [SerializeField] private Color idleTabColor = new Color32(0xFF, 0xF5, 0xE3, 0xFF);
    [SerializeField] private Color activeTabText = new Color32(0xFF, 0xF5, 0xE3, 0xFF);
    [SerializeField] private Color idleTabText = new Color32(0x16, 0x3E, 0x46, 0xFF);

    [Header("Placement")]
    [SerializeField] private GameObject placementBar;
    [SerializeField] private TMP_Text placementHint;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Demolish dialog")]
    [SerializeField] private GameObject demolishDialog;
    [SerializeField] private TMP_Text demolishTitle;
    [SerializeField] private TMP_Text demolishRefund;
    [SerializeField] private Button demolishConfirm;
    [SerializeField] private Button[] demolishCancel;

    [Header("Upgrade")]
    [SerializeField] private BuildingUpgradePopup upgradePopup;

    //Buildings are only upgraded between waves
    [SerializeField] private WaveHud waveHud;

    private readonly List<BuildActionButton> actions = new List<BuildActionButton>();
    private readonly List<BuildShopCard> cards = new List<BuildShopCard>();
    private int usedActions;
    private Vector3? placeAt;
    private BuildingCategory? shopFilter;
    private Coroutine openPulse;

    private void Start()
    {
        openButton.onClick.AddListener(() => OpenShop(placeAt));
        closeShopButton.onClick.AddListener(CloseAll);
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(EndPlacement);

        // First tab is everything, the rest follow BuildingCategory.
        for (int i = 0; i < tabButtons.Length; i++)
        {
            BuildingCategory? filter = i == 0 ? (BuildingCategory?)null : (BuildingCategory)(i - 1);
            tabButtons[i].onClick.AddListener(() => ShowShopTab(filter));
        }

        placement.CanConfirmChanged += value => confirmButton.interactable = value;
        placement.Tapped += ShowSelection;

        foreach (Button cancel in demolishCancel)
            cancel.onClick.AddListener(() => demolishDialog.SetActive(false));
        demolishDialog.SetActive(false);

        actionTemplate.gameObject.SetActive(false);
        cardTemplate.gameObject.SetActive(false);
        CloseAll();
        placementBar.SetActive(false);
    }

    public void ShowSelection(Vector2Int cell, UnityEngine.Object occupant, Vector3 world)
    {
        BuildingManager manager = BuildingManager.Instance;
        shopPanel.SetActive(false);
        placeAt = null;
        BeginActions();

        if (occupant is PlacedBuilding building)
            ShowBuilding(building);
        else if (occupant is RuinedBuilding ruin)
            ShowRuin(ruin);
        else if (manager.GetClearCost(cell) != null)
            ShowObstacle(cell);
        else
            ShowGround(cell, world);

        selectionBar.SetActive(usedActions > 0);
    }

    private void ShowBuilding(PlacedBuilding building)
    {
        BuildingManager manager = BuildingManager.Instance;
        selectionTitle.text = building.Definition.HasLevel(building.Level)
            ? $"{building.Definition.displayName} · Level {building.Level + 1}"
            : building.Definition.displayName;
        placement.ShowSelection(building.Origin, building.Definition.size, building.transform.GetChild(0));

        if (BuildingUpgrade.Instance.CanUpgrade(building) && !waveHud.IsWaveActive)
            AddAction("Vylepšiť", upgradeIcon, primaryColor, true, () => upgradePopup.Show(building, () => ShowUpgraded(building)));

        AddAction("Presunúť", moveIcon, secondaryColor, true, () =>
        {
            CloseAll();
            BeginPlacing(building.Definition, null);
            placement.BeginMove(building);
        });
        if (building.Definition.IsDecoration)
        {
            // Cleared like jungle, nothing is paid back.
            AddAction("Cost: 0", clearIcon, primaryColor, true, () =>
            {
                manager.Remove(building);
                CloseAll();
            }, true);
            return;
        }

        AddAction("Zbúrať", demolishIcon, destructiveColor, true, () => OpenDemolishDialog(building));
    }

    // Selecting the new art again pops it, which is the upgrade animation.
    private void ShowUpgraded(PlacedBuilding building)
    {
        BeginActions();
        ShowBuilding(building);
    }

    private void OpenDemolishDialog(PlacedBuilding building)
    {
        string refund = FormatCost(BuildingManager.Instance.GetRefund(building), false);
        demolishTitle.text = $"Zbúrať {building.Definition.displayName}?";
        demolishRefund.text = $"Získaš späť: {refund}";
        demolishConfirm.onClick.RemoveAllListeners();
        demolishConfirm.onClick.AddListener(() =>
        {
            demolishDialog.SetActive(false);
            BuildingManager.Instance.Demolish(building);
            CloseAll();
        });
        demolishDialog.SetActive(true);
    }

    private void ShowRuin(RuinedBuilding ruin)
    {
        selectionTitle.text = ruin.RestoredBuilding.displayName + " · zničený";
        placement.ShowSelection(ruin.Origin, ruin.RestoredBuilding.size, ruin.transform);

        bool canAfford = ResourceManager.Instance.CanAfford(ruin.RestoreCost);
        AddAction("Obnoviť " + FormatCost(ruin.RestoreCost), buildIcon, primaryColor, canAfford, () =>
        {
            BuildingManager.Instance.TryRestore(ruin);
            CloseAll();
        }, true);
    }

    private void ShowObstacle(Vector2Int cell)
    {
        BuildingManager manager = BuildingManager.Instance;
        manager.Grid.TryGetObstacle(cell, out Vector2Int origin);
        Vector2Int size = manager.Grid.GetObstacleSize(origin);
        ResourceCost[] cost = manager.GetClearCost(cell);

        selectionTitle.text = manager.Grid.GetCategory(cell).displayName;
        placement.ShowSelection(origin, size, null, true);
        AddAction("Cost: " + FormatCost(cost), clearIcon, primaryColor, ResourceManager.Instance.CanAfford(cost), () =>
        {
            manager.TryClear(cell);
            CloseAll();
        }, true);
    }

    private void ShowGround(Vector2Int cell, Vector3 world)
    {
        BuildingManager manager = BuildingManager.Instance;
        TileCategory category = manager.Grid.GetCategory(cell);
        placement.ShowSelection(cell, Vector2Int.one);

        foreach (BuildingDefinition definition in manager.Buildings)
        {
            if (!definition.CanStandOn(category)) continue;

            placeAt = world;
            PulseOpenButton();
            return;
        }
    }

    private void PulseOpenButton()
    {
        if (openPulse != null) StopCoroutine(openPulse);
        openPulse = StartCoroutine(PulseOpenButtonRoutine());
    }

    private IEnumerator PulseOpenButtonRoutine()
    {
        Transform button = openButton.transform;
        for (float time = 0f; time < openPulseDuration; time += Time.unscaledDeltaTime)
        {
            float wave = Mathf.Sin(time / openPulseDuration * Mathf.PI);
            button.localScale = Vector3.one * (1f + (openPulseScale - 1f) * wave);
            yield return null;
        }
        button.localScale = Vector3.one;
        openPulse = null;
    }

    private void OpenShop(Vector3? world)
    {
        placeAt = world;
        selectionBar.SetActive(false);
        placement.ClearSelection();
        shopPanel.SetActive(true);
        ShowShopTab(shopFilter);
    }

    private void ShowShopTab(BuildingCategory? filter)
    {
        shopFilter = filter;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            bool active = i == 0 ? filter == null : filter == (BuildingCategory)(i - 1);
            tabButtons[i].image.color = active ? activeTabColor : idleTabColor;
            tabButtons[i].GetComponentInChildren<TMP_Text>().color = active ? activeTabText : idleTabText;
        }

        foreach (BuildShopCard card in cards) card.gameObject.SetActive(false);

        int used = 0;
        foreach (BuildingDefinition definition in BuildingManager.Instance.Buildings)
        {
            if (filter != null && definition.category != filter) continue;

            if (used == cards.Count)
                cards.Add(Instantiate(cardTemplate, cardTemplate.transform.parent));
            cards[used++].Show(definition, FormatCost(definition.cost), GetProblem(definition), () => OnCardPicked(definition));
        }
    }

    private string GetProblem(BuildingDefinition definition)
    {
        if (!ResourceManager.Instance.CanAfford(definition.cost)) return "Málo mincí";
        if (!placeAt.HasValue) return null;

        BuildGrid grid = BuildingManager.Instance.Grid;
        Vector2Int origin = BuildPlacementController.OriginAround(definition, grid.WorldToCell(placeAt.Value));
        return grid.CanPlace(definition, origin) ? null : "Nezmestí sa";
    }

    private void OnCardPicked(BuildingDefinition definition)
    {
        Vector3? world = placeAt;
        CloseAll();
        BeginPlacing(definition, FormatCost(definition.cost));
        placement.BeginPlace(definition, world);
    }

    private void BeginPlacing(BuildingDefinition definition, string price)
    {
        placementHint.text = price != null ? $"Ťahaj budovu · {price}" : "Ťahaj budovu";
        placementBar.SetActive(true);
    }

    private void BeginActions()
    {
        usedActions = 0;
        foreach (BuildActionButton action in actions) action.gameObject.SetActive(false);
    }

    private void AddAction(string text, Sprite icon, Color color, bool interactable, Action onClick, bool showCoin = false)
    {
        if (usedActions == actions.Count)
            actions.Add(Instantiate(actionTemplate, actionTemplate.transform.parent));
        actions[usedActions++].Show(text, icon, color, interactable, onClick, showCoin);
    }

    private void CloseAll()
    {
        placeAt = null;
        selectionBar.SetActive(false);
        shopPanel.SetActive(false);
        placement.ClearSelection();
    }

    private void OnConfirm()
    {
        placement.Confirm();
        placementBar.SetActive(placement.IsPlacing);
    }

    private void EndPlacement()
    {
        placement.Cancel();
        placementBar.SetActive(false);
    }

    // abbreviate keeps short labels short, full numbers read better in sentences.
    public static string FormatCost(ResourceCost[] costs, bool abbreviate = true)
    {
        var text = new StringBuilder();
        foreach (ResourceCost cost in costs)
        {
            if (text.Length > 0) text.Append(", ");
            text.Append(abbreviate ? ResourceCounterUI.Abbreviate(cost.amount) : cost.amount.ToString("N0"));
        }
        return text.ToString();
    }
}
