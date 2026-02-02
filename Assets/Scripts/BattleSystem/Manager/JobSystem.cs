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

    // 新增的发射信息
    [ReadOnly] public NativeArray<int> shootPointIndices;

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
    [ReadOnly] public NativeArray<float> accelAngles;       // 现在这个值代表"相对角度" (0=前方, 90=右侧)
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
        float currentAngle = angles[index];     // 当前朝向（单位：度）
        float accel = accelerations[index];     // 加速度大小
        float angVel = angularVelocities[index];

        // 1. 处理角速度 (先旋转)
        // 如果有角速度，先改变当前的朝向
        if (math.abs(angVel) > 0.001f)
        {
            currentAngle += angVel * dt;
        }

        // 2. 将当前速度转换为向量
        // 注意：原代码使用了 radians(-currentAngle)，保持坐标系一致性
        float angleRad = math.radians(-currentAngle);
        float s, c;
        math.sincos(angleRad, out s, out c);
        float2 velVec = new float2(c, s) * currentSpeed;

        // 3. 处理加速度 (核心修改部分)
        // 【修复1】使用 abs 允许负加速度生效
        if (math.abs(accel) > 0.0001f)
        {
            // 【修复2】计算相对加速度方向
            // accelAngles[index] 现在被视为相对于子弹当前朝向的偏移量
            // 0度 = 沿当前速度方向加速
            // 180度 = 沿当前速度反方向加速
            float relativeAccelAngle = accelAngles[index];
            float finalAccelAngle = currentAngle + relativeAccelAngle;

            // 转换为弧度向量 (保持与上面一致的 -angle 转换逻辑)
            float accRad = math.radians(-finalAccelAngle);
            float2 accVec = new float2(math.cos(accRad), math.sin(accRad)) * accel;

            // 矢量相加：速度 + 加速度 * 时间
            velVec += accVec * dt;

            // 根据新的速度向量，反推新的速度大小和朝向
            float newSpeed = math.length(velVec);

            // 只有当速度大于微小值时才更新角度，防止速度为0时角度归零
            if (newSpeed > 0.001f)
            {
                // atan2 返回的是 CCW 弧度，需要转换回原本的 "负角度" 系统
                // 因为上面用了 radians(-angle)，这里反向操作就是 -degrees(atan2)
                float newRad = math.atan2(velVec.y, velVec.x);
                currentAngle = -math.degrees(newRad);
            }

            currentSpeed = newSpeed;
        }

        // 4. 写回数据
        speeds[index] = currentSpeed;
        angles[index] = currentAngle;

        // 5. 计算位移并更新位置
        float3 move = new float3(velVec.x, velVec.y, 0) * dt;
        float3 newPos = positions[index] + move;

        // --- 相对移动逻辑 ---
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

        // 6. 更新旋转 (减少不必要的 SetRotation 调用)
        if (math.abs(currentAngle - lastAngles[index]) > 0.001f)
        {
            // 同样保持 -currentAngle 的旋转逻辑
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
        //玩家撞到子弹时，子弹将被消除
        return true;
    }

    public void ObjectOnCollision(int index)
    {
        //记录哪些id的子弹碰撞到了玩家
        //BulletManager在下一帧Update最开始时，处理这些子弹
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
        //玩家撞到敌人时，敌人不消失
        return false;
    }

    public void ObjectOnCollision(int index)
    {
        //记录哪些id的敌人碰撞到了玩家
        //EnemyManager在下一帧Update最开始时，处理这些敌人
        collisionEvents.Enqueue(index);
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
    [ReadOnly] public NativeArray<float> hp;

    public void Execute(int index)
    {
        CheckBurstStatus();

        if (isDeadResults[index]) return;

        // 修改：使用每个子弹的最大生命周期进行判断
        bool lifeDead = lifetimes[index] > maxLifetimes[index];
        bool hpDead = hp[index] <= 0;
        isDeadResults[index] = lifeDead || hpDead;
    }

    [BurstDiscard]
    private void CheckBurstStatus()
    {
        Debug.LogWarning($"[性能警告] EnemyCullJob 正在以 Mono (慢速) 模式运行！Burst 未生效！");
    }
}





