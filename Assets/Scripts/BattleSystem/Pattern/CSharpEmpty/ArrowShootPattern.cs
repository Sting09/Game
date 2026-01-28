using UnityEngine;
using UnityEngine.UI;

public class ArrowShootPattern : ShootPattern
{
    private ArrowPatternSO arrowConfig;

    // 运行时缓存参数
    public int n;                   //一共2n+1个子弹，不算正中间，一边n个子弹
    public float centerDirection;
    public float edgeAngle;
    public float internalAngle;
    public float centerMinSpeed;
    public float centerMaxSpeed;
    public int bulletsPerWay;
    public float distance;

    // 构造函数：接收配置文件和运行时引用
    public ArrowShootPattern(ArrowPatternSO config, EmitterRuntime runtime) : base(config, runtime)
    {
        arrowConfig = config;
    }

    public override void UpdatePattern()
    {
        // 从SO中读取参数值（支持随机范围）
        n = arrowConfig.shootWaysHalf.GetValue();
        bulletsPerWay = arrowConfig.bulletsPerWay.GetValue();
        centerDirection = arrowConfig.centerDirection.GetValue();
        edgeAngle = arrowConfig.edgeAngle.GetValue();
        internalAngle = arrowConfig.internalAngle.GetValue();
        distance = arrowConfig.distance.GetValue();
        centerMinSpeed = arrowConfig.centerMinSpeed.GetValue();
        centerMaxSpeed = arrowConfig.centerMaxSpeed.GetValue();
    }

