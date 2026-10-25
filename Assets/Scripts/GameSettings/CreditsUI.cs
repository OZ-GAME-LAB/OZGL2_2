using UnityEngine;
using UnityEngine.UI;


public class CreditsUI : MonoBehaviour
{
    [SerializeField] private Button _closeButton;

    private void OnEnable()
    {
        _closeButton?.onClick.AddListener(HandleClose);
    }

    private void OnDisable()
    {
        _closeButton?.onClick.RemoveListener(HandleClose);
    }

    private void HandleClose()
    {
        this.gameObject.SetActive(false);
    }
}
