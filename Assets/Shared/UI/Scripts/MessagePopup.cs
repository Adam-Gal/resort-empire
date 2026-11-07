using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Dialog with a title, a message and one button that closes it.
public class MessagePopup : MonoBehaviour
{
    [SerializeField] private GameObject dialog;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [SerializeField] private Button okButton;
    [SerializeField] private TMP_Text okLabel;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        okButton.onClick.AddListener(Hide);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        dialog.SetActive(false);
    }

    public void Show(string heading, string message, string button = "Rozumiem")
    {
        title.text = heading;
        body.text = message;
        if (okLabel != null) okLabel.text = button;
        dialog.SetActive(true);
    }

    public void Hide() => dialog.SetActive(false);
}
