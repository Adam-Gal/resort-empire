using System.Collections;
using TMPro;
using UnityEngine;

// Lock over a locked island with what it needs to open, fades away once it opens.
public class IslandLockMarker : MonoBehaviour
{
    [SerializeField] private TMP_Text requirement;
    [SerializeField] private CanvasGroup group;
    [SerializeField, Min(0f)] private float fadeTime = .5f;

    //Grows when the camera zooms out past this lens size, so it stays readable from afar
    [SerializeField, Min(.1f)] private float referenceZoom = 8.5f;
    [SerializeField, Min(1f)] private float maxGrow = 2f;

    private Vector3 baseScale;
    private Camera view;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void LateUpdate()
    {
        if (view == null) view = Camera.main;
        if (view == null || !view.orthographic) return;
        float grow = Mathf.Clamp(view.orthographicSize / referenceZoom, 1f, maxGrow);
        transform.localScale = baseScale * grow;
    }

    public void Show(string text)
    {
        if (requirement != null) requirement.text = text;
        if (group != null) group.alpha = 1f;
        gameObject.SetActive(true);
    }

    public void Hide(bool fade)
    {
        if (!gameObject.activeInHierarchy) return;
        if (fade && group != null && fadeTime > 0f) StartCoroutine(FadeOut());
        else gameObject.SetActive(false);
    }

    private IEnumerator FadeOut()
    {
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / fadeTime;
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
