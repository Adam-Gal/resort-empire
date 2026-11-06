using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ResortEmpire.Presentation;
using UnityEngine;
using UnityEngine.Tilemaps;

// Islands beyond the starting one are locked and drawn darker. Enough reputation offers
// to open one in a dialog, the player may refuse and open it later by tapping the island.
// A ruined bridge opens the island behind it once repaired for coins. Locked islands cannot be built on.
[DefaultExecutionOrder(-80)]
public class IslandProgress : MonoBehaviour
{
    [Serializable]
    public class Island
    {
        public string id;
        public string displayName;

        //Can be opened at this much reputation, 0 means only a bridge opens it
        public long requiredReputation;
        public Collider2D area;

        //Swapped for the repaired prefab when the island opens
        public GameObject ruinedHarbor;
        public GameObject repairedHarbor;

        //Lock with the requirement, shown over the island while it is locked
        public IslandLockMarker lockMarker;
    }

    [Serializable]
    public class Bridge
    {
        public string id;
        public string displayName = "Most";

        //Can be repaired only when this island is open, repairing it opens the other one
        public string fromIsland;
        public string toIsland;
        public ResourceCost[] repairCost;
        public GameObject ruined;
        public GameObject repaired;
    }

    public static IslandProgress Instance { get; private set; }

    [SerializeField] private Island[] islands;
    [SerializeField] private Bridge[] bridges;
    [SerializeField] private TransientNotice notice;
    [SerializeField] private IslandUnlockPopup unlockPopup;

    //Says what is missing when the player cannot afford a repair
    [SerializeField] private MessagePopup messagePopup;

    [Header("Locked look")]
    //Tiles and sprites of a locked island are multiplied by this
    [SerializeField] private Color lockedTint = new Color(.45f, .45f, .5f, 1f);
    [SerializeField] private Tilemap[] islandTilemaps;

    //Sprites under these roots are never darkened, like clouds over the sea
    [SerializeField] private Transform[] shadeIgnore;

    //Edge tiles stick out of the island area by up to this much
    [SerializeField, Min(0f)] private float tileTolerance = .75f;
    [SerializeField, Min(0f)] private float unlockFadeTime = .8f;

    //Smoke over a harbor or bridge when it is repaired
    [SerializeField] private RemovalEffect repairEffect;
    [SerializeField] private string saveFileName = "islands_save.json";

    // An island opened or a bridge was repaired.
    public event Action Changed;

    private IslandSaveData saveData;
    private readonly Dictionary<GameObject, Bridge> bridgeByArt = new Dictionary<GameObject, Bridge>();

    // What is drawn darker while an island is locked.
    private class Shade
    {
        public readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
        public readonly List<Color> spriteColors = new List<Color>();
        public readonly List<Tilemap> tilemaps = new List<Tilemap>();
        public readonly List<Vector3Int> cells = new List<Vector3Int>();
    }

    private readonly Dictionary<string, Shade> shades = new Dictionary<string, Shade>();
    private readonly Queue<Island> pendingOffers = new Queue<Island>();

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    private string TempSavePath => SavePath + ".tmp";

    private void Awake()
    {
        Instance = this;
        saveData = ReadSaveFile() ?? new IslandSaveData();

        foreach (Island island in islands)
        {
            if (saveData.unlockedIslands.Contains(island.id)) SwapArt(ref island.ruinedHarbor, island.repairedHarbor, false);
        }
        foreach (Bridge bridge in bridges)
        {
            if (saveData.repairedBridges.Contains(bridge.id)) SwapArt(ref bridge.ruined, bridge.repaired, false);
            else if (bridge.ruined != null) bridgeByArt[bridge.ruined] = bridge;
        }
        RefreshMarkers(false);
    }

    private IEnumerator Start()
    {
        ResourceManager.Instance.ResourceChanged += OnResourceChanged;

        // Landmarks are placed in BuildingManager.Start, wait so they get darkened too.
        yield return null;
        BuildShades();

        // Reputation may already be high enough from an older save.
        CheckReputation();
    }

