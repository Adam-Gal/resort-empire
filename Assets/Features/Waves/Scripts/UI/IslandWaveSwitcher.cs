using System.Collections.Generic;
using ResortEmpire.Presentation;
using UnityEngine;

// One chip per open island with its wave state, tapping one moves the view there.
// Hidden while only the starting island is open.
public class IslandWaveSwitcher : MonoBehaviour
{
    [SerializeField] private IslandCameraController cameraController;
    [SerializeField] private GameObject root;
    [SerializeField] private IslandWaveChip chipTemplate;
    [SerializeField] private Color currentColor = new Color32(0x27, 0x8E, 0x89, 0xFF);
    [SerializeField] private Color otherColor = new Color32(0x00, 0x39, 0x47, 0xFF);

    private readonly List<IslandWaveChip> chips = new List<IslandWaveChip>();

    private void Awake()
    {
        chipTemplate.gameObject.SetActive(false);
    }

    public void Refresh(string currentIslandId)
    {
        List<string> islands = IslandProgress.Instance.GetOpenIslandIds();
        root.SetActive(islands.Count > 1);
        if (islands.Count <= 1) return;

        for (int i = 0; i < islands.Count; i++)
        {
            if (i == chips.Count) chips.Add(Instantiate(chipTemplate, chipTemplate.transform.parent));

            string islandId = islands[i];
            WaveSession session = WaveManager.Instance.GetSession(islandId);
            chips[i].Show(
                IslandProgress.Instance.GetIslandName(islandId),
                session == null ? "Voľno" : WaveStatusUI.StateLabel(session),
                islandId == currentIslandId ? currentColor : otherColor,
                () => cameraController.FocusOn(IslandProgress.Instance.GetIslandCenter(islandId)));
        }
        for (int i = islands.Count; i < chips.Count; i++) chips[i].gameObject.SetActive(false);
    }
}
