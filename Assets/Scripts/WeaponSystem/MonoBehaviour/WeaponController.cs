// WeaponController.cs
using UnityEngine;

namespace WeaponSystem
{
    /// <summary>
    /// 武器控制器。持有武器数据，维护运行时的状态变量，并接收玩家输入。
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Header("当前装备武器")]
        public WeaponData currentWeapon;
        public OptionManager optionManager;

        // --- 运行时状态 (对策划隐藏，供 Strategy 使用) ---
        [HideInInspector] public float ActionTimer;
        [HideInInspector] public float ChargeTimer;
        [HideInInspector] public bool IsFireInputHeld;
        [HideInInspector] public bool WasFireInputReleasedThisFrame;

        // 外部依赖项引用 (如玩家属性、外部输入模块)
        // private PlayerInputManager _inputManager;
        // private PlayerStats _stats;

        private void Start()
        {
            // 初始化逻辑，比如加载战斗场景时装备默认武器
            EquipWeapon(currentWeapon);
        }

        public void EquipWeapon(WeaponData newWeapon)
        {
            if (newWeapon == null) return;
            currentWeapon = newWeapon;

            // 切换武器时重置状态
            ActionTimer = 0f;
            ChargeTimer = 0f;

            // 设置子机数量
            optionManager.SetOptionCount(currentWeapon.BaseOptionCount);

            Debug.Log($"[系统] 装备了新武器: {currentWeapon.WeaponName}");
        }

        private void Update()
        {
            // 1. 获取输入 (实际开发中请替换为你的 InputSystem 或 InputManager)
            // 这里使用鼠标左键或空格键作为演示
            IsFireInputHeld = Input.GetKey(KeyCode.Z);
            WasFireInputReleasedThisFrame = Input.GetKeyUp(KeyCode.Z);

            // 2. 执行武器策略
            if (currentWeapon != null && currentWeapon.ShootStrategy != null)
            {
                currentWeapon.ShootStrategy.Tick(this, Time.deltaTime);
            }
        }

        /// <summary>
        /// 执行开火。此方法由 Strategy 在条件满足时调用。
        /// </summary>
        public void PerformShoot()
        {
            // 构造 Context
            ShootContext context = new ShootContext
            {
                PlayerPosition = transform.position,
                AimDirection = transform.up, // 假设玩家的 up 方向是瞄准方向
                DamageMultiplier = 1.0f,     // 之后接入 Roguelike 道具系统进行修饰
                OptionCount = currentWeapon.BaseOptionCount,
                ChargeNormalized = currentWeapon.ShootStrategy is ChargeShootStrategy chargeStrategy ?
                                   (ChargeTimer / chargeStrategy.MaxChargeTime) : 1f
            };

            optionManager.AllOptionsFire(context);

            // Debug.Log($"<color=orange>[开火!] {CurrentWeapon.WeaponName} 进行了射击! 当前子机数: {context.OptionCount}</color>");
        }
    }
}