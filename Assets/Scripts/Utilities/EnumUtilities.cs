using UnityEngine;
using System;

public static class EnumUtilities
{
    // 泛型方法：传入任何枚举类型 T
    public static T GetRandom<T>()
    {
        // 这里的 typeof(T) 就是你传入的枚举类型
        Array values = Enum.GetValues(typeof(T));

        // 随机取值并强制转换为 T 类型
        return (T)values.GetValue(UnityEngine.Random.Range(0, values.Length));
    }
}