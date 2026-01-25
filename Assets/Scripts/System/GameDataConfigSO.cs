using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameDataConfigSO", menuName = "Global/GameDataConfigSO")]
public class GameDataConfigSO : ScriptableObject
{
    public List<TileDataSO> randomTileDataList;

    public List<TileDataSO> fixedTileDataList;

    public List<OpponentDataSO> opponentDataList;
}
