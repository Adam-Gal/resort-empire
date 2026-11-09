using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StarterGiftPopup : MonoBehaviour
{
    [Serializable]
    public class RewardTextSlot
    {
        public ResourceType resourceType;
        public TMP_Text text;

        //Whole reward card, hidden when the gift has none of this resource
        public GameObject container;
    }

    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;

    //One entry per resource
    [SerializeField] private RewardTextSlot[] rewardTexts;

    [SerializeField] private Button claimButton;

    //Wait so the island would be visible before the popup 
    [SerializeField] private float showDelay = 0.75f;

    private void Awake()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
        if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
    }

    private IEnumerator Start()
    {
        ResourceManager manager = ResourceManager.Instance;
        if (manager == null || !manager.HasPendingStarterGift) yield break;

        if (showDelay > 0f) yield return new WaitForSeconds(showDelay);

        Show(manager);
    }

    private void Show(ResourceManager manager)
    {
        StarterGiftData gift = manager.StarterGift;

        if (titleText != null) titleText.text = gift.title;
        if (messageText != null) messageText.text = gift.message;
        FillRewardTexts(manager, gift);

        if (claimButton != null) claimButton.interactable = true;
        if (popupRoot != null) popupRoot.SetActive(true);
    }

    private void OnClaimClicked()
    {
        // Guard against double taps.
        if (claimButton != null) claimButton.interactable = false;

        ResourceManager manager = ResourceManager.Instance;
        if (manager != null) manager.ClaimStarterGift();

        if (popupRoot != null) popupRoot.SetActive(false);
    }

    private void FillRewardTexts(ResourceManager manager, StarterGiftData gift)
    {
        if (rewardTexts == null) return;

        foreach (RewardTextSlot slot in rewardTexts)
        {
            if (slot == null || slot.text == null) continue;

            long amount = GetRewardAmount(gift, slot.resourceType);

            GameObject shown = slot.container != null ? slot.container : slot.text.gameObject;
            shown.SetActive(amount > 0);
            if (amount <= 0) continue;

            string text = "+" + amount.ToString("N0");
            slot.text.text = text;
        }
    }

    //Total of this resource in the gift (sums it if listed more than once).
    private static long GetRewardAmount(StarterGiftData gift, ResourceType type)
    {
        long total = 0;
        foreach (ResourceReward reward in gift.rewards)
        {
            if (reward.TryGetType(out ResourceType rewardType) && rewardType == type)
                total += reward.amount;
        }
        return total;
    }
}