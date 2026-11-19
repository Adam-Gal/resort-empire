using TMPro;
using UnityEngine;

// Shows the resort value in the header and keeps it up to date.
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

    private void Show(long value) => valueText.text = value.ToString("N0");
}
