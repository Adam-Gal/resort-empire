using UnityEngine;

// HUD parts that depend on a wave running. WaveStatusUI calls SetWaveActive.
public class WaveHud : MonoBehaviour
{
    [SerializeField] private GameObject services;
    [SerializeField] private GameObject idlePanel;

    //Fades the service cards out while something else needs the bottom of the screen
    [SerializeField] private CanvasGroup servicesGroup;

    public bool IsWaveActive { get; private set; }

    private void Awake()
    {
        Apply();
    }

    public void SetWaveActive(bool active)
    {
        IsWaveActive = active;
        Apply();
    }

    // The build menu covers the bottom of the screen, the cards come back when it closes.
    public void SetCovered(bool covered)
    {
        servicesGroup.alpha = covered ? 0f : 1f;
        servicesGroup.blocksRaycasts = !covered;
    }

    private void Apply()
    {
        services.SetActive(IsWaveActive);
        idlePanel.SetActive(!IsWaveActive);
    }

    [ContextMenu("Debug/Toggle Wave")]
    private void DebugToggleWave() => SetWaveActive(!IsWaveActive);
}
