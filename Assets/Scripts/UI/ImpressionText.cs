using TMPro;
using UnityEngine;

public class ImpressionText : MonoBehaviour
{
    public TextMeshProUGUI impressionText;

    public void OnPlayerImpressionChange(float newValue)
    {
        impressionText.SetText("Impression: {0}", newValue);
    }
}
