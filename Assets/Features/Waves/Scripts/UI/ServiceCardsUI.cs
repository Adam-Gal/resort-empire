using System;
using System.Collections.Generic;
using UnityEngine;

// Service cards of the island in view during its wave.
// Basic services are always listed, the others once the island has such a building.
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

    private readonly List<ServiceCardView> cards = new List<ServiceCardView>();
    private string islandId;
    private float sinceRefresh;

    private void Awake()
    {
        cardTemplate.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (islandId == null) return;
        sinceRefresh += Time.unscaledDeltaTime;
        if (sinceRefresh >= refreshSeconds) Show(islandId);
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
    }
}
