using System;
using UnityEngine;
using UnityEngine.Pool;


//对象池模板
//获得一个对象：拖拽赋值给某个Manager，然后GameObject obj = xxxPool.GetObjectFromPool();
//归还一个对象：xxxPool.ReturnObjectPool(obj);
//也可以改写成单例模式，不用获取组件，直接获得归还

public class PoolTool : MonoBehaviour
{
    public GameObject objPrefab;    //ObjectPool里都是这个对象
    public int fillNum = 600;     //Awake时填充的物体数量
    public bool ifPreFill = false;  //是否需要预填充
    public GameObject parent;   //生成的对象挂到这个物体下，为空则挂在ObjectPool所在物体下
    private ObjectPool<GameObject> pool;

    private void Awake()
    {
        //初始化对象池
        //对象的父物体是自己
        pool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(objPrefab, parent == null ? transform : parent.transform),
            actionOnGet:obj => { obj.SetActive(true); },
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy:(obj) => Destroy(obj)
        );

        if (ifPreFill) { PreFillPool(fillNum); }
    }

    private void PreFillPool(int count)
    {
        var tempBuffer = new System.Collections.Generic.List<GameObject>(count);
        for (int i = 0; i < count; i++)
        {
            tempBuffer.Add(pool.Get());
        }
        for (int i = 0; i < tempBuffer.Count; i++)
        {
            pool.Release(tempBuffer[i]);
        }
    }

    public GameObject GetObj()
    {
        return pool.Get();
    }

    public void ReturnObj(GameObject obj)
    {
        pool.Release(obj);
    }
}
