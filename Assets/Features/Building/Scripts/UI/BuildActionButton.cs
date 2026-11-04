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

    public void Show(string text, Sprite picture, Color color, bool interactable, Action onClick, bool showCoin = false)
    {
        caption.text = text;
        captionCoin.SetActive(showCoin);
        icon.sprite = picture;
        background.color = color;
        button.interactable = interactable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        gameObject.SetActive(true);
    }
}
