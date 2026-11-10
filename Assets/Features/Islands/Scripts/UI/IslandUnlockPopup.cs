using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Asks whether to open an island whose reputation requirement is met.
// Closing it leaves the island locked, tapping the island brings the dialog back.
public class IslandUnlockPopup : MonoBehaviour
{
    [SerializeField] private GameObject dialog;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [SerializeField] private Button unlockButton;
    [SerializeField] private Button laterButton;
    [SerializeField] private Button closeButton;

    private Action onUnlock;
    private Action onClosed;

    public bool IsOpen => dialog.activeSelf;

    private void Awake()
    {
        unlockButton.onClick.AddListener(OnUnlockClicked);
        if (laterButton != null) laterButton.onClick.AddListener(Close);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        dialog.SetActive(false);
    }

    public void Show(string islandName, long requiredReputation, Action unlock, Action closed)
    {
        onUnlock = unlock;
        onClosed = closed;

        title.text = $"{islandName} čaká";
        body.text = $"Tvoja reputácia dosiahla {requiredReputation:N0}. {islandName} môžeš teraz odomknúť a stavať na ňom.";
        unlockButton.interactable = true;
        dialog.SetActive(true);
    }

    private void OnUnlockClicked()
    {
        // Guard against double taps.
        unlockButton.interactable = false;
        Hide(true);
    }

    private void Close() => Hide(false);

    private void Hide(bool unlock)
    {
        dialog.SetActive(false);
        Action unlocked = onUnlock, closed = onClosed;
        onUnlock = null;
        onClosed = null;
        if (unlock) unlocked?.Invoke();
        closed?.Invoke();
    }
}
