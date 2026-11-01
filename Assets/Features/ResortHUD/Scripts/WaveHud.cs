using UnityEngine;

// HUD parts that depend on a wave running. The wave system calls SetWaveActive.
public class WaveHud : MonoBehaviour
{
    [SerializeField] private GameObject services;
    [SerializeField] private GameObject idlePanel;

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

    private void Apply()
    {
        services.SetActive(IsWaveActive);
        idlePanel.SetActive(!IsWaveActive);
    }

    [ContextMenu("Debug/Toggle Wave")]
    private void DebugToggleWave() => SetWaveActive(!IsWaveActive);
}
