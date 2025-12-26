using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

public class BulletDOTSManager : SingletonMono<BulletDOTSManager>
{
    // --- 配置 ---
    [Header("Pool Config")]
    public GameObject bulletPrefab;
    public int maxBulletCapacity = 10000;           //最大子弹数量
    public Transform poolRoot;
    public int initialPreFillCount = 500;                  //预填充的子弹数量
    public int frameInstantiateLimit = 50;          // 每一帧后台偷偷生成的数量 (避免卡顿)
    public int currentBulletNum;
    public float deltaZ = -0.00001f;
    [SerializeField] private float currentZ = 0;

    // --- Native 数据 ---
    private NativeArray<float3> m_Positions;        //位置
    private NativeArray<float> m_Speeds;            //速度
    private NativeArray<float> m_Angles;            //角度
    private NativeArray<float> m_LastAngles;        //上一帧的角度，如果和角度差别不大，就不新写入Transform
    private NativeArray<float> m_Lifetimes;         //存活了多长时间
    private NativeArray<bool> m_IsDead;             //子弹是否应该在本帧移除

    // TransformAccessArray
    private TransformAccessArray m_Transforms;      //用来给场景内的物体写入Transform数据

    // --- Managed 数据 ---
    private List<GameObject> m_ActiveGOs;           //活跃子弹列表
    private Queue<GameObject> m_PoolQueue;          //子弹对象池

    private int m_ActiveCount = 0;                  //活跃子弹数目
    private bool m_IsInitialized = false;           //Manager是否已经初始化

    private JobHandle m_JobHandle;                  //统一的Job句柄
    private JobHandle bulletMoveJobHandle;          //控制子弹移动的任务句柄
    private JobHandle bulletCullJobHandle;          //移除子弹的任务句柄


    protected override void Awake()
    {
        base.Awake();
        InitializeMemory();
    }

    void OnDestroy()
    {
        // 确保销毁前 Job 已经结束，否则会导致 Unity 报错或崩溃
        m_JobHandle.Complete();
        DisposeMemory();
    }

    private void InitializeMemory()
    {
        // 申请 Native 内存
        m_Positions = new NativeArray<float3>(maxBulletCapacity, Allocator.Persistent);
        m_Speeds = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_Angles = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_Lifetimes = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_LastAngles = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_IsDead = new NativeArray<bool>(maxBulletCapacity, Allocator.Persistent);

        m_Transforms = new TransformAccessArray(maxBulletCapacity);

        m_ActiveGOs = new List<GameObject>(maxBulletCapacity);
        m_PoolQueue = new Queue<GameObject>(maxBulletCapacity);

        // 预填充对象池
        for (int i = 0; i < initialPreFillCount; i++)
        {
            CreateNewBulletToPool();
        }

        m_IsInitialized = true;

        // 启动后台分帧扩容协程
        StartCoroutine(ExpandPoolRoutine());
    }

    private void DisposeMemory()
    {
        if (!m_IsInitialized) return;

        if (m_Positions.IsCreated) m_Positions.Dispose();
        if (m_Speeds.IsCreated) m_Speeds.Dispose();
        if (m_Angles.IsCreated) m_Angles.Dispose();
        if (m_Lifetimes.IsCreated) m_Lifetimes.Dispose();
        if (m_LastAngles.IsCreated) m_LastAngles.Dispose();
        if (m_IsDead.IsCreated) m_IsDead.Dispose();

        //这里注意一下，前面IsCreated全是大写I，只有这里是小写i
        if (m_Transforms.isCreated) m_Transforms.Dispose();
    }

    private GameObject CreateNewBulletToPool()
    {
        GameObject obj = Instantiate(bulletPrefab, poolRoot);
        obj.SetActive(false);
        m_PoolQueue.Enqueue(obj);
        return obj;
    }

