using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One island in the island switcher: its name and wave state in words, so it reads without colour.
public class IslandWaveChip : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text stateText;

    public void Show(string islandName, string state, Color color, Action onClick)
    {
        nameText.text = islandName;
        stateText.text = state;
        button.image.color = color;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        gameObject.SetActive(true);
    }
}
