using UnityEngine;
using UnityEngine.UI;

public class ManagerBridge : MonoBehaviour
{
    public Player player;
    public Canvas canvas;
    public Button winButton;


    //MapScene每次加载时，进入第一个阶段（GameStart）
    void OnEnable()
    {
        // 为Manager对该场景内物体的引用重新赋值
        GameManager.Instance.player = player;
        GameManager.Instance.playerObject = player.gameObject;
        GameManager.Instance.mapSceneMap = player.gameObject.transform.parent.gameObject;
        GameManager.Instance.mapSceneCanvas = canvas;
        GameManager.Instance.winBtn = winButton;

        MapManager.Instance.tilesRoot = player.gameObject.transform.parent.gameObject;

        PhaseController.Instance.StartGame();
    }

    public void MapMgr_Randomize()
    {
        MapManager.Instance.RandomizeTileData();
    }

    public void MapMgr_UpdateMapGrid()
    {
        MapManager.Instance.UpdateMapGrid();
    }

    public void MapMgr_Regenerate()
    {
        MapManager.Instance.RegenerateMap();
    }

    public void GameMgr_PlayerBorn()
    {
        GameManager.Instance.PlayerBorn();
    }

    public void GameMgr_MonsterBorn() 
    { 
        GameManager.Instance.AllMonstersBorn();
    }

    public void PhaseMgr_GameWinPhase()
    {
        PhaseController.Instance.GameWinPhase();
    }

    public void PhaseMgr_GameLosePhase()
    {
        PhaseController.Instance.GameLosePhase();
    }

}
