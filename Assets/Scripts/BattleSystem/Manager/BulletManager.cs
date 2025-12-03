using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class BulletManager : SingletonMono<BulletManager>
{
    [SerializeField]private List<BulletManagerData> activeBullets;  //所有激活的子弹
    private Stack<BulletManagerData> inactiveBullets;   //所有未激活的子弹
    public List<GameObject> poolObjects;    //所有子弹对象池
    public int currentBulletNum;   //当前屏幕中的子弹数量
    public Transform playerTransform; // 玩家位置（用于碰撞检测）
    private const float deltaZ = -0.0001f;

    void Start()
    {
        activeBullets = new List<BulletManagerData>();
        inactiveBullets = new Stack<BulletManagerData>();

        //读取子弹对象池中所有子弹的信息
        foreach (GameObject pool in poolObjects)
        {
            foreach (Transform bullet in pool.transform)
            {
                GameObject bulletObject = bullet.gameObject;

                BulletManagerData data = new BulletManagerData();
                data.gameObject = bulletObject;
                data.transform = bulletObject.transform; //直接缓存 Transform 引用
                data.isActive = false;

                inactiveBullets.Push(data);
            }
        }
    }

    
    void Update()
    {
        float dt = Time.deltaTime;

        //更新玩家位置
        Vector2 playerPos = playerTransform != null ? (Vector2)playerTransform.position : Vector2.zero;

        #region 普通的子弹更新（当前不启用）
        /*// 更新active子弹的信息（BulletManagerData）
        // 倒序遍历，方便在循环中移除元素（回收子弹）
        for (int i = activeBullets.Count - 1; i >= 0; i--)
        {
            BulletManagerData b = activeBullets[i];

            // 1. 更新生命周期
            b.info.lifetime += dt;

            // 2. 复杂逻辑处理 (事件系统)，更新currentAngle、currentSpeed和b.info



            // 3. 移动计算 (Mathf.Cos/Sin)
            //更新当前速度和速度方向
            b.currentAngle = b.info.direction;
            b.currentSpeed = b.info.speed;

            // 将角度转为弧度 (顺时针为正，所以取负)
            float radians = -b.currentAngle * Mathf.Deg2Rad;
            float dirX = Mathf.Cos(radians);
            float dirY = Mathf.Sin(radians);

            // 计算位移
            float moveX = dirX * b.currentSpeed * dt;
            float moveY = dirY * b.currentSpeed * dt;

            // 更新数据中的位置
            b.position.x += moveX;
            b.position.y += moveY;

            // 4. 应用到 Unity 场景 (这是唯一的 Transform 访问)
            // 直接使用缓存的 transform 引用，速度非常快，没有查找开销
            b.transform.position += new Vector3(moveX, moveY, 0);

            // (可选) 如果子弹外观需要旋转
            b.transform.rotation = Quaternion.Euler(0, 0, -b.currentAngle);

            // 5. 碰撞检测 (手动计算距离平方)


            // 6. 边界检查 (例如超出屏幕则回收)
            bool outOfLife = b.info.lifetime > 15.0f;
            bool outOfBoundary = b.position.x > 16 || b.position.x < -16 ||
                                 b.position.y > 16 || b.position.y < -16;
            if (outOfLife || outOfBoundary) // 当前假设存活15秒
            {
                ReturnBulletToPool(i);
            }
        }*/
        #endregion

        #region DOTS子弹更新
        if(activeBullets.Count > 0 )
        {
            //新建数组
            NativeArray<BulletData> nativeBulletDataList = 
                new NativeArray<BulletData>(activeBullets.Count, Allocator.TempJob);
            for(int i = 0; i < activeBullets.Count; i++)
            {
                nativeBulletDataList[i] = new BulletData(activeBullets[i], dt);
            }

            //更新数组
            BulletUpdateJob bulletUpdateJob = new BulletUpdateJob();
            bulletUpdateJob.dataList = nativeBulletDataList;
            JobHandle jobHandle = bulletUpdateJob.Schedule(activeBullets.Count, 100);
            jobHandle.Complete();

            //写回列表
            for(int i = activeBullets.Count - 1; i >= 0; i--)
            {
                //销毁子弹
                if (nativeBulletDataList[i].isReadyDestroy)
                {
                    ReturnBulletToPool(i);
                }
                //应用到Unity场景
                else
                {
                    activeBullets[i].transform.position = nativeBulletDataList[i].position;
                    activeBullets[i].transform.rotation = Quaternion.Euler(0, 0, -nativeBulletDataList[i].currentAngle); 
                }
            }

            nativeBulletDataList.Dispose();
        }
        #endregion

        currentBulletNum = activeBullets.Count;
    }

    public void AddBullet(Vector3 startPos, BulletRuntimeInfo info, GameObject pool = null)
    {
        GameObject bulletPool = pool != null ? pool: poolObjects[0];    //从哪个对象池里获取子弹
        PoolTool poolTool = bulletPool.GetComponent<PoolTool>();        //对象池对应的池类

        BulletManagerData b = new BulletManagerData();
        GameObject bulletObj = poolTool.GetObj();

        // 初始化数据
        b.transform = bulletObj.transform;
        b.gameObject = bulletObj;
        b.pool = poolTool;

        b.isActive = true;

        b.info = info;
        b.position = startPos;
        startPos.z = info.orderInWave * deltaZ;
        b.transform.position = startPos;
        b.currentSpeed = info.speed;
        b.currentAngle = info.direction;

        b.gameObject.SetActive(true); // 激活物体
        activeBullets.Add(b);         // 加入活跃列表
    }

    // 回收子弹
    private void ReturnBulletToPool(int index)
    {
        BulletManagerData b = activeBullets[index];
        b.isActive = false;
        b.gameObject.SetActive(false);

        b.pool.ReturnObj(b.gameObject);       // 放回池中
        activeBullets.RemoveAt(index);        // 从活跃列表移除
    }
}

