using UnityEngine;

[CreateAssetMenu(fileName = "GlobalVariableSO", menuName = "Global/GlobalVariableSO")]
public class GlobalVariableSO : ScriptableObject
{
    [Header("Map System")]
    public int mapLevel = 3;          //µØÍ¼ÓÐ¼¸²ã
    public float tileEdgeLength;
    public Sprite defaultRoomIcon;

    [Header("Competiton Info")]
    public int contestantNum = 16;

    [Header("Monster Icon")]
    public Sprite lowLevelMonsterIcon;
    public int minMediumLevel;
    public Sprite mediumLevelMonsterIcon;
    public int minHighLevel;
    public Sprite highLevelMonsterIcon;
}
