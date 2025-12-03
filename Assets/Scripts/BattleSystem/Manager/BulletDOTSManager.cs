using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs; // 必须引用：处理 TransformAccessArray
using Unity.Collections; // 必须引用：处理 NativeArray
using Unity.Jobs; // 必须引用：Job System
using Unity.Burst; // 必须引用：Burst 编译器
using Unity.Mathematics; // 必须引用：高速数学库 float3, sincos

public class BulletDOTSManager : SingletonMono<BulletDOTSManager>
{
    // --- 配置 ---
    [Header("Pool Config")]
    public GameObject bulletPrefab;
    private int maxBulletCapacity = 10000; // 硬上限，申请内存用
    public Transform poolRoot;
    public int maxBulletNum = 2000;
    public int currentBulletNum;
    public float deltaZ = -0.0001f;
    [SerializeField]private float currentZ = 0;

    // --- 核心 SoA 数据 (Native 侧) ---
    // 这些数组常驻内存，绝对不要在 Update 里 new！
    private NativeArray<float3> m_Positions;
    private NativeArray<float> m_Speeds;
    private NativeArray<float> m_Angles; // 角度制
    private NativeArray<float> m_Lifetimes;

    // 专门用于多线程修改 Transform 的特殊数组
    private TransformAccessArray m_Transforms;

    // --- 辅助数据 (Managed 侧) ---
    // 用于管理 GameObject 的激活/回收 (Job 里不能碰 GameObject，只能在主线程处理)
    private List<GameObject> m_ActiveGOs;
    private Queue<GameObject> m_PoolQueue;

    // 当前活跃子弹数
    private int m_ActiveCount = 0;

    // 标记是否已初始化
    private bool m_IsInitialized = false;

    protected override void Awake()
    {
        base.Awake();
        InitializeMemory();
    }

    void OnDestroy()
    {
        DisposeMemory();
    }

    private void InitializeMemory()
    {
        // 1. 申请 Native 内存 (Allocator.Persistent 表示我们要长期持有)
        m_Positions = new NativeArray<float3>(maxBulletCapacity, Allocator.Persistent);
        m_Speeds = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_Angles = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_Lifetimes = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);

        // TransformAccessArray 需要特殊的初始化
        m_Transforms = new TransformAccessArray(maxBulletCapacity);

        // 2. 初始化对象池结构
        m_ActiveGOs = new List<GameObject>(maxBulletCapacity);
        m_PoolQueue = new Queue<GameObject>(maxBulletCapacity);

        // 预填充对象池 (避免运行时 Instantiate)
        for (int i = 0; i < maxBulletNum; i++)
        {
            CreateNewBulletToPool();
        }

