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

// One service during a wave: its icon, name, one line about how it is doing and how full it is.
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

    //How full the busiest building is, hidden while the service is missing
    [SerializeField] private GameObject loadRoot;
    [SerializeField] private RectTransform loadFill;
    [SerializeField] private Image loadFillImage;

    [SerializeField] private Sprite normalBackground;
    [SerializeField] private Sprite problemBackground;
    [SerializeField] private Sprite missingBackground;
    [SerializeField] private Color normalColor = new Color32(0x66, 0x78, 0x7A, 0xFF);
    [SerializeField] private Color busyColor = new Color32(0xC9, 0x72, 0x1E, 0xFF);
    [SerializeField] private Color problemColor = new Color32(0xC9, 0x4A, 0x4A, 0xFF);
    [SerializeField] private Color normalFill = new Color32(0x08, 0x7F, 0x82, 0xFF);
    [SerializeField] private Color busyFill = new Color32(0xDF, 0x9E, 0x27, 0xFF);

    //Blinks per second of the warning while the service does not keep up
    [SerializeField, Min(0f)] private float warningBlinks = .8f;

    private bool isBlinking;

    private void Update()
    {
        if (!isBlinking) return;

        // Fades between full and a third, so it never fully disappears.
        float wave = .5f + .5f * Mathf.Cos(Time.unscaledTime * warningBlinks * 2f * Mathf.PI);
        Color color = warning.color;
        color.a = Mathf.Lerp(.35f, 1f, wave);
        warning.color = color;
    }

    public void Show(Sprite picture, string serviceName, string detail, ServiceCardState state, float load, Action onClick)
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
        isBlinking = state == ServiceCardState.Problem;

        loadRoot.SetActive(state != ServiceCardState.Missing);
        Vector2 max = loadFill.anchorMax;
        max.x = Mathf.Clamp01(load);
        loadFill.anchorMax = max;
        loadFillImage.color = state == ServiceCardState.Normal ? normalFill : state == ServiceCardState.Busy ? busyFill : problemColor;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        gameObject.SetActive(true);
    }
}