    public override void ShootBullet(BulletRuntimeInfo info, Vector3 pos, float dir)
    {
        UpdatePattern(); // 每次发射更新参数

        if(bulletsPerWay <= 0) { return; }

        float startSpeed = centerMinSpeed;
        float deltaSpeed = bulletsPerWay == 1 ? 0 : (centerMaxSpeed - centerMinSpeed) / (bulletsPerWay - 1);

        for(int i = 0; i < bulletsPerWay; i++)
        {
            float speed = startSpeed + deltaSpeed * i;

            // 1. 准备基础角度（转换为弧度以便计算）
            float angleODeg = internalAngle / 2f;
            float angleBDeg = edgeAngle / 2f;

            float angleORad = angleODeg * Mathf.Deg2Rad;
            float angleBRad = angleBDeg * Mathf.Deg2Rad;

            // 角An = 180 - O - B
            // Sin(An) = Sin(O + B)
            float sumAngleRad = angleORad + angleBRad;

            // 2. 计算线段 AnB 的总长度 (利用正弦定理: AnB / Sin(O) = OB / Sin(An))
            // 这是一个固定值，用于后续按比例分割
            float totalLengthAnB = speed * Mathf.Sin(angleORad) / Mathf.Sin(sumAngleRad);

            // 3. 循环计算 A1 到 An (k 从 1 到 n)
            for (int k = 0; k <= n; k++)
            {
                if (k == 0)    //正中间的子弹特别处理
                {
                    info.wayIndex = 0;
                    info.orderInWay = i;
                    info.orderInOneShoot = i;    // 本次发射中的总序号，暂时不用，暂时不填
                    info.orderInWave = i;    // 本波次中的总序号，暂时不用，暂时不填
                    info.speed = speed;
                    info.direction = dir + centerDirection; //正中心子弹的角度

                    // 最终位置 = 发射器位置 + 箭头形状偏移
                    switch (type)
                    {
                        case ShootObjType.Bullet:
                            BulletDOTSManager.Instance.AddBullet(bulletTypeID, bulletBehaviourID, pos + CalculatePosOffset(dir, distance), info);
                            break;
                        case ShootObjType.BulletGroup:
                            break;
                        case ShootObjType.PlayerBullet:
                            PlayerShootingManager.Instance.AddBullet(bulletTypeID, bulletBehaviourID, pos + CalculatePosOffset(dir, distance), info);
                            break;
                        case ShootObjType.Enemy:
                            EnemyDOTSManager.Instance.AddEnemy(bulletTypeID, bulletBehaviourID, pos + CalculatePosOffset(dir, distance), info);
                            break;
                        default:
                            break;
                    }

                    continue;
                }

                // 当前点 Ak 距离 B 的长度 (BAk)
                float ratio = (float)k / n;
                float lengthBAk = totalLengthAnB * ratio;

                // --- 计算 OAk 的长度 (利用余弦定理) ---
                // c^2 = a^2 + b^2 - 2ab*cos(C)
                // OAk^2 = OB^2 + BAk^2 - 2 * OB * BAk * cos(B)
                float lengthOAkSq = (speed * speed) + (lengthBAk * lengthBAk) - (2 * speed * lengthBAk * Mathf.Cos(angleBRad));
                float lengthOAk = Mathf.Sqrt(lengthOAkSq);

                // --- 计算角 BOAk (利用余弦定理反推) ---
                // cos(O) = (a^2 + c^2 - b^2) / 2ac
                // 在三角形 O-B-Ak 中，求角O(即角BOAk)
                // BAk^2 = OB^2 + OAk^2 - 2 * OB * OAk * cos(AngleBOAk)
                // => cos(AngleBOAk) = (OB^2 + OAk^2 - BAk^2) / (2 * OB * OAk)

                float cosVal = ((speed * speed) + (lengthOAkSq) - (lengthBAk * lengthBAk)) / (2 * speed * lengthOAk);

                // 防止浮点数误差导致超出 [-1, 1] 范围
                cosVal = Mathf.Clamp(cosVal, -1f, 1f);

                float angleBOAkRad = Mathf.Acos(cosVal);
                float angleBOAkDeg = angleBOAkRad * Mathf.Rad2Deg;

                BulletRuntimeInfo infoLeft = info;
                infoLeft.wayIndex = 2 * k - 1;
                infoLeft.orderInWay = i;
                infoLeft.orderInOneShoot = bulletsPerWay * (2 * k - 1) + 2 * i;    // 本次发射中的总序号
                infoLeft.orderInWave = info.timesInWave * (2 * n + 1) * bulletsPerWay + 
                                        bulletsPerWay * (2 * k - 1) + 2 * i;    // 本波次中的总序号
                infoLeft.speed = lengthOAk;
                infoLeft.direction = dir + centerDirection + angleBOAkDeg;
                switch (type)
                {
                    case ShootObjType.Bullet:
                        BulletDOTSManager.Instance.AddBullet(bulletTypeID, bulletBehaviourID, pos + CalculatePosOffset(dir, distance), info);
                        break;
                    case ShootObjType.BulletGroup:
                        break;
                    case ShootObjType.PlayerBullet:
                        PlayerShootingManager.Instance.AddBullet(bulletTypeID, bulletBehaviourID, pos + CalculatePosOffset(dir, distance), info);
                        break;
                    case ShootObjType.Enemy:
                        EnemyDOTSManager.Instance.AddEnemy(bulletTypeID, bulletBehaviourID, pos + CalculatePosOffset(dir, distance), info);
                        break;
                    default:
                        break;
                }



                BulletRuntimeInfo infoRight = info;
                infoRight.wayIndex = 2 * k;
                infoRight.orderInWay = i;
                infoRight.orderInOneShoot = bulletsPerWay * (2 * k - 1) + 2 * i + 1;    // 本次发射中的总序号
                infoRight.orderInWave = info.timesInWave * (2 * n + 1) * bulletsPerWay + 
                                        bulletsPerWay * (2 * k - 1) + 2 * i + 1;    // 本波次中的总序号
                infoRight.speed = lengthOAk;
                infoRight.direction = dir + centerDirection - angleBOAkDeg;
                switch (type)
                {
                    case ShootObjType.Bullet:
                        BulletDOTSManager.Instance.AddBullet(bulletTypeID, bulletBehaviourID, pos + CalculatePosOffset(dir, distance), info);
                        break;
                    case ShootObjType.BulletGroup:
                        break;
                    case ShootObjType.PlayerBullet:
                        PlayerShootingManager.Instance.AddBullet(bulletTypeID, bulletBehaviourID, pos + CalculatePosOffset(dir, distance), info);
                        break;
                    case ShootObjType.Enemy:
                        EnemyDOTSManager.Instance.AddEnemy(bulletTypeID, bulletBehaviourID, pos + CalculatePosOffset(dir, distance), info);
                        break;
                    default:
                        break;
                }


                // 存入结果
                /*results.Add(new PointData
                {
                    Index = k,
                    LengthOA = lengthOAk,
                    AngleBOA = angleBOAkDeg
                });*/
            }
        }
    }

    /// <summary>
    /// 返回一条子弹初始位置相对于圆心的位置偏移 (复用CircleShootPattern逻辑)
    /// </summary>
    private Vector3 CalculatePosOffset(float direction, float distance)
    {
        // 将角度转换为弧度，顺时针为正，需要对传入的 direction 取反
        float angleRad = -direction * Mathf.Deg2Rad;

        float x = distance * Mathf.Cos(angleRad);
        float y = distance * Mathf.Sin(angleRad);

        return new Vector3(x, y, 0f);
    }
}