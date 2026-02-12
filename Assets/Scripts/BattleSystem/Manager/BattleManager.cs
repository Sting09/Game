using UnityEngine;

public class BattleManager : SingletonMono<BattleManager>
{
    public GameObject player;

    /// <summary>
    /// 获取战斗玩家位置。
    /// </summary>
    public Vector3 GetPlayerPos()
    {
        return player != null ? player.transform.position : Vector3.zero;
    }

    /// <summary>
    /// 设置战斗玩家显示状态。
    /// </summary>
    public void SetPlayerActive(bool state)
    {
        player.SetActive(state);
    }

    /// <summary>
    /// 计算朝向角度（2D）。
    /// </summary>
    public float CalculateAngle(Vector3 startPoint, Vector3 endPoint)
    {
        float dx = endPoint.x - startPoint.x;
        float dy = endPoint.y - startPoint.y;

        float radians = Mathf.Atan2(dy, dx);
        float degrees = radians * Mathf.Rad2Deg;

        return -degrees;
    }

    /// <summary>
    /// 开始战斗（预留扩展）。
    /// </summary>
    public void StartBattle()
    {
    }

    /// <summary>
    /// 结束战斗并回到地图场景。
    /// </summary>
    /// <param name="isPlayerWin">战斗是否胜利。</param>
    public void EndBattle(bool isPlayerWin)
    {
        StartCoroutine(SceneLoader.Instance.ReturnToMapScene(() =>
        {
            if (BattleContext.OnBattleResult != null)
            {
                BattleContext.OnBattleResult.Invoke(isPlayerWin);
            }
        }));
    }
}
