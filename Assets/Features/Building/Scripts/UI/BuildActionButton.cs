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

    public void Show(string text, Sprite picture, Color color, bool interactable, Action onClick)
    {
        caption.text = text;
        icon.sprite = picture;
        background.color = color;
        button.interactable = interactable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        gameObject.SetActive(true);
    }
}
