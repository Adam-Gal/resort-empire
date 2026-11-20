using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Service cards of the island in view during its wave.
// Basic services are always listed, the others once the island has such a building.
// With many services the cards get narrower so the row still fits the screen.
public class ServiceCardsUI : MonoBehaviour
{
    [Serializable]
    public class Service
    {
        public BuildingCategory category;
        public string displayName;
        public Sprite icon;

        //Picture at the top of the building list
        public Sprite header;

        //Guests always ask for it, so the card shows even with nothing built
        public bool alwaysShown;
    }

    [SerializeField] private Service[] services;
    [SerializeField] private ServiceCardView cardTemplate;
    [SerializeField] private ServiceDetailsPopup detailsPopup;

    //Loads change all the time during a wave, the cards follow them this often
    [SerializeField, Min(.1f)] private float refreshSeconds = .5f;

    //Room kept free right of the row, for the build button
    [SerializeField, Min(0f)] private float rightReserve = 180f;

    //Cards never get narrower than this, the row may then run off screen
    [SerializeField, Min(100f)] private float minCardWidth = 250f;

    private readonly List<ServiceCardView> cards = new List<ServiceCardView>();
    private string islandId;
    private float sinceRefresh;
    private float fullCardWidth;

    private void Awake()
    {
        cardTemplate.gameObject.SetActive(false);
        LayoutElement layout = cardTemplate.GetComponent<LayoutElement>();
        fullCardWidth = layout != null ? layout.preferredWidth : ((RectTransform)cardTemplate.transform).rect.width;
    }

    private void Update()
    {
        if (islandId == null) return;
        sinceRefresh += Time.unscaledDeltaTime;
        if (sinceRefresh >= refreshSeconds) Show(islandId);
    }

    // Opens the building list of one service, like tapping its card.
    public void ShowDetails(string island, BuildingCategory category)
    {
        foreach (Service service in services)
        {
            if (service.category != category) continue;
            detailsPopup.Show(island, service.category, service.displayName, service.icon, service.header);
            return;
        }
    }

    public void Show(string island)
    {
        islandId = island;
        sinceRefresh = 0f;
        WaveManager waves = WaveManager.Instance;
        GuestSimulation guests = GuestManager.Instance != null ? GuestManager.Instance.GetSimulation(islandId) : null;

        int used = 0;
        foreach (Service service in services)
        {
            List<PlacedBuilding> buildings = waves.GetBuildings(islandId, service.category);
            if (buildings.Count == 0 && !service.alwaysShown) continue;

            if (used == cards.Count) cards.Add(Instantiate(cardTemplate, cardTemplate.transform.parent));
            int capacity = 0;
            foreach (PlacedBuilding building in buildings) capacity += building.Definition.CapacityAt(building.Level);

            // The card shows the busiest building, guests in line are counted over all of them.
            float load = 0f;
            int queued = 0;
            if (guests != null)
            {
                foreach (PlacedBuilding building in buildings)
                {
                    load = Mathf.Max(load, guests.GetLoad(building));
                    queued += guests.GetQueued(building);
                }
                if (service.category == BuildingCategory.Rooms) queued += guests.WaitingForRoom;
            }

            ServiceState worst = waves.GetWorstState(islandId, buildings);
            bool missing = buildings.Count == 0;
            string detail = missing ? "Chýba budova"
                : worst == ServiceState.Overloaded ? (queued > 0 ? $"Nestíha · v rade {queued}" : "Nestíha")
                : worst == ServiceState.Busy ? $"Ledva stíha · {Mathf.RoundToInt(load * 100f)} %"
                : $"Kapacita {capacity}";

            ServiceCardState state = missing ? ServiceCardState.Missing
                : worst == ServiceState.Overloaded ? ServiceCardState.Problem
                : worst == ServiceState.Busy ? ServiceCardState.Busy
                : ServiceCardState.Normal;

            Service shown = service;
            cards[used++].Show(service.icon, service.displayName, detail, state, load,
                () => detailsPopup.Show(islandId, shown.category, shown.displayName, shown.icon, shown.header));
        }
        for (int i = used; i < cards.Count; i++) cards[i].gameObject.SetActive(false);
        FitCards(used);
    }

    // Shares the width left of the build button among the cards shown, up to their full width.
    private void FitCards(int count)
    {
        if (count == 0) return;

        var row = (RectTransform)cardTemplate.transform.parent;
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 0f;
        float available = ((RectTransform)transform).rect.width - row.anchoredPosition.x - rightReserve - spacing * (count - 1);
        float width = Mathf.Clamp(available / count, minCardWidth, fullCardWidth);

        foreach (ServiceCardView card in cards)
        {
            LayoutElement element = card.GetComponent<LayoutElement>();
            if (element == null || Mathf.Approximately(element.preferredWidth, width)) continue;
            element.preferredWidth = width;
            element.minWidth = width;
        }
    }
}
