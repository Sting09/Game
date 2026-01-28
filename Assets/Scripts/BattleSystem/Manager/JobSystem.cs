using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

/// <summary>
/// 控制GameObject执行事件的Job
/// </summary>
[BurstCompile]
public struct ObjectEventJob : IJobParallelFor
{
    public NativeArray<float> speeds;
    public NativeArray<float> angles;
    public NativeArray<float> accelerations;
    public NativeArray<float> accelAngles;
    public NativeArray<float> angularVelocities;
    public NativeArray<bool> isDead;
    public NativeArray<int> nextEventIndex;
    public NativeArray<Unity.Mathematics.Random> randoms;
    [ReadOnly] public NativeArray<float> lifetimes;
    [ReadOnly] public NativeArray<int> bulletBehaviorIDs;
    [ReadOnly] public NativeArray<int2> behaviorRanges;
    [ReadOnly] public NativeArray<NativeEntityEvent> globalEvents;

    public void Execute(int index)
    {
        CheckBurstStatus();

        int evtIdx = nextEventIndex[index];
        if (evtIdx == -1) return;

        int bID = bulletBehaviorIDs[index];
        if (bID < 0 || bID >= behaviorRanges.Length) return;

        int2 range = behaviorRanges[bID];
        int globalEndIndex = range.x + range.y;

        while (evtIdx < globalEndIndex)
        {
            NativeEntityEvent evt = globalEvents[evtIdx];
            if (lifetimes[index] < evt.triggerTime) break;

            float val = evt.valueA;
            if (evt.useRandom)
            {
                var rng = randoms[index];
                val = rng.NextFloat(evt.valueA, evt.valueC);
                randoms[index] = rng;
            }

            switch (evt.type)
            {
                case EntityEventType.ChangeSpeed:
                    if (evt.useRelative) speeds[index] += val; else speeds[index] = val; break;
                case EntityEventType.ChangeDirection:
                    if (evt.useRelative) angles[index] += val; else angles[index] = val; break;
                case EntityEventType.SetAcceleration:
                    accelerations[index] = val;
                    if (evt.valueB > 0.5f) accelAngles[index] = angles[index]; else accelAngles[index] = evt.valueC; break;
                case EntityEventType.SetAngularVelocity:
                    angularVelocities[index] = val; break;
                case EntityEventType.Stop:
                    speeds[index] = 0; accelerations[index] = 0; angularVelocities[index] = 0; break;
                case EntityEventType.Recycle:
                    isDead[index] = true; break;
            }
            evtIdx++;
        }
        nextEventIndex[index] = (evtIdx >= globalEndIndex) ? -1 : evtIdx;
    }

    [BurstDiscard]
    private void CheckBurstStatus()
    {
        Debug.LogWarning($"[性能警告] ObjectEventJob 正在以 Mono (慢速) 模式运行！Burst 未生效！");
    }
}




/// <summary>
/// 控制GameObject更新位置的Job
/// </summary>
[BurstCompile]
public struct ObjectMoveJob : IJobParallelForTransform
{
    public NativeArray<float3> positions;
    public NativeArray<float> lifetimes;
    public NativeArray<float> lastAngles;
    public NativeArray<float> speeds;
    public NativeArray<float> angles;
    [ReadOnly] public NativeArray<float> accelerations;
    [ReadOnly] public NativeArray<float> accelAngles;
    [ReadOnly] public NativeArray<float> angularVelocities;
    [ReadOnly] public float dt;

    // --- 相对移动 ---
    [ReadOnly] public NativeArray<bool> isRelative;
    [ReadOnly] public NativeArray<int> emitterIDs;
    [ReadOnly] public NativeHashMap<int, float3> emitterDeltas;

    public void Execute(int index, TransformAccess transform)
    {
        CheckBurstStatus();

        lifetimes[index] += dt;

        float currentSpeed = speeds[index];
        float currentAngle = angles[index];
        float accel = accelerations[index];
        float angVel = angularVelocities[index];

        if (math.abs(angVel) > 0.001f) currentAngle += angVel * dt;

        float angleRad = math.radians(-currentAngle);
        float s, c;
        math.sincos(angleRad, out s, out c);
        float2 velVec = new float2(c, s) * currentSpeed;

        if (accel > 0.0001f)
        {
            float accRad = math.radians(-accelAngles[index]);
            float2 accVec = new float2(math.cos(accRad), math.sin(accRad)) * accel;
            velVec += accVec * dt;
            float newSpeed = math.length(velVec);
            if (newSpeed > 0.001f)
            {
                float newRad = math.atan2(velVec.y, velVec.x);
                currentAngle = -math.degrees(newRad);
            }
            currentSpeed = newSpeed;
        }

        speeds[index] = currentSpeed;
        angles[index] = currentAngle;

        float3 move = new float3(velVec.x, velVec.y, 0) * dt;
        float3 newPos = positions[index] + move;

        // --- 相对移动逻辑：叠加发射者的位移 delta ---
        if (isRelative[index])
        {
            int eID = emitterIDs[index];
            if (emitterDeltas.TryGetValue(eID, out float3 delta))
            {
                newPos += delta;
            }
        }

        positions[index] = newPos;
        transform.position = new Vector3(newPos.x, newPos.y, newPos.z);

        if (math.abs(currentAngle - lastAngles[index]) > 0.001f)
        {
            quaternion q = quaternion.RotateZ(math.radians(-currentAngle));
            transform.rotation = new Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);
            lastAngles[index] = currentAngle;
        }
    }

    [BurstDiscard]
    private void CheckBurstStatus()
    {
        Debug.LogWarning($"[性能警告] ObjectMoveJob 正在以 Mono (慢速) 模式运行！Burst 未生效！");
    }
}