[SerializeField]
public class BulletManagerData
{
    // --- 核心引用 ---
    public Transform transform; //自己的Transform
    public GameObject gameObject;   //自己的GameObject
    public PoolTool pool;   //所在的对象池

    // --- 运动数据 ---
    public Vector2 position;
    public float currentSpeed;
    public float currentAngle; // 角度制，右侧0，正下90

    // --- 逻辑状态 ---
    public BulletRuntimeInfo info;
    public bool isActive;
}

public struct BulletData
{
    //运动数据
    public float currentSpeed;
    public float currentAngle;

    //transform数据
    public float3 position;

    //生命周期数据
    public float lifetime;
    public float maxLifetime;
    public float deltaTime;

    //状态数据     
    public bool isReadyDestroy;  //需要销毁

    //初始化方法
    public BulletData(BulletManagerData data, float deltaTime)
    {
        BulletRuntimeInfo config = data.info;
        currentSpeed = data.currentSpeed;
        currentAngle = data.currentAngle;
        position = data.transform.position;

        lifetime = config.lifetime;
        maxLifetime = 15f;      //测试。暂时填15
        this.deltaTime = deltaTime;
        isReadyDestroy = false;
    }

    public void UpdateBullet()
    {
        // 1. 更新生命周期
        lifetime += deltaTime;
        // 2. 复杂逻辑处理 (事件系统)，更新currentAngle、currentSpeed和b.info

        // 3. 移动计算 (Mathf.Cos/Sin)

        // 将角度转为弧度 (顺时针为正，所以取负)
        float radians = -currentAngle * Mathf.Deg2Rad;
        float dirX = Mathf.Cos(radians);
        float dirY = Mathf.Sin(radians);

        // 计算位移
        float moveX = dirX * currentSpeed * deltaTime;
        float moveY = dirY * currentSpeed * deltaTime;

        // 更新数据中的位置
        position.x += moveX;
        position.y += moveY;

        // 5. 碰撞检测 (手动计算距离平方)


        // 6. 边界检查 (例如超出屏幕则回收)
        bool outOfLife = lifetime > 15.0f;      //暂时写15，实际应该与maxLifetime比较
        bool outOfBoundary = position.x > 16 || position.x < -16 ||
                             position.y > 16 || position.y < -16;
        if (outOfLife || outOfBoundary) // 当前假设存活15秒
        {
            isReadyDestroy = true;
        }
    }
}

public struct BulletUpdateJob: IJobParallelFor
{
    public NativeArray<BulletData> dataList;
    public void Execute(int i)
    {
        var data = dataList[i];
        data.UpdateBullet();
        dataList[i] = data;
    }
}

