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
