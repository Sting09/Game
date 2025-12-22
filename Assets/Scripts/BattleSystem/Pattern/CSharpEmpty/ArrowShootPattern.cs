using UnityEngine;

public class ArrowShootPattern : ShootPattern
{
    private ArrowPatternSO arrowConfig;

    public ArrowShootPattern(ArrowPatternSO config) : base(config)
    {
        arrowConfig = config;
    }

    public override void ShootBullet(int shootPointIndex, int timesInWave, int waveTimes, Vector3 pos, float dir)
    {
        throw new System.NotImplementedException();
    }

    public override void UpdatePattern()
    {
        throw new System.NotImplementedException();
    }
}