        m_IsInitialized = true;
    }

    private void DisposeMemory()
    {
        if (!m_IsInitialized) return;

        // 必须手动释放 Native 内存，否则内存泄漏！
        if (m_Positions.IsCreated) m_Positions.Dispose();
        if (m_Speeds.IsCreated) m_Speeds.Dispose();
        if (m_Angles.IsCreated) m_Angles.Dispose();
        if (m_Lifetimes.IsCreated) m_Lifetimes.Dispose();
        if (m_Transforms.isCreated) m_Transforms.Dispose();
    }

    private GameObject CreateNewBulletToPool()
    {
        GameObject obj = Instantiate(bulletPrefab, poolRoot);
        obj.SetActive(false);
        m_PoolQueue.Enqueue(obj);
        return obj;
    }

    // --- 核心发射逻辑 ---
    public void AddBullet(Vector3 startPos, BulletRuntimeInfo info)
    {
        if (m_ActiveCount >= maxBulletCapacity)
        {
            Debug.LogWarning("Bullet pool limit reached!");
            return;
        }

        // 1. 获取 GameObject (对象池逻辑)
        GameObject obj = null;
        if (m_PoolQueue.Count > 0)
        {
            obj = m_PoolQueue.Dequeue();
        }
        else
        {
            obj = CreateNewBulletToPool(); // 迫不得已扩容
            obj = m_PoolQueue.Dequeue();
        }

        obj.SetActive(true);
        // 初始化 Transform，防止闪现
        obj.transform.SetPositionAndRotation(startPos, Quaternion.Euler(0, 0, -info.direction));

        // 2. 填充 Native 数据 (直接写数组，极快)
        int index = m_ActiveCount;
        currentZ -= deltaZ;
        m_Positions[index] = new float3(startPos.x, startPos.y, currentZ);
        m_Speeds[index] = info.speed;
        m_Angles[index] = info.direction;
        m_Lifetimes[index] = 0f;

        // 3. 注册 Transform 到 AccessArray
        m_Transforms.Add(obj.transform);

        // 4. 注册 GameObject 到列表 (为了回收用)
        m_ActiveGOs.Add(obj);

        m_ActiveCount++;
    }

    // --- 核心更新循环 ---
    void Update()
    {
        if (m_ActiveCount == 0) return;

        float dt = Time.deltaTime;

        // ==========================================
        // 步骤 1: 调度 Job (多线程并行计算 + 移动)
        // ==========================================
        BulletMoveJob moveJob = new BulletMoveJob
        {
            dt = dt,
            positions = m_Positions,
            speeds = m_Speeds,
            angles = m_Angles,
            lifetimes = m_Lifetimes
        };

        // Schedule 需要两个参数：
        // 1. TransformAccessArray
        // 2. JobHandle (依赖关系，这里没有依赖传 default)
        JobHandle handle = moveJob.Schedule(m_Transforms);

        // ==========================================
        // 步骤 2: 等待 Job 完成
        // ==========================================
        // 注意：在极致优化中，我们会把 Complete 放到 LateUpdate 
        // 甚至下一帧，以利用时间间隙。但现在为了逻辑简单，立刻 Complete。
        handle.Complete();

        // ==========================================
        // 步骤 3: 边界检查与回收 (主线程)
        // ==========================================
        // 因为 Job 里不能做 Remove 操作，必须回到主线程做
        CheckAndRemoveBullets();

        currentBulletNum = m_ActiveCount;

        //重置CurrentZ
        currentZ = 0;
    }

    private void CheckAndRemoveBullets()
    {
        // 倒序遍历，方便 SwapBack 删除
        // 此时 m_Positions 里的数据已经被 Job 更新过了
        for (int i = m_ActiveCount - 1; i >= 0; i--)
        {
            float3 pos = m_Positions[i];
            float life = m_Lifetimes[i];

            // 简单的边界检查逻辑
            bool isDead = life > 10f; // 假设寿命10秒
            bool isOutOfBounds = pos.x < -20 || pos.x > 20 || pos.y < -12 || pos.y > 12;

            if (isDead || isOutOfBounds)
            {
                RemoveBulletAt(i);
            }
        }
    }

    // --- O(1) 的 SwapBack 删除核心 ---
    private void RemoveBulletAt(int index)
    {
        int lastIndex = m_ActiveCount - 1;
        GameObject objToRemove = m_ActiveGOs[index];

        // 1. 回收 GameObject
        objToRemove.SetActive(false);
        m_PoolQueue.Enqueue(objToRemove);

        // 2. 如果删除的不是最后一个，需要把最后一个搬过来填坑
        if (index != lastIndex)
        {
            // Move Native Data
            m_Positions[index] = m_Positions[lastIndex];
            m_Speeds[index] = m_Speeds[lastIndex];
            m_Angles[index] = m_Angles[lastIndex];
            m_Lifetimes[index] = m_Lifetimes[lastIndex];

            // Move GameObject List
            m_ActiveGOs[index] = m_ActiveGOs[lastIndex];

            // Move TransformAccessArray (这是一个特殊操作)
            // RemoveAtSwapBack 是 O(1) 的，它会自动把最后一个元素的 Transform 搬到 index 位置
            m_Transforms.RemoveAtSwapBack(index);
        }
        else
        {
            // 如果是最后一个，直接删除
            m_Transforms.RemoveAtSwapBack(index);
        }

        // 移除 List 尾部 (O(1))
        m_ActiveGOs.RemoveAt(lastIndex);

        m_ActiveCount--;
    }
}

// =========================================================
// The Job (Burst 编译的核心)
// =========================================================
[BurstCompile]
public struct BulletMoveJob : IJobParallelForTransform
{
    // 读写数据
    public NativeArray<float3> positions;
    public NativeArray<float> lifetimes;

    // 只读数据 (加上 [ReadOnly] 可以进一步优化性能)
    [ReadOnly] public NativeArray<float> speeds;
    [ReadOnly] public NativeArray<float> angles;

    [ReadOnly] public float dt;

    // Execute 会在多个线程上并行执行
    // index: 当前子弹的索引
    // transform: 当前子弹的 Transform 访问器 (直接操作 C++ 侧数据)
    public void Execute(int index, TransformAccess transform)
    {
        // 1. 更新生命
        lifetimes[index] += dt;

        // 2. 计算位移
        // math.sincos 是 SIMD 优化的三角函数，比 Mathf.Sin/Cos 快
        float angleRad = math.radians(angles[index]);
        float s, c;
        math.sincos(angleRad, out s, out c);

        // 注意：STG 一般是正右(0度)为X轴正向，Y轴向下90度? 
        // 这里假设标准数学系：0度右，90度上。
        // 如果是顺时针旋转，需要调整符号。
        // 这里为了简单，假设 dir = (cos, sin)
        // 另外，Unity 的 rotation z 是逆时针为正。
        // 你的逻辑中 angle 似乎是角度制。

        // 你的逻辑：float radians = -currentAngle * Mathf.Deg2Rad;
        // 对应的 math 写法：
        float radians = math.radians(-angles[index]);
        math.sincos(radians, out s, out c);

        float3 dir = new float3(c, s, 0);
        float3 move = dir * speeds[index] * dt;

        // 3. 更新位置数据
        float3 newPos = positions[index] + move;
        positions[index] = newPos;

        // 4. 直接写回 Transform (最快的方式)
        // 保持 Z 轴不变 (或者在这里处理 Z-Depth)
        //float3 currentPos = transform.position;
        //transform.position = new float3(newPos.x, newPos.y, newPos.z);
        transform.position = newPos;

        // 5. 更新旋转 (如果需要)
        transform.rotation = Quaternion.Euler(0, 0, -angles[index]);
    }
}