    public void AddBullet(Vector3 startPos, BulletRuntimeInfo info)
    {
        //如果当前有Job正在后台运行，暂时阻塞主线程，立即完成Job
        m_JobHandle.Complete();

        if (m_ActiveCount >= maxBulletCapacity)
        {
            Debug.LogWarning("Bullet pool limit reached!");
            return;
        }

        GameObject obj = null;
        if (m_PoolQueue.Count > 0)
        {
            obj = m_PoolQueue.Dequeue();
        }
        else
        {
            obj = CreateNewBulletToPool();
            obj = m_PoolQueue.Dequeue();
        }

        obj.SetActive(true);
        // 初始化 Transform
        obj.transform.SetPositionAndRotation(startPos, Quaternion.Euler(0, 0, -info.direction));

        // 填充 Native 数据
        int index = m_ActiveCount;
        currentZ += deltaZ;
        m_Positions[index] = new float3(startPos.x, startPos.y, currentZ);
        m_Speeds[index] = info.speed;
        m_Angles[index] = info.direction;
        m_Lifetimes[index] = 0f;
        m_LastAngles[index] = info.direction;
        m_IsDead[index] = false;

        m_Transforms.Add(obj.transform);
        m_ActiveGOs.Add(obj);

        m_ActiveCount++;
    }

    void Update()
    {
        // 每一帧开始时，确保上一帧的Job彻底完成了
        m_JobHandle.Complete();

        if (m_ActiveCount == 0) return;

        float dt = Time.deltaTime;

        //子弹移动Job
        BulletMoveJob moveJob = new BulletMoveJob
        {
            dt = dt,
            positions = m_Positions,
            speeds = m_Speeds,
            angles = m_Angles,
            lifetimes = m_Lifetimes,
            lastAngles = m_LastAngles
        };

        //只Schedule，不调用Complete，让Job在后台线程跑，主线程执行其他脚本的Update
        m_JobHandle = moveJob.Schedule(m_Transforms);

        //移除子弹Job
        BulletCullJob cullJob = new BulletCullJob
        {
            positions = m_Positions,
            lifetimes = m_Lifetimes,
            maxLifetime = 10f,  // 暂时的子弹最大存活时间
            boundsX = 20f,      // 暂时的X边界
            boundsY = 12f,      // 暂时的Y边界
            isDeadResults = m_IsDead
        };

        // dependsOn: moveHandle
        // 这告诉 Unity：必须等 bulletMoveJobHandle 算完了位置，才能开始算 cullJob
        //此时 m_JobHandle 变成了 "MoveJob + CullJob" 的总 Handle
        m_JobHandle = cullJob.Schedule(m_ActiveCount, 64, m_JobHandle);
    }

    // 处理Update后台处理的Job的收尾工作
    void LateUpdate()
    {
        // 等待 Job 完成。如果此时 Job 还没算完，主线程会在这里等待。
        m_JobHandle.Complete();

        if (m_ActiveCount > 0)
        {
            // 移除超出屏幕边界、时间过长的子弹
            CheckAndRemoveBullets();
        }

        // 更新子弹数量的调试数据
        currentBulletNum = m_ActiveCount;
        // 检查是否需要重置z轴遮挡排序
        currentZ = currentZ < -1 ? 0 : currentZ;
    }

    private void CheckAndRemoveBullets()
    {
        for (int i = m_ActiveCount - 1; i >= 0; i--)
        {
            if (m_IsDead[i])
            {
                RemoveBulletAt(i);
            }
        }
    }

