using System.Collections;
using UnityEngine;

public class BattleManager : SingletonMono<BattleManager>
{
    public GameObject player;

    /// <summary>
    /// Gets current battle player position.
    /// </summary>
    public Vector3 GetPlayerPos()
    {
        return player != null ? player.transform.position : Vector3.zero;
    }

    /// <summary>
    /// Sets active state of the battle player object.
    /// </summary>
    public void SetPlayerActive(bool state)
    {
        player.SetActive(state);
    }

    /// <summary>
    /// Calculates clockwise angle from start point to end point.
    /// </summary>
    public float CalculateAngle(Vector3 startPoint, Vector3 endPoint)
    {
        float dx = endPoint.x - startPoint.x;
        float dy = endPoint.y - startPoint.y;
        float radians = Mathf.Atan2(dy, dx);
        float degrees = radians * Mathf.Rad2Deg;
        return -degrees;
    }

    public void StartBattle()
    {
    }

    /// <summary>
    /// Ends battle and returns to map flow.
    /// </summary>
    public void EndBattle(bool isPlayerWin)
    {
        StartCoroutine(CloseBattleProcess(isPlayerWin));
    }

    private IEnumerator CloseBattleProcess(bool isWin)
    {
        if (BattleContext.OnBattleResult != null)
        {
            BattleContext.OnBattleResult.Invoke(isWin);
        }

        yield return StartCoroutine(SceneLoader.Instance.LoadMapScene());
    }
}
