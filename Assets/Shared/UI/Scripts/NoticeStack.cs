using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.Presentation
{
    // Look of a notice, one of the UINotice cards.
    public enum NoticeLevel
    {
        Error,
        Warning
    }

    // Notices on the left of the screen. Each slides in from the left, the newest on top pushes
    // the others down. Only a few show at once, the rest wait until one hides.
    // Errors and warnings share the stack, each with its own card.
    // One pinned warning can sit above them all, for a state the player has to fix, without a close button.
    // Runs only while a notice is on screen, then the component sleeps.
    [DisallowMultipleComponent]
    public sealed class NoticeStack : MonoBehaviour
    {
        //A notice in the scene, copied for each message and kept hidden itself
        [SerializeField] private RectTransform template;

        //The same for warnings, the error card is used when empty
        [SerializeField] private RectTransform warningTemplate;
        [SerializeField, Min(1)] private int maxVisible = 3;
        [SerializeField, Min(.5f)] private float duration = 4f;
        [SerializeField, Min(0f)] private float spacing = 12f;

        [Header("Animation")]
        [SerializeField, Min(.01f)] private float slideTime = .3f;

        //Speed of the others moving to make room, higher is snappier
        [SerializeField, Min(1f)] private float moveSharpness = 14f;

        private sealed class Item
        {
            public RectTransform source;
            public RectTransform rect;
            public GameObject dismiss;
            public CanvasGroup group;
            public TMP_Text message;
            public string text;
            public float shownAt;
            public float hideAt;
            public bool closing;
            public float closingAt;

            //Tapping the card itself, null when it does nothing
            public Action onClick;
        }

        private struct Pending
        {
            public string text;
            public NoticeLevel level;
            public Action onClick;
            public bool untilHidden;
        }

        private readonly List<Item> visible = new List<Item>();

        //The pinned warning first, older ones still sliding out after it
        private readonly List<Item> pinned = new List<Item>();
        private readonly List<Item> pool = new List<Item>();
        private readonly List<Pending> waiting = new List<Pending>();
        private readonly List<Item> finished = new List<Item>();

        private float Height => template.rect.height;
        private float Width => template.rect.width;

        private void Awake()
        {
            template.gameObject.SetActive(false);
            if (warningTemplate != null) warningTemplate.gameObject.SetActive(false);
            enabled = false;
        }

        public void Show(string text) => Show(text, NoticeLevel.Error);

        // Showing the same message again only keeps it longer, so repeated calls do not fill the screen.
        // untilHidden keeps it on screen until Hide or the player closes it, for a state that lasts.
        public void Show(string text, NoticeLevel level, Action onClick = null, bool untilHidden = false)
        {
            foreach (Item item in visible)
            {
                if (item.closing || item.text != text) continue;
                item.hideAt = untilHidden ? float.PositiveInfinity : Time.unscaledTime + duration;
                item.onClick = onClick;
                return;
            }
            if (waiting.Exists(p => p.text == text)) return;

            var pending = new Pending { text = text, level = level, onClick = onClick, untilHidden = untilHidden };
            if (OpenCount() < maxVisible) Open(pending);
            else waiting.Add(pending);
        }

        // Takes the message away before its time, for something that is no longer true.
        public void Hide(string text)
        {
            waiting.RemoveAll(p => p.text == text);
            foreach (Item item in visible)
                if (item.text == text) Close(item);
        }

        // Keeps one warning on top of the stack until it is cleared with null. The player cannot close it.
        public void SetPinned(string text)
        {
            Item current = pinned.Count > 0 && !pinned[0].closing ? pinned[0] : null;
            if (text == null)
            {
                if (current != null) Close(current);
                return;
            }
            if (current != null)
            {
                current.text = text;
                current.message.text = text;
                return;
            }

            Item item = Take(TemplateFor(NoticeLevel.Warning));
            if (item.dismiss != null) item.dismiss.SetActive(false);
            Prepare(item, text, null, true);
            pinned.Insert(0, item);
            enabled = true;
        }

        private int OpenCount()
        {
            int count = 0;
            foreach (Item item in visible)
                if (!item.closing) count++;
            return count;
        }

        private RectTransform TemplateFor(NoticeLevel level) =>
            level == NoticeLevel.Warning && warningTemplate != null ? warningTemplate : template;

        private void Open(Pending pending)
        {
            Item item = Take(TemplateFor(pending.level));
            if (item.dismiss != null) item.dismiss.SetActive(true);
            Prepare(item, pending.text, pending.onClick, pending.untilHidden);
            visible.Insert(0, item);
            enabled = true;
        }

        private void Prepare(Item item, string text, Action onClick, bool untilHidden)
        {
            item.text = text;
            item.message.text = text;
            item.onClick = onClick;
            item.shownAt = Time.unscaledTime;
            item.hideAt = untilHidden ? float.PositiveInfinity : item.shownAt + duration;
            item.closing = false;
            item.group.alpha = 0f;
            item.rect.anchoredPosition = new Vector2(-Width, item.rect.anchoredPosition.y);
            item.rect.SetAsLastSibling();
            item.rect.gameObject.SetActive(true);
        }

        private Item Take(RectTransform source)
        {
            int index = pool.FindLastIndex(pooled => pooled.source == source);
            if (index >= 0)
            {
                Item reused = pool[index];
                pool.RemoveAt(index);
                return reused;
            }

            RectTransform rect = Instantiate(source, source.parent);
            rect.name = source.name + "Item";
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            var item = new Item
            {
                source = source,
                rect = rect,
                group = group,
                message = rect.Find("Message").GetComponent<TMP_Text>()
            };

            Transform dismissRect = rect.Find("Dismiss");
            Button dismiss = dismissRect != null ? dismissRect.GetComponent<Button>() : null;
            if (dismiss != null)
            {
                item.dismiss = dismiss.gameObject;
                dismiss.onClick.AddListener(() => Close(item));
            }

            // The card itself, for notices that open something when tapped.
            Button card = rect.GetComponent<Button>();
            if (card == null)
            {
                card = rect.gameObject.AddComponent<Button>();
                card.transition = Selectable.Transition.None;
            }
            card.onClick.AddListener(() => item.onClick?.Invoke());
            return item;
        }

        private void Close(Item item)
        {
            if (item.closing) return;
            item.closing = true;
            item.closingAt = Time.unscaledTime;
            enabled = true;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float slot = 0f;

            // The pinned warning takes the top slot, the rest line up under it.
            finished.Clear();
            foreach (Item item in pinned) Animate(item, now, ref slot);
            foreach (Item item in finished)
            {
                pinned.Remove(item);
                item.rect.gameObject.SetActive(false);
                pool.Add(item);
            }

            finished.Clear();
            foreach (Item item in visible)
            {
                if (!item.closing && now >= item.hideAt) Close(item);
                Animate(item, now, ref slot);
            }

            foreach (Item item in finished)
            {
                visible.Remove(item);
                item.rect.gameObject.SetActive(false);
                pool.Add(item);
            }

            while (waiting.Count > 0 && OpenCount() < maxVisible)
            {
                Pending next = waiting[0];
                waiting.RemoveAt(0);
                Open(next);
            }

            // Ticks only while a notice is on screen, its timer needs it.
            enabled = visible.Count > 0 || pinned.Count > 0;
        }

        private void Animate(Item item, float now, ref float slot)
        {
            Vector2 position = item.rect.anchoredPosition;
            if (item.closing)
            {
                // Slides back out to the left and fades, keeping its place until gone.
                float t = Mathf.Clamp01((now - item.closingAt) / slideTime);
                position.x = Mathf.Lerp(0f, -Width, t * t);
                item.group.alpha = 1f - t;
                if (t >= 1f) finished.Add(item);
            }
            else
            {
                // Ease out: fast at first, settling softly.
                float t = Mathf.Clamp01((now - item.shownAt) / slideTime);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                position.x = Mathf.Lerp(-Width, 0f, eased);
                item.group.alpha = eased;
            }

            float target = -slot;
            position.y = Mathf.Abs(position.y - target) < .5f
                ? target
                : Mathf.Lerp(position.y, target, 1f - Mathf.Exp(-moveSharpness * Time.unscaledDeltaTime));
            item.rect.anchoredPosition = position;

            // Closing ones give up their room as they fade, so the rest move up smoothly.
            slot += (Height + spacing) * item.group.alpha;
        }
    }
}
