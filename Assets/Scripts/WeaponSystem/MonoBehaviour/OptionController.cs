using UnityEngine;

namespace WeaponSystem
{
    public class OptionController : MonoBehaviour
    {
        // 供 SmoothDamp 使用的引用变量，必须保存在实例上
        [HideInInspector] public Vector3 CurrentVelocity;
        public AbstractEmitterConfigSO highSpeedEmitterConfig;
        private EmitterRuntime highSpeedEmitter;

        public AbstractEmitterConfigSO lowSpeedEmitterConfig;
        private EmitterRuntime lowSpeedEmitter;

        public bool isHighSpeed;         //是否处于低速模式

        // 子机的序号 (0, 1, 2...)，用于排兵布阵（如一字排开时的偏移）
        public int Index { get; private set; }

        public void Initialize(int index)
        {
            Index = index;
            CurrentVelocity = Vector3.zero;
            highSpeedEmitter = highSpeedEmitterConfig.CreateRuntime();
            lowSpeedEmitter = lowSpeedEmitterConfig.CreateRuntime();
        }

        public void SetSpeedState(bool isHighSpeed)
        {
            this.isHighSpeed = isHighSpeed;
        }

        // 占位：用于触发特效或开火
        public void Fire(ShootContext context, bool isHighSpeed)
        {
            // Debug.Log($"子机{Index}开火");
            if (isHighSpeed)
            {
                highSpeedEmitter.Shoot(transform, true);
            }
            else
            {
                lowSpeedEmitter.Shoot(transform, true);
            }


        }
    }
}