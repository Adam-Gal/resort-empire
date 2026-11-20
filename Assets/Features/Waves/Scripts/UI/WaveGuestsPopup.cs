using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small popup under the wave bar: how many guests of the wave left happy, are staying,
// wait, left unhappy or are still to come, and how they rate the stay. Follows the guests while open.
public class WaveGuestsPopup : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;

    [Header("Summary")]
    //Happy guests large, then out of how many
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private Color happyColor = new Color32(0x08, 0x7F, 0x82, 0xFF);
    [SerializeField, Min(1f)] private float happySize = 46f;

    //Average stars of the guests who left so far
    [SerializeField] private TMP_Text ratingText;

    //How many of the arrived guests had each basic need met, optional
    [SerializeField] private TMP_Text needsText;

    //Parts of one bar, each spans its share of all the wave's guests
    [SerializeField] private RectTransform happyPart;
    [SerializeField] private RectTransform stayingPart;
    [SerializeField] private RectTransform waitingPart;
    [SerializeField] private RectTransform unhappyPart;

    [Header("Rows")]
    [SerializeField] private TMP_Text happyValue;
    [SerializeField] private TMP_Text stayingValue;
    [SerializeField] private TMP_Text waitingValue;
    [SerializeField] private TMP_Text unhappyValue;
    [SerializeField] private TMP_Text comingValue;

    //Guests change all the time, the popup follows them this often
    [SerializeField, Min(.1f)] private float refreshSeconds = .25f;

    private GuestSimulation guests;
    private float sinceRefresh;

    public bool IsOpen => root.activeSelf;

    private void Awake()
    {
        closeButton.onClick.AddListener(Hide);
        root.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;
        sinceRefresh += Time.unscaledDeltaTime;
        if (sinceRefresh >= refreshSeconds) Refresh();
    }

    public void Toggle(GuestSimulation simulation)
    {
        if (IsOpen) Hide();
        else Show(simulation);
    }

    public void Show(GuestSimulation simulation)
    {
        root.SetActive(true);
        SetGuests(simulation);
    }

    public void Hide() => root.SetActive(false);

    // Swaps the guests shown without opening or closing the popup.
    public void SetGuests(GuestSimulation simulation)
    {
        guests = simulation;
        if (IsOpen) Refresh();
    }

    private void Refresh()
    {
        sinceRefresh = 0f;
        if (guests == null)
        {
            // The wave has just started and its guests are not made yet.
            ShowCounts(0, 0, 0, 0, 0, 0);
            ShowRating(0, 0f);
            ShowNeeds(null);
            return;
        }

        int waiting = guests.Waiting;
        ShowCounts(guests.TotalGuests, guests.HappyGuests, guests.InResort - waiting, waiting, guests.UnhappyGuests, guests.StillToCome);
        ShowRating(guests.RatedGuests, guests.AverageRating);
        ShowNeeds(guests);
    }

    // "Ubytovanie 5/8 · Jedlo 4/8 · Nápoje 3/8", out of the guests who arrived so far.
    private void ShowNeeds(GuestSimulation simulation)
    {
        if (needsText == null) return;

        int arrived = simulation != null ? simulation.Arrived : 0;
        var parts = new System.Collections.Generic.List<string>();
        foreach (GuestNeeds need in GuestNeedsInfo.Basic)
        {
            int met = simulation != null ? simulation.GetMetCount(need) : 0;
            parts.Add($"{GuestNeedsInfo.DisplayName(need)} {met}/{arrived}");
        }
        needsText.text = string.Join("  ·  ", parts);
    }

    // Written with a decimal comma, a dash until the first guest has left.
    private void ShowRating(int rated, float average)
    {
        string stars = rated > 0
            ? average.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')
            : "–";
        ratingText.text = $"Hodnotenie {stars} / 5";
    }

    private void ShowCounts(int total, int happy, int staying, int waiting, int unhappy, int coming)
    {
        summaryText.text = $"<size={happySize}><color=#{ColorUtility.ToHtmlStringRGB(happyColor)}>{happy}</color></size>  / {total} spokojných";

        happyValue.text = happy.ToString();
        stayingValue.text = staying.ToString();
        waitingValue.text = waiting.ToString();
        unhappyValue.text = unhappy.ToString();
        comingValue.text = coming.ToString();

        float share = total > 0 ? 1f / total : 0f;
        float start = Place(happyPart, 0f, happy * share);
        start = Place(stayingPart, start, staying * share);
        start = Place(waitingPart, start, waiting * share);
        Place(unhappyPart, start, unhappy * share);
    }

    // Stretches the part over its share of the bar, returns where the next one starts.
    private static float Place(RectTransform part, float start, float width)
    {
        float end = Mathf.Clamp01(start + width);
        part.anchorMin = new Vector2(start, 0f);
        part.anchorMax = new Vector2(end, 1f);
        part.offsetMin = Vector2.zero;
        part.offsetMax = Vector2.zero;
        return end;
    }
}
