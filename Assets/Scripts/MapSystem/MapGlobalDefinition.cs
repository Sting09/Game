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
