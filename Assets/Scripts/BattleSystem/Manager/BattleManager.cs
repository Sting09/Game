using System.Collections.Generic;
using UnityEngine;

public class BattleManager : SingletonMono<BattleManager> 
{
    public GameObject player;

    public Vector3 GetPlayerPos()
    {
        return player != null ? player.transform.position : Vector3.zero;
    }

    public float CalculateAngle(Vector3 startPoint, Vector3 endPoint)
    {
        // 1. 计算方向向量 (终点 - 起点)
        // 因为是 2D 平面，我们只需要 x 和 y
        float dx = endPoint.x - startPoint.x;
        float dy = endPoint.y - startPoint.y;

        // 2. 使用 Atan2 计算弧度
        // Mathf.Atan2(y, x) 返回的是弧度值
        // 标准结果：右(0), 上(+), 左(+-PI), 下(-)
        float radians = Mathf.Atan2(dy, dx);

        // 3. 转换为角度 (Multiply by 180/PI)
        float degrees = radians * Mathf.Rad2Deg;

        // 4. 调整方向符合你的需求
        // 你的需求：顺时针旋转为正 (正下为90)
        // Unity默认：逆时针旋转为正 (正上为90，正下为-90)
        // 解决方法：直接取负号
        return -degrees;
    }
}
