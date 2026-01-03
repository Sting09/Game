using DG.Tweening;
using UnityEngine;

public class GameManager : SingletonMono<GameManager>
{
    public GameObject playerObject;
    public Player player;
    public Room playerCurrentRoom;      //玩家当前所在的房间
    public ObjectEventSO playerNewPositionEvent;

    private void Start()
    {
        PlayerBorn(MapManager.Instance.GetRandomTile(), EnumUtilities.GetRandom<RoomDirection>());
        UpdatePlayerSight();
    }

    public void PlayerBorn(int tileLine, int tileIndex, RoomDirection direction)
    {

    }

    public void PlayerBorn(Tile tile, RoomDirection direction)
    {
        playerCurrentRoom = tile.GetRandomRoom();
        playerObject.transform.position = playerCurrentRoom.gameObject.transform.position;
    }


    [ContextMenu("Player Reborn")]
    public void PlayerReborn()
    {
        PlayerBorn(MapManager.Instance.GetRandomTile(), EnumUtilities.GetRandom<RoomDirection>());
        UpdatePlayerSight();
    }

    public void UpdatePlayerSight()
    {
        playerNewPositionEvent.RaiseEvent(null, this);
    }
}
