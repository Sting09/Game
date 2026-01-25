using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class EllipseShootPattern : ShootPattern
{
    private EllipsePatternSO ellipseConfig;

    public float minV;
    public float maxV;
    public int num;
    public float direction;
    public bool typeB;      //typeA为角度均匀分布；typeB为周长均匀分割

    public EllipseShootPattern(EllipsePatternSO config, EmitterRuntime runtime) : base(config, runtime)
    {
        ellipseConfig = config;
    }

    public override void UpdatePattern()
    {
        minV = ellipseConfig.minBulletSpeed.GetValue(); 
        maxV = ellipseConfig.maxBulletSpeed.GetValue();
        num = ellipseConfig.shootWays.GetValue();
        direction = ellipseConfig.shootDirection.GetValue();
        //未来实现事件后的写法
        //direction = ownerEmitterRuntime.GetPropertyValue(EmitterPropertyType.PatternEllipse_Direction, direction);
        typeB = ellipseConfig.typeB;
    }

    /// <summary>
    /// 生成旋转的椭圆形弹幕数据
    /// </summary>
    public override void ShootBullet(int shootPointIndex, int timesInWave, int waveTimes,
                                     Vector3 pos, float dir)
    {
        //每次发射子弹时UpdatePattern()，未来可以改为每波再更新
        UpdatePattern();

        if (num <= 0) return;

        if (!typeB)
        {
            //角度均匀
            float angleStep = 360f / num;

            // 这里我们将 a (半长轴) 设为 maxV， b (半短轴) 设为 minV
            // 公式中 a 对应的是 X 轴 (cos)，b 对应 Y 轴 (sin)
            // 当相对角度为 0 时，cos=1, 结果应为 maxV
            float a = maxV;
            float b = minV;

            for (int i = 0; i < num; i++)
            {
                // 1. 正常的均匀分布角度
                float currentAngle = i * angleStep;                     //发射角度

                // 2. 计算相对角度 (Offset)
                // 如果 direction 是 90 度，当 currentAngle 为 90 度时，theta 应为 0
                float thetaDeg = currentAngle - direction;
                float thetaRad = thetaDeg * Mathf.Deg2Rad;

                // 3. 代入椭圆极坐标公式
                // speed = (a * b) / Sqrt( (b * cos(θ))^2 + (a * sin(θ))^2 )
                float cos = Mathf.Cos(thetaRad);
                float sin = Mathf.Sin(thetaRad);

                float numerator = a * b;
                float term1 = Mathf.Pow(b * cos, 2);
                float term2 = Mathf.Pow(a * sin, 2);
                float denominator = Mathf.Sqrt(term1 + term2);

                float currentSpeed = numerator / denominator;

                BulletRuntimeInfo info = new BulletRuntimeInfo();
                info.shootPointIndex = shootPointIndex;
                info.wayIndex = i;
                info.orderInWay = 1;
                info.orderInOneShoot = i;
                info.orderInWave = timesInWave * num + i;
                info.speed = currentSpeed;

                //发射方向还要再加上一个shootDirection
                info.direction = dir + currentAngle;
                info.lifetime = 0;

                //pos加上偏移
                //BulletManager.Instance.AddBullet(pos, info);
                //Debug.Log("Add Bullet!");
                BulletDOTSManager.Instance.AddBullet(bulletTypeID, bulletBehaviourID, pos, info);
            }
        }
        else
        {
            //周长均匀
            int precisionPerSegment = 100;
            int totalSamples = num * precisionPerSegment;

            // 存储所有的采样点 (未旋转的椭圆上)
            List<Vector2> samplePoints = new List<Vector2>(totalSamples + 1);
            List<float> cumulativeLengths = new List<float>(totalSamples + 1);

            float totalPerimeter = 0f;
            Vector2 prevPoint = new Vector2(maxV, 0); // t=0 时的点 (a, 0)

            // 使用参数方程 x = a*cos(t), y = b*sin(t) 进行采样
            // 注意：这里的 t 是参数，不是最终发射角度
            for (int i = 0; i <= totalSamples; i++)
            {
                float t = (float)i / totalSamples * 2 * Mathf.PI;

                // 基础椭圆公式（长轴在X轴）
                float x = maxV * Mathf.Cos(t);
                float y = minV * Mathf.Sin(t); // Unity中Y轴向上这里用正sin，最后根据需求转换

                Vector2 currentPoint = new Vector2(x, y);

                // 累加距离
                float dist = Vector2.Distance(prevPoint, currentPoint);
                totalPerimeter += dist;

                samplePoints.Add(currentPoint);
                cumulativeLengths.Add(totalPerimeter);

                prevPoint = currentPoint;
            }

            // ---------------------------------------------------------
            // 第二步：根据弧长寻找均匀的发射点
            // ---------------------------------------------------------

            float stepLen = totalPerimeter / num; // 每颗子弹应间隔的弧长
            int currentSampleIndex = 0;

            for (int i = 0; i < num; i++)
            {
                float targetDist = i * stepLen;

                // 在累加数组中找到对应的位置 (寻找 targetDist 落在哪个采样段内)
                while (currentSampleIndex < cumulativeLengths.Count - 1 &&
                       cumulativeLengths[currentSampleIndex + 1] < targetDist)
                {
                    currentSampleIndex++;
                }

                // 简单的线性插值来获取更精确的点坐标
                // 实际上直接取 samplePoints[currentSampleIndex] 在高精度下也肉眼难辨，但为了完美我们做插值
                float lenStart = cumulativeLengths[currentSampleIndex];
                float lenEnd = cumulativeLengths[currentSampleIndex + 1];
                float segmentLen = lenEnd - lenStart;
                float ratio = (segmentLen > 0.00001f) ? (targetDist - lenStart) / segmentLen : 0;

                Vector2 pStart = samplePoints[currentSampleIndex];
                Vector2 pEnd = samplePoints[currentSampleIndex + 1];
                Vector2 pointOnEllipse = Vector2.Lerp(pStart, pEnd, ratio);

                // ---------------------------------------------------------
                // 第三步：将点转换为速度和角度，并应用旋转
                // ---------------------------------------------------------

                // 1. 算出该点相对于中心的原始速度 (即原点到该点的距离)
                float speed = pointOnEllipse.magnitude;

                // 2. 算出该点的原始角度 (Atan2 返回弧度)
                float rawAngleRad = Mathf.Atan2(pointOnEllipse.y, pointOnEllipse.x);
                float rawAngleDeg = rawAngleRad * Mathf.Rad2Deg;

                // 3. 应用旋转偏移
                // 你的需求：0是右，90是下。
                // Unity默认：0是右，90是上 (逆时针)。
                // 如果你的逻辑是顺时针旋转，需要: final = raw - direction (或者 raw + direction 视具体坐标系定义)
                // 这里假设你的 "direction" 是顺时针偏转的角度

                // 注意：因为椭圆关于X轴对称，我们之前采样是用标准数学系(逆时针)。
                // 要实现“正下为90”的效果，我们需要适配你的坐标系。

                // 方案：将计算出的标准数学角度，加上你的目标朝向
                // 如果 direction=90 (下), 我们希望原先的0度(右)变为90度(下)？
                // 还是说 direction=90 意味着长轴指着下方？
                // 通常做法：FinalAngle = RawAngle + Direction

                float finalAngle = rawAngleDeg + direction;


                BulletRuntimeInfo info = new BulletRuntimeInfo();
                info.shootPointIndex = shootPointIndex;
                info.wayIndex = i;
                info.orderInWay = 1;
                info.orderInOneShoot = i;
                info.orderInWave = timesInWave * num + i;
                info.speed = speed;

                //发射方向还要再加上一个shootDirection
                info.direction = dir + finalAngle;
                info.lifetime = 0;

                //pos加上偏移
                //BulletManager.Instance.AddBullet(pos, info);
                //Debug.Log("Add Bullet!");
                BulletDOTSManager.Instance.AddBullet(bulletTypeID, bulletBehaviourID, pos, info);
            }
        }
        
    }
}
