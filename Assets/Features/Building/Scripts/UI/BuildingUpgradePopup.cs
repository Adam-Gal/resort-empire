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

    private PlacedBuilding building;
    private Action onUpgraded;

    private void Awake()
    {
        upgradeButton.onClick.AddListener(OnUpgradeClicked);
        closeButton.onClick.AddListener(() => dialog.SetActive(false));
        dialog.SetActive(false);
    }

    public void Show(PlacedBuilding target, Action upgraded)
    {
        building = target;
        onUpgraded = upgraded;

        BuildingDefinition definition = target.Definition;
        BuildingLevel current = definition.levels[target.Level];
        BuildingLevel next = definition.levels[target.Level + 1];

        title.text = $"{definition.displayName} · Level {target.Level + 2}";
        picture.sprite = definition.PictureAt(target.Level + 1);

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

        if (BuildingUpgrade.Instance.TryUpgrade(building)) onUpgraded?.Invoke();
    }
}
