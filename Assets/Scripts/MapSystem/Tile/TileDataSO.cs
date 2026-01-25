using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TileDataSO", menuName = "Game Data/TileDataSO")]
public class TileDataSO : ScriptableObject
{
    public string tileName;                 //地块名称
    public Sprite bgSprite;                 //在地图上的背景图
    public List<AttackSO> attackList;     //这个区块全部的野怪样式
    public List<RewardSO> rewardList;     //这个区块全部的奖励
    //public List<ItemSO> itemList;           //这个区块的道具池
}