    private void RemoveBulletAt(int index)
    {
        int lastIndex = m_ActiveCount - 1;
        GameObject objToRemove = m_ActiveGOs[index];

        objToRemove.SetActive(false);
        m_PoolQueue.Enqueue(objToRemove);

        if (index != lastIndex)
        {
            // Move Native Data
            m_Positions[index] = m_Positions[lastIndex];
            m_Speeds[index] = m_Speeds[lastIndex];
            m_Angles[index] = m_Angles[lastIndex];
            m_Lifetimes[index] = m_Lifetimes[lastIndex];
            m_LastAngles[index] = m_LastAngles[lastIndex];
            m_IsDead[index] = m_IsDead[lastIndex];

            m_ActiveGOs[index] = m_ActiveGOs[lastIndex];
            m_Transforms.RemoveAtSwapBack(index);
        }
        else
        {
            m_Transforms.RemoveAtSwapBack(index);
        }

        m_ActiveGOs.RemoveAt(lastIndex);
        m_ActiveCount--;
    }

    private System.Collections.IEnumerator ExpandPoolRoutine()
    {
        // 计算还需要生成多少个
        int totalToCreate = maxBulletCapacity - initialPreFillCount;
        int createdCount = 0;

        while (createdCount < totalToCreate)
        {
            // 每一帧生成一批
            for (int i = 0; i < frameInstantiateLimit; i++)
            {
                // 如果已经填满了，就停止
                if (m_PoolQueue.Count + m_ActiveCount >= maxBulletCapacity)
                {
                    yield break;
                }

                CreateNewBulletToPool();
                createdCount++;
            }

            // 暂停一帧，让出 CPU 给游戏逻辑和渲染
            yield return null;
        }

        Debug.Log($"[BulletManager] Pool expansion finished. Total Capacity: {maxBulletCapacity}");
    }
}

// =========================================================
// The Job
// =========================================================
[BurstCompile]
public struct BulletMoveJob : IJobParallelForTransform
{
    // 读写数据
    public NativeArray<float3> positions;
    public NativeArray<float> lifetimes;
    public NativeArray<float> lastAngles;

    // 只读数据
    [ReadOnly] public NativeArray<float> speeds;
    [ReadOnly] public NativeArray<float> angles;
    [ReadOnly] public float dt;

    public void Execute(int index, TransformAccess transform)
    {
        // 1. 更新生命
        lifetimes[index] += dt;

        // 2. 计算位移
        float currentAngle = angles[index];
        float angleRad = math.radians(-currentAngle);
        float s, c;
        math.sincos(angleRad, out s, out c);

        float3 dir = new float3(c, s, 0);
        float3 move = dir * speeds[index] * dt;

        // 3. 更新位置
        float3 newPos = positions[index] + move;
        positions[index] = newPos;

        // 【关键修复】显式构造 Vector3，避免 TLS 错误
        transform.position = new Vector3(newPos.x, newPos.y, newPos.z);

        // 4. 更新旋转 (带缓存优化)
        float lastAngle = lastAngles[index];

        // 【优化】只有角度变化超过阈值时才写入 transform.rotation
        if (math.abs(currentAngle - lastAngle) > 0.001f)
        {
            quaternion q = quaternion.RotateZ(angleRad);

            // 【关键修复】显式构造 Quaternion，避免 TLS 错误
            transform.rotation = new Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);

            // 更新缓存
            lastAngles[index] = currentAngle;
        }
    }
}


[BurstCompile]
public struct BulletCullJob : IJobParallelFor
{
    // 输入数据
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float> lifetimes;

    // 边界配置 (可以从 Manager 传入，避免硬编码)
    public float maxLifetime;
    public float boundsX;
    public float boundsY;

    // 输出数据：只写 bool，不改变数组结构
    [WriteOnly] public NativeArray<bool> isDeadResults;

    public void Execute(int index)
    {
        // 1. 检查生命周期
        bool dead = lifetimes[index] > maxLifetime;

        // 2. 检查边界 (如果还没有死，再查边界，节省性能)
        if (!dead)
        {
            float3 pos = positions[index];
            // 这里使用绝对值简化判断：|x| > bound
            bool outOfBounds = pos.x < -boundsX || pos.x > boundsX ||
                               pos.y < -boundsY || pos.y > boundsY;
            dead = outOfBounds;
        }

        // 写入结果
        isDeadResults[index] = dead;
    }
}