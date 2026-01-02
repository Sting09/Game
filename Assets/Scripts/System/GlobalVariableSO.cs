using UnityEngine;

[CreateAssetMenu(fileName = "GlobalVariableSO", menuName = "Global/GlobalVariableSO")]
public class GlobalVariableSO : ScriptableObject
{
    [Header("Map System")]
    public int mapLevel = 3;          //µØÍ¼ÓÐ¼¸²ã
    public float tileEdgeLength;
}