    private void OnDestroy()
    {
        if (ResourceManager.Instance != null) ResourceManager.Instance.ResourceChanged -= OnResourceChanged;
        if (Instance == this) Instance = null;
    }

    public bool IsUnlocked(string islandId) => saveData.unlockedIslands.Contains(islandId);

    // Locked island under a world point, null on the starting island, open islands and the sea.
    public Island GetLockedIsland(Vector3 world)
    {
        foreach (Island island in islands)
        {
            if (island.area != null && !IsUnlocked(island.id) && island.area.OverlapPoint(world)) return island;
        }
        return null;
    }

    public bool IsLocked(Vector3 world) => GetLockedIsland(world) != null;

    // Reputation is high enough, the player only has to confirm.
    public bool CanUnlock(Island island) =>
        island.requiredReputation > 0 && !IsUnlocked(island.id) &&
        ResourceManager.Instance.GetAmount(ResourceType.Reputation) >= island.requiredReputation;

    // A tap on a locked island offers to open it, or says what it still needs.
    public void OnLockedIslandTapped(Island island)
    {
        if (CanUnlock(island)) Offer(island);
        else ShowLockedDialog(island);
    }

    // Big dialog with what the island still needs.
    public void ShowLockedDialog(Island island)
    {
        if (messagePopup == null)
        {
            ShowLockedMessage(island);
            return;
        }

        string text;
        if (CanUnlock(island))
            text = $"{island.displayName} už môžeš odomknúť. Ťukni naň.";
        else if (island.requiredReputation > 0)
        {
            long have = ResourceManager.Instance.GetAmount(ResourceType.Reputation);
            text = $"Na odomknutie potrebuješ reputáciu {island.requiredReputation:N0}.\nMáš {have:N0}, chýba ti {island.requiredReputation - have:N0}.";
        }
        else
        {
            Bridge bridge = Array.Find(bridges, b => b.toIsland == island.id);
            Island from = bridge != null ? Array.Find(islands, i => i.id == bridge.fromIsland) : null;
            text = from != null
                ? $"Vedie k nemu zničený {bridge.displayName.ToLowerInvariant()} z ostrova {from.displayName.Replace("Ostrov ", "")}. Oprav ho a ostrov sa odomkne."
                : "Ostrov sa zatiaľ nedá odomknúť.";
        }
        messagePopup.Show($"{island.displayName} je uzamknutý", text);
    }

    // The bridge stands on a locked island, so that island has to open first.
    public void ShowBridgeProblem(Bridge bridge)
    {
        Island from = Array.Find(islands, i => i.id == bridge.fromIsland);
        if (from != null && !IsUnlocked(from.id)) OnLockedIslandTapped(from);
    }

    public string GetLockedMessage(Island island)
    {
        if (CanUnlock(island)) return $"{island.displayName} môžeš odomknúť. Ťukni naň.";
        if (island.requiredReputation > 0)
        {
            long have = ResourceManager.Instance.GetAmount(ResourceType.Reputation);
            return $"{island.displayName} je uzamknutý. Vyžaduje reputáciu {island.requiredReputation:N0} (máš {have:N0}).";
        }

        Bridge bridge = Array.Find(bridges, b => b.toIsland == island.id);
        return bridge != null
            ? $"{island.displayName} je uzamknutý. Oprav {bridge.displayName.ToLowerInvariant()}, ktorý k nemu vedie."
            : $"{island.displayName} je uzamknutý.";
    }

    public void ShowLockedMessage(Island island) => ShowMessage(GetLockedMessage(island));

    public void ShowMessage(string text)
    {
        if (notice != null) notice.Show(text);
    }

