using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class AlphaTransparent : MonoBehaviour
{
    [Range(0f, 1f)]
    public float threshold = 0.1f;

    private void Awake()
    {
        GetComponent<Image>().alphaHitTestMinimumThreshold = threshold;
    }
}