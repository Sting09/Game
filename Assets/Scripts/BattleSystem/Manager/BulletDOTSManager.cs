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
    private int maxBulletCapacity = 10000;
    public Transform poolRoot;
    public int maxBulletNum = 2000;
    public int currentBulletNum;
    public float deltaZ = -0.0001f;
    [SerializeField] private float currentZ = 0;

    // --- Native 数据 ---
    private NativeArray<float3> m_Positions;
    private NativeArray<float> m_Speeds;
    private NativeArray<float> m_Angles;
    private NativeArray<float> m_Lifetimes;

    // 【新增】上一帧的角度缓存，用于减少 transform.rotation 的写入次数
    private NativeArray<float> m_LastAngles;

    // TransformAccessArray
    private TransformAccessArray m_Transforms;

    // --- Managed 数据 ---
    private List<GameObject> m_ActiveGOs;
    private Queue<GameObject> m_PoolQueue;

    private int m_ActiveCount = 0;
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
        // 申请 Native 内存
        m_Positions = new NativeArray<float3>(maxBulletCapacity, Allocator.Persistent);
        m_Speeds = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_Angles = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_Lifetimes = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);

        // 【新增】初始化 LastAngles
        m_LastAngles = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);

        m_Transforms = new TransformAccessArray(maxBulletCapacity);

        m_ActiveGOs = new List<GameObject>(maxBulletCapacity);
        m_PoolQueue = new Queue<GameObject>(maxBulletCapacity);

        // 预填充对象池
        for (int i = 0; i < maxBulletNum; i++)
        {
            CreateNewBulletToPool();
        }

        m_IsInitialized = true;
    }

    private void DisposeMemory()
    {
        if (!m_IsInitialized) return;

        if (m_Positions.IsCreated) m_Positions.Dispose();
        if (m_Speeds.IsCreated) m_Speeds.Dispose();
        if (m_Angles.IsCreated) m_Angles.Dispose();
        if (m_Lifetimes.IsCreated) m_Lifetimes.Dispose();

        // 【新增】释放 LastAngles
        if (m_LastAngles.IsCreated) m_LastAngles.Dispose();

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
        currentZ -= deltaZ;
        m_Positions[index] = new float3(startPos.x, startPos.y, currentZ);
        m_Speeds[index] = info.speed;
        m_Angles[index] = info.direction;
        m_Lifetimes[index] = 0f;

        // 【新增】初始化 LastAngle，防止第一帧重复计算
        m_LastAngles[index] = info.direction;

        m_Transforms.Add(obj.transform);
        m_ActiveGOs.Add(obj);

        m_ActiveCount++;
    }

    void Update()
    {
        if (m_ActiveCount == 0) return;

        float dt = Time.deltaTime;

        BulletMoveJob moveJob = new BulletMoveJob
        {
            dt = dt,
            positions = m_Positions,
            speeds = m_Speeds,
            angles = m_Angles,
            lifetimes = m_Lifetimes,
            // 【新增】传入 LastAngles
            lastAngles = m_LastAngles
        };

        JobHandle handle = moveJob.Schedule(m_Transforms);
        handle.Complete();

        CheckAndRemoveBullets();

        currentBulletNum = m_ActiveCount;
        currentZ = 0;
    }

    private void CheckAndRemoveBullets()
    {
        for (int i = m_ActiveCount - 1; i >= 0; i--)
        {
            float3 pos = m_Positions[i];
            float life = m_Lifetimes[i];

            bool isDead = life > 10f;
            bool isOutOfBounds = pos.x < -20 || pos.x > 20 || pos.y < -12 || pos.y > 12;

            if (isDead || isOutOfBounds)
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

            // 【新增】搬运 LastAngles
            m_LastAngles[index] = m_LastAngles[lastIndex];

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

    // 【新增】读写 LastAngles (不是 ReadOnly，因为要更新缓存)
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