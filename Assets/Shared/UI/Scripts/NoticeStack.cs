using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.Presentation
{
    // Notices on the left of the screen. Each slides in from the left, the newest on top pushes
    // the others down. Only a few show at once, the rest wait until one hides.
    // Runs only while a notice is on screen, then the component sleeps.
    [DisallowMultipleComponent]
    public sealed class NoticeStack : MonoBehaviour
    {
        //A notice in the scene, copied for each message and kept hidden itself
        [SerializeField] private RectTransform template;
        [SerializeField, Min(1)] private int maxVisible = 3;
        [SerializeField, Min(.5f)] private float duration = 4f;
        [SerializeField, Min(0f)] private float spacing = 12f;

        [Header("Animation")]
        [SerializeField, Min(.01f)] private float slideTime = .3f;

        //Speed of the others moving to make room, higher is snappier
        [SerializeField, Min(1f)] private float moveSharpness = 14f;

        private sealed class Item
        {
            public RectTransform rect;
            public CanvasGroup group;
            public TMP_Text message;
            public string text;
            public float shownAt;
            public float hideAt;
            public bool closing;
            public float closingAt;
        }

        private readonly List<Item> visible = new List<Item>();
        private readonly List<Item> pool = new List<Item>();
        private readonly Queue<string> waiting = new Queue<string>();
        private readonly List<Item> finished = new List<Item>();

        private float Height => template.rect.height;
        private float Width => template.rect.width;

        private void Awake()
        {
            template.gameObject.SetActive(false);
            enabled = false;
        }

        public void Show(string text)
        {
            // The same message again only stays longer, so repeated taps do not fill the screen.
            foreach (Item item in visible)
            {
                if (item.closing || item.text != text) continue;
                item.hideAt = Time.unscaledTime + duration;
                return;
            }
            if (waiting.Contains(text)) return;

            if (OpenCount() < maxVisible) Open(text);
            else waiting.Enqueue(text);
        }

        private int OpenCount()
        {
            int count = 0;
            foreach (Item item in visible)
                if (!item.closing) count++;
            return count;
        }

        private void Open(string text)
        {
            Item item = Take();
            item.text = text;
            item.message.text = text;
            item.shownAt = Time.unscaledTime;
            item.hideAt = item.shownAt + duration;
            item.closing = false;
            item.group.alpha = 0f;
            item.rect.anchoredPosition = new Vector2(-Width, 0f);
            item.rect.SetAsLastSibling();
            item.rect.gameObject.SetActive(true);
            visible.Insert(0, item);
            enabled = true;
        }

        private Item Take()
        {
            if (pool.Count > 0)
            {
                Item reused = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                return reused;
            }

            RectTransform rect = Instantiate(template, template.parent);
            rect.name = template.name + "Item";
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            var item = new Item
            {
                rect = rect,
                group = group,
                message = rect.Find("Message").GetComponent<TMP_Text>()
            };
            Button dismiss = rect.GetComponentInChildren<Button>(true);
            if (dismiss != null) dismiss.onClick.AddListener(() => Close(item));
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
            finished.Clear();

            foreach (Item item in visible)
            {
                if (!item.closing && now >= item.hideAt) Close(item);

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

            foreach (Item item in finished)
            {
                visible.Remove(item);
                item.rect.gameObject.SetActive(false);
                pool.Add(item);
            }

            while (waiting.Count > 0 && OpenCount() < maxVisible) Open(waiting.Dequeue());

            // Ticks only while a notice is on screen, its timer needs it.
            enabled = visible.Count > 0;
        }
    }
}
