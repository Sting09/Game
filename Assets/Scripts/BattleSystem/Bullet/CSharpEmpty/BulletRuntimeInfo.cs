using System.Collections.Generic;
using UnityEngine;

public struct BulletRuntimeInfo
{
    public int shootPointIndex;    //第几个发弹点发射的
    public int wayIndex;           //位于第几条子弹
    public int orderInWay;         //位于这一条的
    public int orderInOneShoot;    //本次发射中的子弹中，是第几个被发射的
    public int orderInWave;         //本波发射中的子弹中，是第几次被发射的

    public float speed;            //子弹的速度大小
    public float direction;        //子弹的方向

    public float lifetime;          //已经生成了多少秒？

    //public List<BulletEventRuntime> events;     //所有子弹事件
}