    // A ruined bridge whose art is under the world point.
    public Bridge PickBridge(Vector3 world)
    {
        foreach (KeyValuePair<GameObject, Bridge> pair in bridgeByArt)
        {
            if (pair.Key == null) continue;
            SpriteRenderer art = pair.Key.GetComponent<SpriteRenderer>();
            if (art != null && art.bounds.Contains(new Vector3(world.x, world.y, art.bounds.center.z))) return pair.Value;
        }
        return null;
    }

    // Why the bridge cannot be repaired yet, null when it can.
    public string GetBridgeProblem(Bridge bridge)
    {
        Island from = Array.Find(islands, i => i.id == bridge.fromIsland);
        if (from != null && !IsUnlocked(from.id)) return GetLockedMessage(from);
        return null;
    }

    public bool CanAffordRepair(Bridge bridge) => ResourceManager.Instance.CanAfford(bridge.repairCost);

    // Dialog listing what the repair still needs.
    public void ShowMissingForRepair(Bridge bridge)
    {
        var missing = new System.Text.StringBuilder();
        foreach (ResourceCost cost in bridge.repairCost)
        {
            long have = ResourceManager.Instance.GetAmount(cost.type);
            if (have >= cost.amount) continue;
            if (missing.Length > 0) missing.Append('\n');
            missing.Append($"{ResourceName(cost.type)}: máš {have:N0} z {cost.amount:N0}, chýba {cost.amount - have:N0}.");
        }

        string text = $"Na opravu mosta nemáš dosť prostriedkov.\n{missing}";
        if (messagePopup != null) messagePopup.Show("Nedostatok prostriedkov", text);
        else ShowMessage(text);
    }

