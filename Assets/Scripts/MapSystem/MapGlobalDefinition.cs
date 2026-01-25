using UnityEngine;

public enum GamePhase
{
    GameStart,
    GenerateMap,
    GenerateSchedule,
    RoundStart,
    UpdateMapBeforeRound,
    TurnStart,
    UpdatePointInfo,
    PlayerPhase,
    BattlePhase,
    OpponentPhase,
    TurnEnd,
    TournamentPhase,
    UpdateMapAfterRound,
    RoundEnd,
    SuddenDeath,
    GameEnd
}

public enum RoomDirection
{
    Right,
    RightBottom,
    LeftBottom,
    Left,
    LeftTop,
    RightTop
}

public enum RoomState
{
    Empty,
    HavePlayer,
    HaveOpponent,
    HaveEnemy,
    HaveFacility,
    NotApplicable
}

public enum TileState
{
    Normal,
    WillShrink,
    AlreadyShrink
}

public enum ContestantState
{
    Alive,
    Dead,
}

public enum EnemyAIMode
{
    Idiot,
    Master
}



[System.Serializable]
public struct RoomRef
{
    public Room roomObj;
    public RoomDirection direction;

    public RoomRef(Room _roomObj, RoomDirection _direction)
    {
        this.roomObj = _roomObj;
        this.direction = _direction;
    }
}
