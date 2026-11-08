using System;
using System.Collections.Generic;
using UnityEngine;

// Buying the next level of a building. The level arrives after its upgradeDuration,
// any number of buildings can be upgrading at once. BuildingManager swaps the art and saves it.
public class BuildingUpgrade : MonoBehaviour
{
    public static BuildingUpgrade Instance { get; private set; }

    // The one clock upgrades run on. Real time, so upgrades keep going while the game is closed.
    // Swap for server time here if players turning their clock forward becomes a problem.
    public static DateTime Now => DateTime.UtcNow;

    //Smoke over the footprint when a level is finished
    [SerializeField] private RemovalEffect upgradeEffect;

    // Paid for, the timer is running.
    public event Action<PlacedBuilding> UpgradeStarted;

    // The new level is standing.
    public event Action<PlacedBuilding> UpgradeFinished;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool CanUpgrade(PlacedBuilding building)
    {
        return !building.IsUpgrading && building.Definition.HasLevel(building.Level + 1);
    }

    public bool TryUpgrade(PlacedBuilding building)
    {
        if (!CanUpgrade(building)) return false;

        BuildingLevel next = building.Definition.levels[building.Level + 1];
        if (!ResourceManager.Instance.TrySpend(next.upgradeCost)) return false;

        if (next.upgradeDuration <= 0f)
        {
            Finish(building);
            return true;
        }

        BuildingManager.Instance.StartUpgrade(building, TimeSpan.FromSeconds(next.upgradeDuration));
        UpgradeStarted?.Invoke(building);
        return true;
    }

    // Ends a running upgrade right away, for a speed-up the player paid for or for testing.
    public void FinishNow(PlacedBuilding building)
    {
        if (building != null && building.IsUpgrading) Finish(building);
    }

    private void Update()
    {
        BuildingManager manager = BuildingManager.Instance;
        if (manager == null) return;

        long now = Now.Ticks;
        IReadOnlyList<PlacedBuilding> placed = manager.Placed;
        for (int i = placed.Count - 1; i >= 0; i--)
        {
            PlacedBuilding building = placed[i];

            // A building being moved is hidden and its ghost shows the old art, it finishes once put down.
            if (!building.IsUpgrading || !building.gameObject.activeSelf) continue;
            if (now >= building.UpgradeEndTicks) Finish(building);
        }
    }

    private void Finish(PlacedBuilding building)
    {
        BuildingManager.Instance.SetLevel(building, building.Level + 1);
        PlayEffect(building);
        UpgradeFinished?.Invoke(building);
    }

    private void PlayEffect(PlacedBuilding building)
    {
        if (upgradeEffect == null) return;

        Vector2Int size = building.Definition.size;
        Vector3 center = BuildingManager.Instance.Grid.FootprintCenter(building.Origin, size);
        Instantiate(upgradeEffect).Play(center, size, null);
    }

    // "2h 05m", "4m 30s", "12s" for timer labels.
    public static string Format(TimeSpan time)
    {
        // Rounded up, so a timer never shows 0s while it is still running.
        time = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(time.TotalSeconds)));
        if (time.TotalDays >= 1) return $"{(int)time.TotalDays}d {time.Hours:00}h";
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours}h {time.Minutes:00}m";
        if (time.TotalMinutes >= 1) return $"{(int)time.TotalMinutes}m {time.Seconds:00}s";
        return $"{(int)time.TotalSeconds}s";
    }
}