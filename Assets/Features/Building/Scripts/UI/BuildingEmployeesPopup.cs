using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hiring and firing the Employees of a building.
public class BuildingEmployeesPopup : MonoBehaviour
{
    [SerializeField]
    private GameObject dialog;

    [SerializeField]
    private TMP_Text title;

    [SerializeField]
    private Image picture;

    [SerializeField]
    private TMP_Text Employees;

    [SerializeField]
    private Button hireButton;

    [SerializeField]
    private Button fireButton;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private TMP_Text hirePrice;

    private PlacedBuilding building;

    private void Awake()
    {
        hireButton.onClick.AddListener(() => BuildingManager.Instance.TryHire(building));
        fireButton.onClick.AddListener(() => BuildingManager.Instance.TryFire(building));
        closeButton.onClick.AddListener(Hide);
        dialog.SetActive(false);
    }

    private void Start()
    {
        BuildingManager.Instance.Changed += OnIslandChanged;
    }

    private void Update()
    {
        if (dialog.activeSelf && building != null)
            RefreshHireButton();
    }

    private void OnDestroy()
    {
        if (BuildingManager.Instance != null)
            BuildingManager.Instance.Changed -= OnIslandChanged;
    }

    private void RefreshHireButton()
    {
        ResourceCost[] cost = building.Definition.HireCostAt(building.Level);
        if (hirePrice != null)
            hirePrice.text = cost.Length > 0 ? $"Najať · {BuildMenuUI.FormatCost(cost)}" : "Najať";

        hireButton.interactable =
            building.Employees < building.MaxEmployees && ResourceManager.Instance.CanAfford(cost);
    }

    public void Show(PlacedBuilding target)
    {
        building = target;
        Refresh();
        dialog.SetActive(true);
    }

    public void Hide()
    {
        building = null;
        dialog.SetActive(false);
    }

    private void OnIslandChanged()
    {
        if (!dialog.activeSelf || building == null)
            return;

        // Destroy is delayed, so a demolished building is only missing from the list.
        bool stillPlaced = false;
        foreach (PlacedBuilding placed in BuildingManager.Instance.Placed)
            stillPlaced |= placed == building;

        if (stillPlaced)
            Refresh();
        else
            Hide();
    }

    private void Refresh()
    {
        BuildingDefinition definition = building.Definition;
        int max = building.MaxEmployees;

        title.text = definition.HasLevel(building.Level)
            ? $"{definition.displayName} · Level {building.Level + 1}"
            : definition.displayName;
        picture.sprite = definition.PictureAt(building.Level);

        Employees.text = $"Najaté: {building.Employees} / {max}";
        hireButton.interactable = building.Employees < max;
        fireButton.interactable = building.Employees > building.MinEmployees;
    }
}
