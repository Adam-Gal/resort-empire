using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows what the next level of a building brings and buys it.
public class BuildingUpgradePopup : MonoBehaviour
{
    [SerializeField] private GameObject dialog;
    [SerializeField] private TMP_Text title;
    [SerializeField] private Image picture;
    [SerializeField] private TMP_Text capacity;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradePrice;
    [SerializeField] private Button closeButton;

    private Func<bool> tryUpgrade;
    private Action onUpgraded;

    private void Awake()
    {
        upgradeButton.onClick.AddListener(OnUpgradeClicked);
        closeButton.onClick.AddListener(() => dialog.SetActive(false));
        dialog.SetActive(false);
    }

    public void Show(PlacedBuilding target, Action upgraded) =>
        Show(target.Definition, target.Level, () => BuildingUpgrade.Instance.TryUpgrade(target), upgraded);

    // Anything with levels can use it, like a harbor that is not on the build grid.
    public void Show(BuildingDefinition definition, int level, Func<bool> upgrade, Action upgraded)
    {
        tryUpgrade = upgrade;
        onUpgraded = upgraded;

        BuildingLevel current = definition.levels[level];
        BuildingLevel next = definition.levels[level + 1];

        title.text = $"{definition.displayName} · Level {level + 2}";
        picture.sprite = definition.PictureAt(level + 1);

        capacity.gameObject.SetActive(next.capacity > 0);
        capacity.text = $"Kapacita: {current.capacity} → {next.capacity}";

        upgradePrice.text = $"Vylepšiť · {BuildMenuUI.FormatCost(next.upgradeCost)}";
        upgradeButton.interactable = ResourceManager.Instance.CanAfford(next.upgradeCost);

        dialog.SetActive(true);
    }

    private void OnUpgradeClicked()
    {
        // Guard against double taps.
        upgradeButton.interactable = false;
        dialog.SetActive(false);

        if (tryUpgrade != null && tryUpgrade()) onUpgraded?.Invoke();
    }
}
