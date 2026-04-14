using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "BossAction/MoveTo")]
public class MoveToActionSO : BossTransitionActionSO
{
    public Vector2 targetPosition;
    public float speed;
    public override IEnumerator Execute(BossController boss)
    {
        while (Vector2.Distance(boss.transform.position, targetPosition) > 0.01f)
        {
            boss.transform.position = Vector2.MoveTowards(boss.transform.position, targetPosition, speed * Time.deltaTime);
            // 同步给EnemyDOTSManager中对应的坐标
            EnemyDOTSManager.Instance.SyncObjectPosition(EnemyDOTSManager.Instance.bossEnemyID, boss.transform.position);
            yield return null;
        }
    }
}
