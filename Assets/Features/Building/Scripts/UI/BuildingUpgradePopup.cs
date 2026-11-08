using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows what the next level of a building brings and buys it.
// While that level is being built it shows the time left instead.
public class BuildingUpgradePopup : MonoBehaviour
{
    [SerializeField] private GameObject dialog;
    [SerializeField] private TMP_Text title;
    [SerializeField] private Image picture;
    [SerializeField] private TMP_Text capacity;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradePrice;
    [SerializeField] private Button closeButton;

    //Optional, an Image set to Filled that shows how far the upgrade is
    [SerializeField] private Image upgradeProgress;

    [SerializeField] private Button skipButton;
    [SerializeField] private TMP_Text skipPrice;

    private Func<bool> tryUpgrade;
    private Action onUpgraded;

    // The building whose running upgrade is on screen, the timer follows it.
    private PlacedBuilding upgrading;
    private bool showingUpgrade;

    private void Awake()
    {
        upgradeButton.onClick.AddListener(OnUpgradeClicked);
        closeButton.onClick.AddListener(Hide);
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
        dialog.SetActive(false);
    }

    public void Show(PlacedBuilding target, Action upgraded)
    {
        if (target.IsUpgrading)
            ShowUpgrading(target);
        else
            Show(target.Definition, target.Level, () => BuildingUpgrade.Instance.TryUpgrade(target), upgraded);
    }

    // Anything with levels can use it, like a harbor that is not on the build grid.
    public void Show(BuildingDefinition definition, int level, Func<bool> upgrade, Action upgraded)
    {
        StopTimer();
        tryUpgrade = upgrade;
        onUpgraded = upgraded;

        BuildingLevel next = ShowLevels(definition, level);
        upgradePrice.text = $"Vylepšiť · {BuildMenuUI.FormatCost(next.upgradeCost)}";
        upgradeButton.interactable = ResourceManager.Instance.CanAfford(next.upgradeCost);

        dialog.SetActive(true);
    }

    // The level being built, the upgrade button turns into a countdown.
    public void ShowUpgrading(PlacedBuilding target)
    {
        tryUpgrade = null;
        onUpgraded = null;

        ShowLevels(target.Definition, target.Level);
        upgradeButton.interactable = false;

        upgrading = target;
        showingUpgrade = true;
        if (upgradeProgress != null) upgradeProgress.gameObject.SetActive(true);
        RefreshTimer();

        dialog.SetActive(true);
    }

    public void Hide()
    {
        StopTimer();
        dialog.SetActive(false);
    }

    private void Update()
    {
        if (!showingUpgrade || !dialog.activeSelf) return;

        // Finished, or the building was demolished while the popup was open.
        if (upgrading == null || !upgrading.IsUpgrading)
        {
            Hide();
            return;
        }
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        upgradePrice.text = $"Vylepšuje sa · {BuildingUpgrade.Format(upgrading.UpgradeRemaining)}";
        if (upgradeProgress != null) upgradeProgress.fillAmount = upgrading.UpgradeProgress;
        RefreshSkip();
    }

    // The price drops as the upgrade goes on, so refresh is needed
    private void RefreshSkip()
    {
        if (skipButton == null) return;

        ResourceCost[] cost = BuildingUpgrade.Instance.GetSkipCost(upgrading);
        skipButton.gameObject.SetActive(cost != null);
        if (cost == null) return;

        if (skipPrice != null) skipPrice.text = $"Dokončiť · {BuildMenuUI.FormatCost(cost)}";
        skipButton.interactable = ResourceManager.Instance.CanAfford(cost);
    }

    private void StopTimer()
    {
        upgrading = null;
        showingUpgrade = false;
        if (upgradeProgress != null) upgradeProgress.gameObject.SetActive(false);
        if (skipButton != null) skipButton.gameObject.SetActive(false);
    }

    private void OnSkipClicked()
    {
        PlacedBuilding building = upgrading;
        if (building == null) return;

        // Guard against double taps.
        skipButton.interactable = false;
        if (BuildingUpgrade.Instance.TrySkip(building))
            Hide();
        else
            RefreshSkip();
    }

    // Title, picture and capacity of the level after this one, returns that level.
    private BuildingLevel ShowLevels(BuildingDefinition definition, int level)
    {
        BuildingLevel current = definition.levels[level];
        BuildingLevel next = definition.levels[level + 1];

        title.text = $"{definition.displayName} · Level {level + 2}";
        picture.sprite = definition.PictureAt(level + 1);

        capacity.gameObject.SetActive(next.capacity > 0);
        capacity.text = $"Kapacita: {current.capacity} → {next.capacity}";
        return next;
    }

    private void OnUpgradeClicked()
    {
        // Guard against double taps.
        upgradeButton.interactable = false;
        dialog.SetActive(false);

        if (tryUpgrade != null && tryUpgrade()) onUpgraded?.Invoke();
    }
}