using System;
using TMPro;
using UnityEngine;

public class SpeedControllButton : MonoBehaviour
{
    [SerializeField] private TMP_Text SpeedText;
    public void Start()
    {
        Time.timeScale = 1;
    }

    public void ChangeTimeScale()
    {
        switch (Time.timeScale)
        {
            case 1f:
                Time.timeScale = 1.5f;
                break;
            case 1.5f:
                Time.timeScale = 2f;
                break;
            case 2f:
                Time.timeScale = 1f;
                break;
        }
        Refresh();
    }

    public void Refresh()
    {
        SpeedText.text = $"X{Time.timeScale}";
    }
}
