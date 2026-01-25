using TMPro;
using UnityEngine;

public class PowerText : MonoBehaviour
{
    public TextMeshProUGUI powerText;

    public void OnPlayerPowerChange(float newValue)
    {
        powerText.SetText("Power: {0}", newValue);
    }
}
