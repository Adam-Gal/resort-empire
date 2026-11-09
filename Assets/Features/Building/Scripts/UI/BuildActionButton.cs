using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Round button with a small caption under it.
public class BuildActionButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text caption;
    [SerializeField] private GameObject captionCoin;

    //Look of a button that can't be used right now, like an upgrade in progress
    [SerializeField] private Color greyedColor = new Color32(0x9E, 0x9E, 0x9E, 0xFF);
    [SerializeField] private Color greyedCaptionColor = new Color32(0x8A, 0x8A, 0x8A, 0xFF);
    [SerializeField] private Color greyedIconColor = new Color(1f, 1f, 1f, .6f);

    // Colors from the prefab, put back when a greyed button is reused for a normal one.
    private Color captionColor;
    private Color iconColor;
    private bool colorsSaved;

    public void Show(string text, Sprite picture, Color color, bool interactable, Action onClick, bool showCoin = false)
    {
        SaveColors();
        caption.color = captionColor;
        icon.color = iconColor;

        caption.text = text;
        captionCoin.SetActive(showCoin);
        icon.sprite = picture;
        background.color = color;
        button.interactable = interactable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        gameObject.SetActive(true);
    }

    // Grey and not tappable, the caption says why.
    public void ShowGreyed(string text, Sprite picture)
    {
        Show(text, picture, greyedColor, false, () => { });
        caption.color = greyedCaptionColor;
        icon.color = greyedIconColor;
    }

    // Done on first use rather than in Awake, buttons are cloned from an inactive template.
    private void SaveColors()
    {
        if (colorsSaved) return;
        captionColor = caption.color;
        iconColor = icon.color;
        colorsSaved = true;
    }
}