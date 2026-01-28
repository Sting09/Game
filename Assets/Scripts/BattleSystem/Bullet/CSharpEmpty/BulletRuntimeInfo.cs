using System.Collections.Generic;
using UnityEngine;

public struct BulletRuntimeInfo
{
    //样式信息
    public int shootPointIndex;    //第几个发弹点发射的
    public int wayIndex;           //位于第几条子弹
    public int orderInWay;         //位于这一条的第几个
    public int waveTimes;           //第几波发射的
    public int timesInWave;         //本波次中第几次发射的
    public int orderInOneShoot;    //本次发射中的子弹中，是第几个被发射的
    public int orderInWave;         //本波发射中的子弹中，是第几次被发射的

    //子弹运动信息
    public float speed;            //子弹的速度大小
    public float direction;        //子弹的方向
    public bool isRelative;         //是否跟随发弹源移动
    public Transform parentTransform;   //发弹源Transform

    //子弹生命信息
    public float lifetime;          //已经生成了多少秒？
    public float totalLifetime;     //最大持续多少秒
    
}

