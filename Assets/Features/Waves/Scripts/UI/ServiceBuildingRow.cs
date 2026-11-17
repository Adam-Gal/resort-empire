using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One building in the service list: picture, name and level, its state with capacity,
// with buttons to show it on the map and, when it does not keep up, to speed it up.
// The strip on the left and the state line carry the colour of the state.
public class ServiceBuildingRow : MonoBehaviour
{
    [Serializable]
    public struct Look
    {
        public Color background;
        public Color strip;
        public Color text;
    }

    [SerializeField] private Image background;
    [SerializeField] private Image strip;
    [SerializeField] private Image picture;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image stateIcon;
    [SerializeField] private TMP_Text stateText;

    [Header("Actions")]
    [SerializeField] private Button mapButton;
    [SerializeField] private Button speedUpButton;
    [SerializeField] private TMP_Text speedUpText;

    //Shown instead of the speed up button while the speed up runs, with the time it has left
    [SerializeField] private GameObject activeChip;
    [SerializeField] private TMP_Text activeText;

    // speedUpSecondsLeft is above zero while a speed up runs.
    public void Show(Sprite sprite, string buildingName, int level, Look look, Sprite icon, string state, int capacity,
        bool offerSpeedUp, float speedUpSecondsLeft, string speedUpProblem, int speedUpPrice, Action onShowOnMap, Action onSpeedUp)
    {
        picture.sprite = sprite;
        nameText.text = $"{buildingName} · Level {level}";

        background.color = look.background;
        strip.color = look.strip;
        stateText.color = look.text;
        stateText.text = $"{state} · Kapacita {capacity}";
        stateIcon.sprite = icon;
        stateIcon.color = look.text;
        stateIcon.gameObject.SetActive(icon != null);

        mapButton.onClick.RemoveAllListeners();
        mapButton.onClick.AddListener(() => onShowOnMap());

        bool running = speedUpSecondsLeft > 0f;
        activeChip.SetActive(running);
        if (running)
        {
            int seconds = Mathf.CeilToInt(speedUpSecondsLeft);
            activeText.text = $"{seconds / 60}:{seconds % 60:00}";
        }

        speedUpButton.gameObject.SetActive(offerSpeedUp && !running);
        speedUpButton.interactable = speedUpProblem == null;
        speedUpText.text = SpeedUpLabel(speedUpProblem, speedUpPrice);
        speedUpButton.onClick.RemoveAllListeners();
        speedUpButton.onClick.AddListener(() => onSpeedUp());

        gameObject.SetActive(true);
    }

    // The energy icon sits right of the label, so the price is just the number.
    // Short on energy the button only greys out and keeps the price, so the player sees what it costs.
    private static string SpeedUpLabel(string problem, int price) =>
        problem == null || problem == WaveManager.NotEnoughEnergy ? $"Zrýchliť · {price}" : problem;
}
