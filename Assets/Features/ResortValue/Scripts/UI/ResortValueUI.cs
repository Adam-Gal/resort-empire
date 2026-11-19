using TMPro;
using UnityEngine;

// Shows the resort value in the header, shortened to K, M and B, and keeps it up to date.
public class ResortValueUI : MonoBehaviour
{
    [SerializeField] private TMP_Text valueText;

    private void OnEnable()
    {
        ResortValueManager manager = ResortValueManager.Instance;
        if (manager == null) return;

        manager.ValueChanged += Show;
        Show(manager.Value);
    }

    private void OnDisable()
    {
        if (ResortValueManager.Instance != null) ResortValueManager.Instance.ValueChanged -= Show;
    }

    // Short like the other counters: 950, 5.7K, 1.2M, 3B.
    private void Show(long value) => valueText.text = ResourceCounterUI.Abbreviate(value);
}