/// <summary>
/// 检查GameObject有没有撞到玩家的Job
/// 如果撞到，会将Object的index保存到队列中，在下一帧Update最开始时处理碰撞事件
/// </summary>
[BurstCompile]
public struct ObjectCollisionJob<T> : IJobParallelFor where T : struct, ICollisionPolicy
{
    [ReadOnly] public float3 playerPos;
    [ReadOnly] public float playerRadius;
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float> angles;

    [ReadOnly] public NativeArray<int> collisionTypes;
    [ReadOnly] public NativeArray<float> bulletRadii;
    [ReadOnly] public NativeArray<float2> boxSizes;

    public NativeArray<bool> isDead;

    // 物体碰撞到玩家时，物体要执行的具体的策略
    public T policyData;

    public void Execute(int index)
    {
        CheckBurstStatus();

        if (isDead[index]) return;

        float3 bPos = positions[index];
        int type = collisionTypes[index];
        bool isHit = false;

        if (type == 0)
        {
            float bRadius = bulletRadii[index];
            float distSq = math.distancesq(bPos.xy, playerPos.xy);
            float combinedRadius = playerRadius + bRadius;

            if (distSq < combinedRadius * combinedRadius)
            {
                isHit = true;
            }
        }
        else if (type == 1)
        {
            float2 bSize = boxSizes[index];
            float bAngle = angles[index];

            float2 diff = playerPos.xy - bPos.xy;
            float angRad = math.radians(bAngle);
            float s, c;
            math.sincos(angRad, out s, out c);

            float2 localPlayerPos = new float2(
                diff.x * c - diff.y * s,
                diff.x * s + diff.y * c
            );

            float2 halfExtents = bSize / 2f;
            float2 closestPoint = math.clamp(localPlayerPos, -halfExtents, halfExtents);
            float distSq = math.distancesq(localPlayerPos, closestPoint);

            if (distSq < playerRadius * playerRadius)
            {
                isHit = true;
            }
        }

        if (isHit)
        {
            if (policyData.CullAfterCollison())
            {
                isDead[index] = true;
            }
            policyData.ObjectOnCollision(index);
        }
    }

    [BurstDiscard]
    private void CheckBurstStatus()
    {
        Debug.LogWarning($"[性能警告] ObjectCollisionJob 正在以 Mono (慢速) 模式运行！Burst 未生效！");
    }
}
// 具体的碰撞处理策略接口
public interface ICollisionPolicy
{
    bool CullAfterCollison();
    void ObjectOnCollision(int entityIndex);
}
// 玩家撞到子弹时，子弹的策略：撞到就销毁
public struct BulletCollisionPolicy : ICollisionPolicy
{
    // 需要写入数据的队列
    [WriteOnly] public NativeQueue<int>.ParallelWriter collisionEvents;

    public bool CullAfterCollison()
    {
        return true;
    }

    public void ObjectOnCollision(int index)
    {
        //玩家撞到子弹时，子弹将被消除
        //BulletManager在下一帧Update最开始时，执行玩家事件
        collisionEvents.Enqueue(index);
    }

}

// 玩家撞到敌人时，敌人的策略
public struct EnemyCollisionPolicy : ICollisionPolicy
{
    // 需要写入数据的队列
    [WriteOnly] public NativeQueue<int>.ParallelWriter collisionEvents;

    public bool CullAfterCollison()
    {
        return false;
    }

    public void ObjectOnCollision(int index)
    {
        //玩家撞到敌人时，敌人什么都不做
        //EnemyManager在下一帧Update最开始时，执行玩家事件
    }
}




/// <summary>
/// 检查Bullet是否需要移除的事件。
/// 检查生命周期和是否超出屏幕边界。
/// 如果需要移除，只修改isDead标志位，LateUpdate时由BulletManager移除所有isDead的Bullet
/// </summary>
[BurstCompile]
public struct BulletCullJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float> lifetimes;
    [ReadOnly] public NativeArray<float> maxLifetimes; // 修改：使用NativeArray存储最大生命周期
    public float cullBoundsX;
    public float cullBoundsY;
    public NativeArray<bool> isDeadResults;

    public void Execute(int index)
    {
        CheckBurstStatus();

        if (isDeadResults[index]) return;

        // 修改：使用每个子弹的最大生命周期进行判断
        bool dead = lifetimes[index] > maxLifetimes[index];
        if (!dead)
        {
            float3 pos = positions[index];
            dead = pos.x < -cullBoundsX || pos.x > cullBoundsX || pos.y < -cullBoundsY || pos.y > cullBoundsY;
        }
        isDeadResults[index] = dead;
    }

    [BurstDiscard]
    private void CheckBurstStatus()
    {
        Debug.LogWarning($"[性能警告] BulletCullJob 正在以 Mono (慢速) 模式运行！Burst 未生效！");
    }
}

/// <summary>
/// 检查Bullet是否需要移除的事件。
/// 只检查生命周期。
/// 如果需要移除，只修改isDead标志位，LateUpdate时由EnemyManager移除所有isDead的Bullet
/// </summary>
[BurstCompile]
public struct EnemyCullJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> lifetimes;
    [ReadOnly] public NativeArray<float> maxLifetimes; // 修改：使用NativeArray存储最大生命周期
    public NativeArray<bool> isDeadResults;

    public void Execute(int index)
    {
        CheckBurstStatus();

        if (isDeadResults[index]) return;

        // 修改：使用每个子弹的最大生命周期进行判断
        bool dead = lifetimes[index] > maxLifetimes[index];
        isDeadResults[index] = dead;
    }

    [BurstDiscard]
    private void CheckBurstStatus()
    {
        Debug.LogWarning($"[性能警告] EnemyCullJob 正在以 Mono (慢速) 模式运行！Burst 未生效！");
    }
}