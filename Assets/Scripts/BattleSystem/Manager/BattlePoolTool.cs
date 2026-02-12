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
}
