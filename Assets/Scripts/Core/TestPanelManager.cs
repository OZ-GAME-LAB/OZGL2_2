using UnityEngine;
using UnityEngine.UIElements;

public class TestPanelManager : MonoBehaviour
{
    [SerializeField] private GameObject TestCanvas;

    public void Toggle()
    {
        TestCanvas.SetActive(!TestCanvas.activeSelf);
    }
}
