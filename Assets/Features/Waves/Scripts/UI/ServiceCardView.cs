using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ServiceCardState
{
    Normal,

    // Some building barely keeps up.
    Busy,

    // Some building does not keep up.
    Problem,

    // The island has no building of this service.
    Missing
}

// One service during a wave: its icon, name and one line about how it is doing.
// The card takes the look of the worst building, tapping it lists the buildings.
public class ServiceCardView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text detailText;

    //Shown with the text when the service has a problem, so it reads without colour
    [SerializeField] private Image warning;

    [SerializeField] private Sprite normalBackground;
    [SerializeField] private Sprite problemBackground;
    [SerializeField] private Sprite missingBackground;
    [SerializeField] private Color normalColor = new Color32(0x66, 0x78, 0x7A, 0xFF);
    [SerializeField] private Color busyColor = new Color32(0xC9, 0x72, 0x1E, 0xFF);
    [SerializeField] private Color problemColor = new Color32(0xC9, 0x4A, 0x4A, 0xFF);

    public void Show(Sprite picture, string serviceName, string detail, ServiceCardState state, Action onClick)
    {
        icon.sprite = picture;
        nameText.text = serviceName;
        detailText.text = detail;

        background.sprite = state == ServiceCardState.Problem ? problemBackground
            : state == ServiceCardState.Missing ? missingBackground
            : normalBackground;
        Color text = state == ServiceCardState.Normal ? normalColor : state == ServiceCardState.Busy ? busyColor : problemColor;
        detailText.color = text;
        warning.gameObject.SetActive(state != ServiceCardState.Normal);
        warning.color = text;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        gameObject.SetActive(true);
    }
}