// 在 JobSystem.cs 的末尾添加以下代码

/// <summary>
/// 玩家子弹与敌人的碰撞检测 Job
/// 使用 IJobParallelFor 并行处理每一颗子弹
/// </summary>
[BurstCompile]
public struct PlayerBulletEnemyCollisionJob : IJobParallelFor
{
    // --- 玩家子弹数据 (只读) ---
    [ReadOnly] public NativeArray<float3> bulletPositions;
    [ReadOnly] public NativeArray<int> bulletTypes; // 0=Circle, 1=Box
    [ReadOnly] public NativeArray<float> bulletRadii;
    [ReadOnly] public NativeArray<float2> bulletBoxSizes;
    [ReadOnly] public NativeArray<float> bulletAngles;
    [ReadOnly] public NativeArray<bool> bulletIsDead;

    // --- 敌人数据 (只读) ---
    [ReadOnly] public NativeArray<float3> enemyPositions;
    [ReadOnly] public NativeArray<int> enemyTypes; // 0=Circle, 1=Box
    [ReadOnly] public NativeArray<float> enemyRadii;
    [ReadOnly] public NativeArray<float2> enemyBoxSizes;
    [ReadOnly] public NativeArray<float> enemyAngles;
    [ReadOnly] public NativeArray<float> enemyHP;

    // --- 输出结果 ---
    // x = bulletIndex, y = enemyIndex
    [WriteOnly] public NativeQueue<int2>.ParallelWriter hitResults;

    public void Execute(int i)
    {
        // 如果子弹已死，直接跳过
        if (bulletIsDead[i]) return;

        float3 bPos = bulletPositions[i];
        int bType = bulletTypes[i];
        float bRadius = bulletRadii[i];
        float2 bSize = bulletBoxSizes[i];
        float bAngle = bulletAngles[i];

        // 遍历所有敌人
        // 虽然这里是双重循环，但因为并行化 + Burst SIMD 优化，处理几千次检测非常快
        for (int j = 0; j < enemyPositions.Length; j++)
        {
            // 如果敌人已死 (HP<=0)，跳过
            if (enemyHP[j] <= 0f) continue;

            bool hit = CheckCollision(
                bPos.xy, bType, bRadius, bSize, bAngle,
                enemyPositions[j].xy, enemyTypes[j], enemyRadii[j], enemyBoxSizes[j], enemyAngles[j]
            );

            if (hit)
            {
                // 记录碰撞对 (子弹索引, 敌人索引)
                hitResults.Enqueue(new int2(i, j));

                // 一颗子弹击中一个敌人后立即停止检测（防止单颗子弹同一帧打中多个敌人，或穿透）
                break;
            }
        }
    }

    // --- 内部数学计算库 (Burst Compatible) ---

    private bool CheckCollision(float2 p1, int t1, float r1, float2 s1, float a1,
                                float2 p2, int t2, float r2, float2 s2, float a2)
    {
        if (t1 == 0) // Bullet is Circle
        {
            if (t2 == 0) return CircleVsCircle(p1, r1, p2, r2);
            else return CircleVsBox(p1, r1, p2, s2, a2);
        }
        else // Bullet is Box
        {
            if (t2 == 0) return CircleVsBox(p2, r2, p1, s1, a1);
            else return BoxVsBox(p1, s1, a1, p2, s2, a2);
        }
    }

    private bool CircleVsCircle(float2 c1, float r1, float2 c2, float r2)
    {
        float distSq = math.distancesq(c1, c2);
        float r = r1 + r2;
        return distSq < r * r;
    }

