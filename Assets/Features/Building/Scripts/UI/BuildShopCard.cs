using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One building in the shop: picture, size, name and price or the reason it is locked.
public class BuildShopCard : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image picture;
    [SerializeField] private TMP_Text sizeBadge;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private GameObject coinIcon;
    [SerializeField] private Color problemColor = new Color32(0xCD, 0x3A, 0x3A, 0xFF);
    [SerializeField] private Color priceColor = new Color32(0x16, 0x3E, 0x46, 0xFF);

    public void Show(BuildingDefinition definition, string price, string problem, Action onClick)
    {
        picture.sprite = definition.Picture;
        sizeBadge.text = $"{definition.size.x}×{definition.size.y}";
        nameText.text = definition.displayName;
        detailText.text = problem ?? price;
        detailText.color = problem != null ? problemColor : priceColor;
        coinIcon.SetActive(problem == null);
        button.interactable = problem == null;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        gameObject.SetActive(true);
    }
}
