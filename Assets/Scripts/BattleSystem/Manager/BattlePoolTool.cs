using UnityEngine;

public class BattlePoolTool : MonoBehaviour
{
    public Transform bulletPoolRoot;
    public Transform enemyPoolRoot;
    public Transform playerBulletPoolRoot;

    public void SetPoolRoot()
    {
        BulletDOTSManager.Instance.poolRoot = bulletPoolRoot;
        EnemyDOTSManager.Instance.poolRoot = enemyPoolRoot;
        PlayerShootingManager.Instance.poolRoot = playerBulletPoolRoot;
    }

    public void PauseAllPool()
    {
        BulletDOTSManager.Instance.isPaused = true;
        EnemyDOTSManager.Instance.isPaused = true;
        PlayerShootingManager.Instance.isPaused = true;
    }


    public void PauseEnemyPool()
    {
        BulletDOTSManager.Instance.isPaused = true;
        EnemyDOTSManager.Instance.isPaused = true;
    }

    public void ContinueAllPool()
    {
        BulletDOTSManager.Instance.isPaused = false;
        EnemyDOTSManager.Instance.isPaused = false;
        PlayerShootingManager.Instance.isPaused = false;
    }

    public void ClearAllPool(bool destroy = false)
    {
        BulletDOTSManager.Instance.ClearAllObjects(destroy);
        EnemyDOTSManager.Instance.ClearAllObjects(destroy);
        PlayerShootingManager.Instance.ClearAllObjects(destroy);
    }


    public void ClearEnemyPool(bool destroy = false)
    {
        BulletDOTSManager.Instance.ClearAllObjects(destroy);
        EnemyDOTSManager.Instance.ClearAllObjects(destroy);
    }
}
