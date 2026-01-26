using UnityEngine;

[CreateAssetMenu(fileName = "GlobalVariableSO", menuName = "Global/GlobalVariableSO")]
public class GlobalVariableSO : ScriptableObject
{
    [Header("Map System")]
    public int mapLevel = 3;          //地图有几层
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

    [Header("Battle System")]
    [Tooltip("战斗场景高度的一半")]
    public float halfHeight = 7f;
    [Tooltip("战斗场景宽度的一半")]
    public float halfWidth = 15f;

    public float playerMoveRangeHalfHeight = 6f;
    public float playerMoveRangeHalfWidth = 14f;

    public float playerDefaultSpeed = 5f;
    public float slowModeRate = 0.5f;

    public float playerHitboxRadius = 2.5f;
}