    private static string ResourceName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Money: return "Mince";
            case ResourceType.Energy: return "Energia";
            case ResourceType.Reputation: return "Reputácia";
            default: return type.ToString();
        }
    }

    // Each locked island shows a lock with what opens it.
    private void RefreshMarkers(bool fade)
    {
        foreach (Island island in islands)
        {
            if (island.lockMarker == null) continue;
            if (IsUnlocked(island.id))
            {
                island.lockMarker.Hide(fade);
                continue;
            }
            island.lockMarker.Show(island.requiredReputation > 0
                ? $"<size=70%>Reputácia</size>\n{island.requiredReputation:N0}"
                : "<size=70%>Oprav</size>\nmost");
        }
    }

    public bool TryRepair(Bridge bridge)
    {
        if (saveData.repairedBridges.Contains(bridge.id) || GetBridgeProblem(bridge) != null) return false;
        if (!ResourceManager.Instance.TrySpend(bridge.repairCost)) return false;

        bridgeByArt.Remove(bridge.ruined);
        SwapArt(ref bridge.ruined, bridge.repaired, true);
        saveData.repairedBridges.Add(bridge.id);

        Island target = Array.Find(islands, i => i.id == bridge.toIsland);
        if (target != null) Unlock(target, true);
        else Save();

        Changed?.Invoke();
        return true;
    }

    private void OnResourceChanged(ResourceType type, long amount, long delta)
    {
        if (type == ResourceType.Reputation) CheckReputation();
    }

    // Each island is offered on its own once, when its requirement is first met.
    private void CheckReputation()
    {
        bool any = false;
        foreach (Island island in islands)
        {
            if (!CanUnlock(island) || saveData.offeredIslands.Contains(island.id)) continue;
            saveData.offeredIslands.Add(island.id);
            pendingOffers.Enqueue(island);
            any = true;
        }
        if (!any) return;
        Save();
        ShowNextOffer();
    }

    private void Offer(Island island)
    {
        if (!pendingOffers.Contains(island)) pendingOffers.Enqueue(island);
        ShowNextOffer();
    }

    // Several islands may become ready at once, they are offered one after another.
    private void ShowNextOffer()
    {
        if (unlockPopup != null && unlockPopup.IsOpen) return;

        while (pendingOffers.Count > 0)
        {
            Island island = pendingOffers.Dequeue();
            if (!CanUnlock(island)) continue;

            if (unlockPopup == null)
            {
                Unlock(island, true);
                Changed?.Invoke();
                continue;
            }

            unlockPopup.Show(island.displayName, island.requiredReputation, () =>
            {
                if (!CanUnlock(island)) return;
                Unlock(island, true);
                Changed?.Invoke();
            }, ShowNextOffer);
            return;
        }
    }

    private void Unlock(Island island, bool announce)
    {
        saveData.unlockedIslands.Add(island.id);
        SwapArt(ref island.ruinedHarbor, island.repairedHarbor, announce);
        Save();
        RefreshShades(announce);
        RefreshMarkers(announce);
        if (announce) ShowMessage($"{island.displayName} je odomknutý!");
    }

    // The island the point belongs to, edge tiles reach a little past the area.
    // Where two islands are close, a tile goes to the nearer one.
    private Island FindIsland(Vector2 point, float tolerance)
    {
        Island nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Island island in islands)
        {
            if (island.area == null) continue;
            Bounds bounds = island.area.bounds;
            bounds.Expand(tolerance * 2f);
            if (!bounds.Contains(new Vector3(point.x, point.y, bounds.center.z))) continue;
            if (island.area.OverlapPoint(point)) return island;
            if (tolerance <= 0f) continue;

            float distance = Vector2.Distance(island.area.ClosestPoint(point), point);
            if (distance <= tolerance && distance < nearestDistance)
            {
                nearest = island;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    private Shade ShadeOf(string islandId)
    {
        if (!shades.TryGetValue(islandId, out Shade shade)) shades[islandId] = shade = new Shade();
        return shade;
    }

    // Darkens every tile and sprite on a locked island. Runs once, unlocking fades them back.
    private void BuildShades()
    {
        var handled = new HashSet<SpriteRenderer>();

        // Harbors and bridges may stand just off the island, so they are matched by hand.
        // A bridge stays dark until the island it is repaired from opens.
        foreach (Island island in islands)
        {
            if (!IsUnlocked(island.id) && island.ruinedHarbor != null) AddSprites(ShadeOf(island.id), island.ruinedHarbor, handled);
        }
        foreach (Bridge bridge in bridges)
        {
            if (bridge.ruined == null) continue;
            if (!IsUnlocked(bridge.fromIsland)) AddSprites(ShadeOf(bridge.fromIsland), bridge.ruined, handled);
            else handled.UnionWith(bridge.ruined.GetComponentsInChildren<SpriteRenderer>(true));
        }

        foreach (SpriteRenderer sprite in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (handled.Contains(sprite) || IsIgnored(sprite.transform)) continue;
            Island island = FindIsland(sprite.transform.position, 0f);
            if (island == null || IsUnlocked(island.id)) continue;
            Shade shade = ShadeOf(island.id);
            shade.sprites.Add(sprite);
            shade.spriteColors.Add(sprite.color);
            sprite.color = Tinted(sprite.color);
        }

        foreach (Tilemap tilemap in islandTilemaps)
        {
            if (tilemap == null) continue;
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell)) continue;
                Island island = FindIsland(tilemap.GetCellCenterWorld(cell), tileTolerance);
                if (island == null || IsUnlocked(island.id)) continue;
                Shade shade = ShadeOf(island.id);
                shade.tilemaps.Add(tilemap);
                shade.cells.Add(cell);
                tilemap.SetTileFlags(cell, TileFlags.None);
                tilemap.SetColor(cell, lockedTint);
            }
        }
    }

    private void AddSprites(Shade shade, GameObject root, HashSet<SpriteRenderer> handled)
    {
        foreach (SpriteRenderer sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            handled.Add(sprite);
            shade.sprites.Add(sprite);
            shade.spriteColors.Add(sprite.color);
            sprite.color = Tinted(sprite.color);
        }
    }

    private bool IsIgnored(Transform t)
    {
        if (shadeIgnore == null) return false;
        foreach (Transform root in shadeIgnore)
        {
            if (root != null && t.IsChildOf(root)) return true;
        }
        return false;
    }

    private Color Tinted(Color c) => new Color(c.r * lockedTint.r, c.g * lockedTint.g, c.b * lockedTint.b, c.a);

    // Lights up whatever belongs to islands that are open now.
    private void RefreshShades(bool fade)
    {
        var opened = new List<string>();
        foreach (KeyValuePair<string, Shade> pair in shades)
        {
            if (IsUnlocked(pair.Key)) opened.Add(pair.Key);
        }
        foreach (string id in opened)
        {
            Shade shade = shades[id];
            shades.Remove(id);
            if (fade && isActiveAndEnabled && unlockFadeTime > 0f) StartCoroutine(FadeIn(shade));
            else SetShade(shade, 1f);
        }
    }

    private IEnumerator FadeIn(Shade shade)
    {
        for (float t = 0f; t < unlockFadeTime; t += Time.unscaledDeltaTime)
        {
            SetShade(shade, t / unlockFadeTime);
            yield return null;
        }
        SetShade(shade, 1f);
    }

    // 0 is the locked look, 1 the normal one.
    private void SetShade(Shade shade, float light)
    {
        for (int i = 0; i < shade.sprites.Count; i++)
        {
            SpriteRenderer sprite = shade.sprites[i];
            if (sprite != null) sprite.color = Color.Lerp(Tinted(shade.spriteColors[i]), shade.spriteColors[i], light);
        }
        Color tile = Color.Lerp(lockedTint, Color.white, light);
        for (int i = 0; i < shade.cells.Count; i++)
        {
            if (shade.tilemaps[i] != null) shade.tilemaps[i].SetColor(shade.cells[i], tile);
        }
    }

    // The repaired art takes the ruined one's place, turned and mirrored the same way.
    // A repaired prefab with its art on a child is mirrored by its root, so the art's offset mirrors too.
    private void SwapArt(ref GameObject ruined, GameObject repairedPrefab, bool playEffect)
    {
        if (ruined == null) return;

        // Without a repaired prefab something else builds in its place, like a harbor site.
        if (repairedPrefab == null)
        {
            if (playEffect && repairEffect != null)
                Instantiate(repairEffect).Play(ruined.transform.position, new Vector2Int(3, 2), null);
            Destroy(ruined);
            ruined = null;
            return;
        }

        GameObject repaired = Instantiate(repairedPrefab, ruined.transform.position, ruined.transform.rotation, ruined.transform.parent);
        repaired.name = repairedPrefab.name;
        SpriteRenderer from = ruined.GetComponent<SpriteRenderer>();
        SpriteRenderer to = repaired.GetComponent<SpriteRenderer>();
        bool mirrored = (from != null && from.flipX) != (ruined.transform.localScale.x < 0f);
        if (to != null)
            to.flipX = mirrored;
        else if (mirrored)
        {
            Vector3 scale = repaired.transform.localScale;
            repaired.transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
        }

        if (playEffect && repairEffect != null)
            Instantiate(repairEffect).Play(ruined.transform.position, new Vector2Int(3, 2), null);

        Destroy(ruined);
        ruined = null;
    }

    private IslandSaveData ReadSaveFile()
    {
        string path = File.Exists(SavePath) ? SavePath : (File.Exists(TempSavePath) ? TempSavePath : null);
        if (path == null) return null;

        try
        {
            return JsonUtility.FromJson<IslandSaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[IslandProgress] Save file is corrupt, starting fresh: {e.Message}");
            return null;
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(TempSavePath, JsonUtility.ToJson(saveData));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(TempSavePath, SavePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[IslandProgress] Failed to save islands: {e.Message}");
        }
    }

    [ContextMenu("Debug/Delete Save File")]
    private void DebugDeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(TempSavePath)) File.Delete(TempSavePath);
        Debug.Log($"[IslandProgress] Deleted save at {SavePath}. Restart play mode to lock the islands again.");
    }
}