    private bool CircleVsBox(float2 circleCenter, float radius, float2 boxCenter, float2 boxSize, float boxAngleDeg)
    {
        float2 diff = circleCenter - boxCenter;
        float angRad = math.radians(boxAngleDeg);
        float s, c;
        math.sincos(angRad, out s, out c);

        // 将圆形中心转换到盒子的局部坐标系
        float2 localCirclePos = new float2(
            diff.x * c + diff.y * s, // 修正旋转公式：逆向旋转
            -diff.x * s + diff.y * c
        );

        float2 halfExtents = boxSize / 2f;
        float2 closestPoint = math.clamp(localCirclePos, -halfExtents, halfExtents);
        float distSq = math.distancesq(localCirclePos, closestPoint);

        return distSq < radius * radius;
    }

    /// <summary>
    /// 优化后的 BoxVsBox (分离轴定理 SAT - 投影半径法)
    /// 无需数组分配，完全兼容 Burst
    /// </summary>
    private bool BoxVsBox(float2 centerA, float2 sizeA, float angleADeg, float2 centerB, float2 sizeB, float angleBDeg)
    {
        float2 halfA = sizeA / 2f;
        float2 halfB = sizeB / 2f;
        float2 dist = centerB - centerA; // 中心距向量

        // 计算 A 的两个轴
        float radA = math.radians(angleADeg);
        float sA, cA;
        math.sincos(radA, out sA, out cA);
        float2 axA_X = new float2(cA, sA);  // A 的 X 轴
        float2 axA_Y = new float2(-sA, cA); // A 的 Y 轴

        // 计算 B 的两个轴
        float radB = math.radians(angleBDeg);
        float sB, cB;
        math.sincos(radB, out sB, out cB);
        float2 axB_X = new float2(cB, sB);  // B 的 X 轴
        float2 axB_Y = new float2(-sB, cB); // B 的 Y 轴

        // 检查 4 个分离轴：A_X, A_Y, B_X, B_Y
        // 如果任意一个轴上没有重叠，则两个盒子不相交
        if (!OverlapOnAxis(dist, axA_X, halfA, axA_X, axA_Y, halfB, axB_X, axB_Y)) return false;
        if (!OverlapOnAxis(dist, axA_Y, halfA, axA_X, axA_Y, halfB, axB_X, axB_Y)) return false;
        if (!OverlapOnAxis(dist, axB_X, halfA, axA_X, axA_Y, halfB, axB_X, axB_Y)) return false;
        if (!OverlapOnAxis(dist, axB_Y, halfA, axA_X, axA_Y, halfB, axB_X, axB_Y)) return false;

        return true;
    }

    // 检查在特定轴 axis 上的投影是否重叠
    private bool OverlapOnAxis(float2 distVec, float2 axis,
        float2 halfA, float2 axA_X, float2 axA_Y,
        float2 halfB, float2 axB_X, float2 axB_Y)
    {
        // 1. 将中心距投影到轴上
        float projDist = math.abs(math.dot(distVec, axis));

        // 2. 计算 Box A 在该轴上的最大投影半径
        // Box A 的半径 = |HalfX * (AxisX dot Axis)| + |HalfY * (AxisY dot Axis)|
        float rA = halfA.x * math.abs(math.dot(axA_X, axis)) + halfA.y * math.abs(math.dot(axA_Y, axis));

        // 3. 计算 Box B 在该轴上的最大投影半径
        float rB = halfB.x * math.abs(math.dot(axB_X, axis)) + halfB.y * math.abs(math.dot(axB_Y, axis));

        // 4. 如果 投影距离 > 半径之和，则无重叠
        return projDist <= (rA + rB);
    }
}



/// <summary>
/// 处理发射器死亡的 Job
/// </summary>
[BurstCompile]
public struct EmitterDeathHandlingJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<bool> isRelative;
    [ReadOnly] public NativeArray<int> emitterIDs;

    // =========================================================
    // 【修改点】：删除了 [DeallocateOnJobCompletion] 特性
    // 我们将在调度后手动调用 .Dispose(JobHandle)
    // =========================================================
    [ReadOnly]
    public NativeHashSet<int> deadEmitterSet;

    public NativeArray<bool> isDead;

    public void Execute(int i)
    {
        if (!isRelative[i]) return;

        int eID = emitterIDs[i];
        if (deadEmitterSet.Contains(eID))
        {
            isDead[i] = true;
        }
    }
}