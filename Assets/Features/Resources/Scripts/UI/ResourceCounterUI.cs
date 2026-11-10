using TMPro;
using UnityEngine;

// autoupdates one resource info
public class ResourceCounterUI : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType = ResourceType.Money;
    [SerializeField] private TMP_Text amountText;

    //Show 12.5K instead of 12,500
    [SerializeField] private bool abbreviate = true;

    //Show max amount for resources with a cap
    [SerializeField] private bool showCap = false;

    private void OnEnable()
    {
        ResourceManager manager = ResourceManager.Instance;
        if (manager == null) return;

        manager.ResourceChanged += OnResourceChanged;

        Refresh(manager.GetAmount(resourceType));
    }

    private void OnDisable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.ResourceChanged -= OnResourceChanged;
    }

    private void OnResourceChanged(ResourceType type, long newAmount, long delta)
    {
        if (type == resourceType) Refresh(newAmount);
    }

    private void Refresh(long amount)
    {
        if (amountText == null) return;

        string text = abbreviate ? Abbreviate(amount) : amount.ToString("N0");

        GameResource resource = ResourceManager.Instance != null ? ResourceManager.Instance.GetResource(resourceType) : null;
        if (showCap && resource != null && resource.HasCap)
            text += "/" + (abbreviate ? Abbreviate(resource.MaxAmount) : resource.MaxAmount.ToString("N0"));

        amountText.text = text;
    }

    public static string Abbreviate(long value)
    {
        if (value < 1000) return value.ToString();
        if (value < 1000000) return Truncate(value, 1000) + "K";
        if (value < 1000000000) return Truncate(value, 1000000) + "M";
        return Truncate(value, 1000000000) + "B";
    }

    private static string Truncate(long value, long unit)
    {
        long tenths = value / (unit / 10);
        return (tenths / 10d).ToString("0.#");
    }
}
