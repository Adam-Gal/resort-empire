using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.Presentation
{
    // Short message in the corner that hides itself after a while or on the close button.
    [DisallowMultipleComponent]
    public sealed class TransientNotice : MonoBehaviour
    {
        public TMP_Text Message;
        public Button Dismiss;
        [Min(.5f)] public float Duration = 3.5f;

        private Coroutine hiding;

        private void Awake()
        {
            if (Dismiss) Dismiss.onClick.AddListener(Hide);
        }

        public void Show(string text)
        {
            if (Message) Message.text = text;
            gameObject.SetActive(true);
            if (hiding != null) StopCoroutine(hiding);
            hiding = StartCoroutine(HideLater());
        }

        public void Hide()
        {
            if (hiding != null) StopCoroutine(hiding);
            hiding = null;
            gameObject.SetActive(false);
        }

        private IEnumerator HideLater()
        {
            yield return new WaitForSecondsRealtime(Duration);
            hiding = null;
            gameObject.SetActive(false);
        }
    }
}
