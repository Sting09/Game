using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : SingletonMono<GameManager>
{
    public GameObject playerObject;
    public Player player;
    public Room playerCurrentRoom;      //玩家当前所在的房间

    public List<OpponentInfo> opponentInfo;
    public int currentOpponentNum;

    public ObjectEventSO playerNewPositionEvent;

    public List<Tile> tilesToShrink = new List<Tile>();    //缩圈要移除的地块

    public Canvas mapSceneCanvas;
    public GameObject mapSceneMap;

    private int monstersPerTile = 3;    //每个地块刷新几个野怪


    /// <summary>
    /// 【已弃用，现在没有对手的概念了，自动触发Boss战】GenerateSchedule阶段，玩家和对手出生
    /// </summary>
    public void AllContestantsBorn()
    {
        int n = MapManager.Instance.allItems.Count;
        // 1. 初始化有序数组 [0, 1, 2, ..., n-1]
        int[] arr = new int[n];
        for (int i = 0; i < n; i++)
        {
            arr[i] = i;
        }

        // 2. 使用 Fisher-Yates 算法原地打乱
        // 从最后一个元素开始，向前遍历
        for (int i = n - 1; i > 0; i--)
        {
            // 随机选取一个位置 j (范围 0 到 i)
            // 注意：Unity 的 Random.Range(min, max) 整数版是不包含 max 的，所以这里用 i + 1
            int j = UnityEngine.Random.Range(0, i + 1);

            // 交换 arr[i] 和 arr[j]
            int temp = arr[i];
            arr[i] = arr[j];
            arr[j] = temp;
        }
        // 3. 填充列表
        int num = GlobalSetting.Instance.globalVariable.contestantNum;
        opponentInfo = new List<OpponentInfo>(num - 1);
        currentOpponentNum = num - 1;

        // 4. 根据arr，生成参赛者初始位置
        int index = 0;
        for(int i=0; i < num; ++i)
        {
            while (index<n && MapManager.Instance.allItems[arr[index]].isFixedTile == true)
            {
                index++;
            }
            if(index >= n)
            {
                Debug.Log("参赛者数量过多，多于可用地块数量");
            }

            if (i == num - 1)
            {
                PlayerBorn(MapManager.Instance.allItems[arr[index]]);
            }
            else
            {
                OpponentBorn(MapManager.Instance.allItems[arr[index]], i);
            }
            index++;
        }

        UpdatePlayerSight();
    }

    #region 玩家和敌人出生相关函数
    public void PlayerBorn(int tileLine, int tileIndex, RoomDirection direction)
    {

    }

    public void PlayerBorn(Tile tile)
    {
        playerObject.SetActive(true);
        playerCurrentRoom = tile.GetRandomRoom();
        playerCurrentRoom.havePlayer = true;
        playerObject.transform.position = playerCurrentRoom.gameObject.transform.position;
    }

    /// <summary>
    /// 随机选择一个地块，玩家出生
    /// </summary>
    public void PlayerBorn()
    {
        int i = UnityEngine.Random.Range(0, MapManager.Instance.allItems.Count); ;
        Tile randomTile = MapManager.Instance.allItems[i];
        PlayerBorn(randomTile);
    }

    public void OpponentBorn(int tileLine, int tileIndex, RoomDirection direction, int index)
    {

    }

    public void OpponentBorn(Tile tile, int index)
    {
        OpponentInfo info = new OpponentInfo();
        info.opponentData = GlobalSetting.Instance.gameDataConfig.opponentDataList[index];
        info.opponentState = ContestantState.Alive;
        info.currentRoom = tile.GetRandomRoom();
        info.power = 0f;
        info.currentTargetTile = tile;
        opponentInfo.Add(info);

        info.currentRoom.haveEnemy = true;
        info.currentRoom.currentAttack = info.opponentData.opponentAttack;
        info.currentRoom.currentOpponentIndex = index;
        info.currentRoom.currentOpponent = info.opponentData;
    }
    #endregion



    /// <summary>
    /// 地图更新时，通知所有房间，更新视觉信息
    /// </summary>
    public void UpdatePlayerSight()
    {
        playerNewPositionEvent.RaiseEvent(null, this);
    }



    /// <summary>
    /// 地图上刷新野怪，每个区块刷新5个
    /// </summary>
    public void AllMonstersBorn()
    {
        // 1. 定义原始数据
        int[] numbers = { 0, 1, 2, 3, 4, 5 };

        foreach (Tile tile in MapManager.Instance.allItems)
        {
            // 2. 核心逻辑：随机排序并取前3个
            int[] result = numbers.OrderBy(x => Guid.NewGuid()).Take(monstersPerTile + 1).ToArray();

            //遍历六个房间，重复三次
            for (int index = 0, times = 0; times < monstersPerTile; index++, times++)
            {
                int i = result[index];
                //如果房间没有玩家
                if (tile.rooms[i].roomObj != playerCurrentRoom)
                {
                    tile.GenerateOneMonster(i);
                }
                else
                {
                    //有玩家则计数-1
                    times--;
                }
            }
        }

        UpdatePlayerSight();
    }



    /// <summary>
    /// 玩家进行战斗
    /// </summary>
    /// <param name="room">与哪个房间的敌人战斗</param>
    public void PlayerBattle(Room room)
    {
        // 防呆检查
        if (room == null || room.currentAttack == null)
        {
            Debug.LogError("房间或敌人数据为空！请检查配置！");
            return;
        }

        // 更新玩家状态，如果不是无限战斗，则可战斗次数-1
        player.battleNum = player.battleNum > 0 ? player.battleNum - 1 : player.battleNum;

        // 隐藏地图场景元素
        if (mapSceneCanvas != null) mapSceneCanvas.gameObject.SetActive(false);
        if (mapSceneMap != null)  mapSceneMap.SetActive(false);

        //-----------------------------------------------------------------------
        // 3. 锁定玩家操作 (防止在加载时玩家乱点别的房间)
        // PlayerInput.Instance.DisableInput(); 

        // 重置摄像机
        //
        //-----------------------------------------------------------------------


        // 4. 配置战斗上下文 (填充公告板)
        BattleContext.roomData = room;
        BattleContext.currentAttack = room.currentAttack;

        // 5. 定义“战斗结束后要做什么” (这就是你的 bool 返回逻辑)
        BattleContext.OnBattleResult = (isWin) =>
        {
            // --- 这里是战斗结束归来后执行的代码 ---

            // =================================================
            // 恢复摄像机
            // ====================================================

            // B. 处理胜负逻辑
            if (isWin)
            {
                //玩家获得奖励
                //情况一：击败的是野怪。获得区块内的一个奖励
                if (!room.currentAttack.isOpponent)
                {
                    Debug.Log("和野怪的战斗胜利！");
                    int num = room.parentTile.tileData.rewardList.Count;
                    RewardSO reward = room.parentTile.tileData.rewardList[UnityEngine.Random.Range(0, num)];
                    player.ChangePower(reward.powerValue);
                }
                //情况二：击败的是对手。获得他身上的一个道具、对手死亡
                else
                {
                    Debug.Log("和对手的战斗胜利！");
                    player.ChangePower(15f);

                    OpponentDie(room.currentOpponentIndex);
                }
                // 调用 Room 脚本的函数，标记为已通关
                room.SetCleared(true);
                // 玩家暴露度增加
                player.ChangeImpression(GlobalSetting.Instance.globalVariable.battleImpression);
                // 可以在这里发放奖励...
            }
            else
            {
                // 执行死亡惩罚，或者重新挑战
                // Player.Instance.Die();
            }

            // ======================C. 解锁玩家操作=======================
            // PlayerInput.Instance.EnableInput();
            //============================================================

            // D. 清理回调防止内存泄漏
            BattleContext.OnBattleResult = null;

            // 恢复地图场景元素的显示
            // 隐藏地图场景元素
            if (mapSceneCanvas != null) mapSceneCanvas.gameObject.SetActive(true);
            if (mapSceneMap != null) mapSceneMap.SetActive(true);


            // E. 更新地图视野
            UpdatePlayerSight();

            player.ChangeImpression(0);
        };

        // 5. 启动加载流程
        StartCoroutine(SceneLoader.Instance.LoadBattleScene());
    }


    




    /// <summary>
    /// 玩家探索房间
    /// </summary>
    /// <param name="room">要探索哪个房间</param>
    public void PlayerSearch(Room room)
    {
        //更新玩家状态，如果不是无限探索，则探索次数-1
        player.searchNum = player.searchNum > 0 ? player.searchNum - 1 : player.searchNum;
        player.currentSearchCost++;

        //生成随机数检定是否得到奖励
        int randomResult = UnityEngine.Random.Range(0,6);

        if (randomResult < room.remainRewardNum + room.alreadyRewardFailTimes)        //成功
        {
            //从所在区块获得一个奖励
            int num = room.parentTile.tileData.rewardList.Count;
            RewardSO reward = room.parentTile.tileData.rewardList[UnityEngine.Random.Range(0, num)];
            //更新玩家战力
            player.ChangePower(reward.powerValue);

            //更新房间状态
            room.remainRewardNum--;
            room.alreadyRewardFailTimes = 0;
        }
        else       //失败
        {
            //失败动画/提示
            Debug.Log("一无所获！");

            //更新房间状态
            room.alreadyRewardFailTimes ++;
        }

        // 玩家暴露度增加
        player.ChangeImpression(player.currentSearchCost);
        //更新地图视野
        UpdatePlayerSight();
    }





    /// <summary>
    /// 轮次开始时，选择若干地块，轮次结束时消失
    /// </summary>
    public void SelectTileToShrink()
    {
        //已经进入suddenDeath了，就不再缩圈了
        if (PhaseController.Instance.suddenDeath) { return; }

        //选择要消失的地块
        int num = PhaseController.Instance.shrinkNum[PhaseController.Instance.currentRound];
        MapManager.Instance.GetRandomConnectedTilesNonAlloc(num, tilesToShrink);

        //被选中的地块发红光
        foreach(Tile tile in tilesToShrink)
        {
            tile.DangerousLight();
        }
        UpdatePlayerSight();
    }

    /// <summary>
    /// 轮次结束时，地图缩圈，移除轮次开始时指定的地块
    /// </summary>
    public void MapShrink()
    {
        foreach (Tile tile in tilesToShrink)
        {
            tile.TileShrink();
            MapManager.Instance.allItems.Remove(tile);
        }
        UpdatePlayerSight();
    }





    /// <summary>
    /// 让opponentInfo中的第index个对手死掉
    /// </summary>
    /// <param name="index">对手索引</param>
    public void OpponentDie(int index)
    {
        OpponentInfo currentOpponent = opponentInfo[index];

        //修改当前房间信息
        currentOpponent.currentRoom.haveEnemy = false;
        currentOpponent.currentRoom.currentAttack = null;
        currentOpponent.currentRoom.currentOpponentIndex = -1;
        currentOpponent.currentRoom.currentOpponent = null;

        //修改opponentInfo中的数据
        currentOpponent.currentRoom = null;
        currentOpponent.opponentState = ContestantState.Dead;
        opponentInfo[index] = currentOpponent;

        //更新当前对手数
        currentOpponentNum--;
        //对手死光了，则游戏结束
        if(currentOpponentNum <= 0)
        {
            PhaseController.Instance.StartCertainPhase(GamePhase.GameEnd);
        }
    }

    public void OpponentMove(int index, Room targetRoom)
    {
        OpponentInfo currentOpponent = opponentInfo[index];

        //修改当前房间信息
        currentOpponent.currentRoom.haveEnemy = false;
        currentOpponent.currentRoom.currentAttack = null;
        currentOpponent.currentRoom.currentOpponentIndex = -1;
        currentOpponent.currentRoom.currentOpponent = null;

        //修改opponentInfo中的数据
        currentOpponent.currentRoom = targetRoom;
        opponentInfo[index] = currentOpponent;

        //修改新房间信息
        targetRoom.haveEnemy = true;
        targetRoom.currentAttack = currentOpponent.opponentData.opponentAttack;
        targetRoom.currentOpponentIndex = index;
        targetRoom.currentOpponent = currentOpponent.opponentData;
    }

    /// <summary>
    /// 玩家死亡
    /// </summary>
    public void PlayerDie()
    {
        //直接游戏结束
        PhaseController.Instance.StartCertainPhase(GamePhase.GameEnd);
    }
}

[System.Serializable]
public struct OpponentInfo
{
    public OpponentDataSO opponentData;
    public ContestantState opponentState;    // 该名敌人的状态
    public Room currentRoom;
    public float power;

    public Tile currentTargetTile;      //当前跑图的目的地Tile
    public Room currentTargetRoom;      //当前跑图的目的地Room
}
