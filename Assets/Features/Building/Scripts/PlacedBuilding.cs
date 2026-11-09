using System;
using UnityEngine;

// A building standing on the island. BuildingManager creates it.
public class PlacedBuilding : MonoBehaviour
{
    public BuildingDefinition Definition { get; private set; }
    public Vector2Int Origin { get; private set; }

    // The level it works at. While an upgrade is being built it stays the old one.
    public int Level { get; private set; }

    //UTC ticks of the running upgrade, both 0 when there is none
    public long UpgradeStartTicks { get; private set; }
    public long UpgradeEndTicks { get; private set; }

    public bool IsUpgrading => UpgradeEndTicks > 0;

    public int Employees { get; private set; }
    public int MaxEmployees => Definition.MaxEmployeesAt(Level);
    public int MinEmployees => MaxEmployees > 0 ? 1 : 0;
    public TimeSpan UpgradeRemaining =>
        IsUpgrading
            ? TimeSpan.FromTicks(Math.Max(0L, UpgradeEndTicks - BuildingUpgrade.Now.Ticks))
            : TimeSpan.Zero;

    // 0 when the upgrade was just bought, 1 when it is done.
    public float UpgradeProgress
    {
        get
        {
            if (!IsUpgrading)
                return 0f;
            long total = UpgradeEndTicks - UpgradeStartTicks;
            if (total <= 0)
                return 1f;
            return Mathf.Clamp01(1f - (float)UpgradeRemaining.Ticks / total);
        }
    }

    public void Initialize(BuildingDefinition definition, Vector2Int origin, int level)
    {
        Definition = definition;
        Origin = origin;
        Level = level;
        Employees = MinEmployees;
    }

    public void SetOrigin(Vector2Int origin) => Origin = origin;

    public void SetLevel(int level) => Level = level;

    public void SetUpgrade(long startTicks, long endTicks)
    {
        UpgradeStartTicks = startTicks;
        UpgradeEndTicks = endTicks;
    }

    public void ClearUpgrade() => SetUpgrade(0, 0);

    public void SetEmployees(int employees) => Employees = Mathf.Clamp(employees, MinEmployees, MaxEmployees);
}
