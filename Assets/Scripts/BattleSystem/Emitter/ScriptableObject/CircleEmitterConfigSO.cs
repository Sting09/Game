using UnityEngine;
using System.Collections.Generic;
using NaughtyAttributes;

[CreateAssetMenu(fileName = "CircleEmitterConfigSO", menuName = "Battle System/Emitter/CircleEmitter")]
public class CircleEmitterConfigSO : AbstractEmitterConfigSO
{
    [Header("Circle Specific")]
    [Tooltip("发射半径。0表示从中心点发射，>0表示子弹在圆环/扇环边缘生成")]
    public float radius = 0f;

    // -----------------------------------------------------------------------
    // 核心逻辑：为了保证 Position 和 Direction 的列表索引一一对应，
    // 我们必须保证两者的循环逻辑完全一致。
    // 在实际的生产环境中，为了避免重复计算正弦余弦，
    // 有时会写一个结构体同时返回 Pos 和 Dir，但鉴于你的基类已经锁死了接口，
    // 我们这里展示标准的独立实现。
    // -----------------------------------------------------------------------

    public override void GetAllShootPointsPosOffset(List<Vector3> outputBuffer)
    {
        // 1. 获取运行时参数 (Random Resolve)
        // 注意：这里必须保证 GetRandomValue 在同一帧内调用时的一致性。
        // 如果 RandomValue 变化剧烈，Runtime 层通常会缓存这些值传进来。
        // 但根据目前的架构，我们假设由 SO 内部计算。

        int wNum = shootWays.GetRandomValue();
        int bNum = bulletsPerWay.GetRandomValue();
        float range = shootRange.GetRandomValue();
        float dirOffset = shootDirection.GetRandomValue();

        // 预防除零错误和非法参数
        if (wNum <= 0) return;
        if (bNum <= 0) return;

        // 2. 预计算角度步长
        float angleStep = 0f;
        float startAngle = 0f;

        CalculateAngleStepAndStart(wNum, range, dirOffset, out angleStep, out startAngle);

        // 3. 填充数据
        // 外层循环：Ways (条数/扇叶)
        for (int i = 0; i < wNum; i++)
        {
            float currentWayAngle = startAngle + (i * angleStep);

            // 如果有半径，需要算出该角度下的偏移坐标
            // 极坐标转笛卡尔坐标：x = r * cos(theta), y = r * sin(theta)
            Vector3 pointPos = Vector3.zero;

            if (radius > 0.001f) // 浮点数比较不要直接用 == 0
            {
                // 注意：Mathf.Cos/Sin 接受的是弧度
                float radians = currentWayAngle * Mathf.Deg2Rad;
                // STG中通常 Y轴朝下为90度 或者 笛卡尔坐标系
                // 这里假设标准数学坐标系：X轴右为0度，Y轴上为90度
                // 如果是Unity 2D，Y轴上通常是90度。
                pointPos.x = radius * Mathf.Cos(radians);
                pointPos.y = radius * Mathf.Sin(radians);
            }

            // 内层循环：BulletsPerWay (每条上的子弹数)
            // 在这个简单实现中，每条上的多颗子弹通常共享同一个发射点
            // 除非你是做“排式”发射。这里假设它们从同一点出来（后续靠速度差拉开距离）
            for (int j = 0; j < bNum; j++)
            {
                outputBuffer.Add(pointPos);
            }
        }
    }

    public override void GetAllShootPointsDirection(List<float> outputBuffer)
    {
        // 1. 参数获取 (必须与 PosOffset 方法中的逻辑保持绝对一致！)
        int wNum = shootWays.GetRandomValue();
        int bNum = bulletsPerWay.GetRandomValue();
        float range = shootRange.GetRandomValue();
        float dirOffset = shootDirection.GetRandomValue();

        if (wNum <= 0) return;
        if (bNum <= 0) return;

        // 2. 预计算
        float angleStep = 0f;
        float startAngle = 0f;

        CalculateAngleStepAndStart(wNum, range, dirOffset, out angleStep, out startAngle);

        // 3. 填充数据
        for (int i = 0; i < wNum; i++)
        {
            float currentWayAngle = startAngle + (i * angleStep);

            // 内层循环：BulletsPerWay
            // 这里我们假设同一条上的子弹角度一致
            // 如果你想做“扇中扇”（每条再开一个小扇形），在这里修改 angle
            for (int j = 0; j < bNum; j++)
            {
                outputBuffer.Add(currentWayAngle);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Helper Method: 封装数学计算，防止两个方法里写得不一样导致 Bug
    // -----------------------------------------------------------------------
    private void CalculateAngleStepAndStart(int ways, float range, float offset, out float step, out float start)
    {
        // 逻辑A：全圆 (360度)
        // 浮点数比较：Range 接近或大于 360
        if (range >= 360f - 0.01f)
        {
            step = 360f / ways;
            start = offset;
        }
        // 逻辑B：扇形 (Sector)
        else
        {
            if (ways > 1)
            {
                step = range / (ways - 1);
                // 扇形通常要居中：基础朝向 - 半个扇形范围
                start = offset - (range / 2f);
            }
            else
            {
                step = 0;
                start = offset;
            }
        }
    }

    public override EmitterRuntime CreateRuntime()
    {
        throw new System.NotImplementedException();
    }
}