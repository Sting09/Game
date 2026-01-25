using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public EnemyAIMode aiMode = EnemyAIMode.Idiot;

    // 预分配缓冲区（避免GC）
    private Dictionary<Tile, int> _tileToIndex;
    private int[] _parentIndex;
    private bool[] _visited;
    private int[] _queue;
    private int _queueHead, _queueTail;
    private int _cachedCount;
    private int[] _neighborOrder = { 0, 1, 2, 3, 4, 5 };
    private readonly Room[] _currentRooms = new Room[6];

    private static readonly int[] DLine = { -1, -1, 0, 0, 1, 1 };
    private static readonly int[] DIndex = { -1, 0, -1, 1, 0, 1 };




    /// <summary>
    /// 所有敌人采取行动。在Opponent Phase调用。
    /// </summary>
    public void AllOpponetTakeAction()
    {
        //每名存活的敌人都进行行动
        int num = GlobalSetting.Instance.globalVariable.contestantNum - 1;
        for(int i = 0; i < num; i++)
        {
            OpponentInfo info = GameManager.Instance.opponentInfo[i];
            if (info.opponentState == ContestantState.Alive)
            {
                OpponentTakeAction(info, i);
            }
        }

        //更新画面表现
        GameManager.Instance.UpdatePlayerSight();
    }

    /// <summary>
    /// 根据AI模式调用对应的函数
    /// </summary>
    /// <param name="info">敌人信息</param>
    /// <param name="index">在GameManager里敌人信息集合的索引</param>
    public void OpponentTakeAction(OpponentInfo info, int index)
    {
        switch(aiMode)
        {
            case EnemyAIMode.Idiot:
                IdiotAI(info, index);
                break;
            case EnemyAIMode.Master:
                break;
            default: 
                break;
        }
    }

    /*======================================================================
     ============================数据格式==================================
    =======================================================================
    
     public struct OpponentInfo
    {
        public OpponentDataSO opponentData;
        public ContestantState opponentState;    // 该名敌人的状态
        public Room currentRoom;
        public float power;
    }

    public class OpponentDataSO : ScriptableObject
    {
        public string opponentName;
        public Sprite opponentIcon;
        public AttackSO opponentAttack;
    }

    ========================================================================
    */

    /// <summary>
    /// 傻瓜AI。
    /// </summary>
    /// <param name="info">敌人信息</param>
    /// <param name="index">在GameManager敌人信息列表中的索引</param>
    private void IdiotAI(OpponentInfo info, int index)
    {
        Room currentRoom = info.currentRoom;
        //决定本回合探索当前地块还是跨地块，只看当前地块是不是将要消失

        //-------------不缩圈，探索当前地块-----------
        if (currentRoom.parentTile.state == TileState.Normal)
        {
            //在能到达的，次数最多的进行一次探索
            //选择与敌人战斗还是与野怪战斗
            //与难度最低的敌人战斗
        }
        //--------------缩圈，跨地块移动-------------
        else
        {
            //如果当前回合数为1，暂时先这么写，以后应该用一个bool值
            if(info.currentTargetTile == info.currentRoom.parentTile || info.currentTargetTile.state == TileState.AlreadyShrink)
            {
                //更新目的地Tile，选择最近的不会消失地块
                Vector2Int currentIndex = new Vector2Int(currentRoom.parentTile.line, currentRoom.parentTile.index);
                info.currentTargetTile = SelectTargetTile(currentIndex);
            }

            //获取为了到达目标Tile，要前往哪个房间
            info.currentTargetRoom = SelectTargetRoom(info.currentRoom.parentTile, info.currentTargetTile, info.currentRoom);

            //探索一个地块

            //战斗
            //和对手战斗
            if(info.currentTargetRoom == null)
            {

            }
            else if(info.currentTargetRoom.currentOpponentIndex >= 0)
            {
                bool currentOpponentWin = true;
                if (currentOpponentWin)
                {
                    Debug.Log("一场激烈的战斗！");
                    //对手死亡
                    GameManager.Instance.OpponentDie(info.currentTargetRoom.currentOpponentIndex);
                    //当前对手移动
                    GameManager.Instance.OpponentMove(index, info.currentTargetRoom);
                }
                else
                {

                }
            }
            //和玩家战斗
            else if(info.currentTargetRoom.havePlayer)
            {
                //先写成玩家胜利
                bool playerWin = true;
                if (playerWin)
                {
                    Debug.Log("你被挑战了！");
                    GameManager.Instance.OpponentDie(index);
                }
                else
                {

                }
            }
            //和野怪战斗
            else if (info.currentTargetRoom.currentAttack!= null &&
                !info.currentTargetRoom.currentAttack.isOpponent)
            {
                GameManager.Instance.OpponentMove(index, info.currentTargetRoom);
            }
            else
            {

                GameManager.Instance.OpponentMove(index, info.currentTargetRoom);
            }
        }
    }






    //----------------------工具函数--------------------------
    // Fisher-Yates 洗牌，无 GC
    private void ShuffleNeighborOrder()
    {
        for (int i = 5; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = _neighborOrder[i];
            _neighborOrder[i] = _neighborOrder[j];
            _neighborOrder[j] = temp;
        }
    }

    private void EnsureBuffers(List<Tile> allItems)
    {
        int n = allItems.Count;
        if (_visited == null || _cachedCount != n)
        {
            _tileToIndex = new Dictionary<Tile, int>(n);
            _parentIndex = new int[n];
            _visited = new bool[n];
            _queue = new int[n];
            _cachedCount = n;
        }
        else
        {
            _tileToIndex.Clear();
        }
        for (int i = 0; i < n; i++)
            _tileToIndex[allItems[i]] = i;
    }

    public Tile SelectTargetTile(Vector2Int currentTileIndex)
    {
        var gridDict = MapManager.Instance.gridDictionary;
        var allItems = MapManager.Instance.allItems;

        if (!gridDict.TryGetValue(currentTileIndex, out Tile currentTile))
            return null;

        if (currentTile.state == TileState.Normal)
            return null;

        if (currentTile.state == TileState.AlreadyShrink)
            return null;

        EnsureBuffers(allItems);

        int n = allItems.Count;
        if (!_tileToIndex.TryGetValue(currentTile, out int startIdx))
            return null;

        Array.Clear(_visited, 0, n);
        _queueHead = 0;
        _queueTail = 0;

        _queue[_queueTail++] = startIdx;
        _visited[startIdx] = true;
        _parentIndex[startIdx] = -1;

        int targetIdx = -1;

        while (_queueHead < _queueTail)
        {
            int curIdx = _queue[_queueHead++];
            Tile cur = allItems[curIdx];

            if (cur.state == TileState.Normal)
            {
                targetIdx = curIdx;
                break;
            }

            int line = cur.line;
            int index = cur.index;

            // 每次扩展前随机打乱邻居顺序
            ShuffleNeighborOrder();

            for (int i = 0; i < 6; i++)
            {
                int d = _neighborOrder[i];  // 使用随机顺序
                Vector2Int neighborPos = new Vector2Int(line + DLine[d], index + DIndex[d]);

                if (gridDict.TryGetValue(neighborPos, out Tile neighbor) &&
                    neighbor.state != TileState.AlreadyShrink &&
                    _tileToIndex.TryGetValue(neighbor, out int neighborIdx) &&
                    !_visited[neighborIdx])
                {
                    _visited[neighborIdx] = true;
                    _parentIndex[neighborIdx] = curIdx;
                    _queue[_queueTail++] = neighborIdx;
                }
            }
        }

        if (targetIdx < 0)
            return null;

        int step = targetIdx;
        while (_parentIndex[step] != startIdx)
        {
            step = _parentIndex[step];
        }

        return allItems[step];
    }

    public Room SelectTargetRoom(Tile currentTile, Tile targetTile, Room currentRoom)
    {
        if (currentTile == null || targetTile == null || currentRoom == null)
            return null;

        // 1. 确定桥接方向
        int dLine = targetTile.line - currentTile.line;
        int dIndex = targetTile.index - currentTile.index;
        int bridgeDir = GetBridgeDirectionForRoom(dLine, dIndex);

        if (bridgeDir < 0) return null;

        // 2. 缓存 currentTile 的房间（避免重复查找）
        foreach (var room in currentTile.rooms)
            _currentRooms[(int)room.direction] = room.roomObj;

        // 3. 获取 targetTile 中的目标房间
        int targetDir = (bridgeDir + 3) % 6;
        Room targetRoom = null;
        foreach (var room in targetTile.rooms)
        {
            if ((int)room.direction == targetDir)
            {
                targetRoom = room.roomObj;
                break;
            }
        }
        if (targetRoom == null) return null;

        int startDir = (int)currentRoom.direction;

        // 4. 如果已经在桥接房间，直接跨过去
        if (startDir == bridgeDir)
        {
            return targetRoom;
        }

        // 5. 计算顺时针路径的代价和第一个敌人
        int cwCost = 0;
        Room cwFirstEnemy = null;
        for (int d = (startDir + 1) % 6; ; d = (d + 1) % 6)
        {
            Room room = _currentRooms[d];
            if (room.havePlayer || (room.haveEnemy && room != currentRoom))
            {
                cwCost++;
                if (cwFirstEnemy == null) cwFirstEnemy = room;
            }
            if (d == bridgeDir) break;
        }

        // 6. 计算逆时针路径的代价和第一个敌人
        int ccwCost = 0;
        Room ccwFirstEnemy = null;
        for (int d = (startDir + 5) % 6; ; d = (d + 5) % 6)
        {
            Room room = _currentRooms[d];
            if (room.havePlayer || (room.haveEnemy && room != currentRoom))
            {
                ccwCost++;
                if (ccwFirstEnemy == null) ccwFirstEnemy = room;
            }
            if (d == bridgeDir) break;
        }

        // 7. 选择代价小的路径（相等时随机）
        bool chooseClockwise;
        if (cwCost < ccwCost)
            chooseClockwise = true;
        else if (ccwCost < cwCost)
            chooseClockwise = false;
        else
            chooseClockwise = UnityEngine.Random.Range(0, 2) == 0;

        Room firstEnemy = chooseClockwise ? cwFirstEnemy : ccwFirstEnemy;

        // 8. 返回第一个消耗体力的房间，或到达的目标房间
        return firstEnemy ?? targetRoom;
    }

    private int GetBridgeDirectionForRoom(int dLine, int dIndex)
    {
        if (dLine == 0 && dIndex == 1) return 0;  // Right
        if (dLine == 1 && dIndex == 1) return 1;  // RightBottom
        if (dLine == 1 && dIndex == 0) return 2;  // LeftBottom
        if (dLine == 0 && dIndex == -1) return 3;  // Left
        if (dLine == -1 && dIndex == -1) return 4;  // LeftTop
        if (dLine == -1 && dIndex == 0) return 5;  // RightTop
        return -1;
    }
}
