using System.Collections;
using UnityEngine;
public class MachineGunAbility : GunAbility
{
    [Header("Machine Gun Stats")]
    [SerializeField] protected GameObject machineGun;
    [SerializeField] protected Transform launchTransform;
    [SerializeField] protected UpgradeableRotationSpeed rotationSpeed;
    [SerializeField] protected float gunRadius;
    protected Vector3 gunDirection = Vector2.right;

    /// <summary>
    /// 装填期间旋转枪械；装填完成且冷却就绪时开始连射。
    /// </summary>
    protected override void Update()
    {
        base.Update();

        // 装填期间旋转枪械
        float reloadRotation = 0;
        float t = timeSinceLastAttack/cooldown.Value;
        if (t > 0 && t < 1)
        {
            reloadRotation = t * 360;
        }

        float theta = Time.time*rotationSpeed.Value;
        gunDirection = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0);
        machineGun.transform.position = playerCharacter.CenterTransform.position + gunDirection * gunRadius;
        machineGun.transform.rotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * theta - reloadRotation);
    }

    /// <summary>
    /// 朝最近敌人发射一发子弹。
    /// </summary>
    protected override void LaunchProjectile()
    {
        Projectile projectile = entityManager.SpawnProjectile(projectileIndex, launchTransform.position, damage.Value, knockback.Value, speed.Value, monsterLayer);
        projectile.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
        projectile.Launch(gunDirection);
    }

    /// <summary>
    /// 开火音在整个弹夹期间循环，打完进入换弹时停
    /// </summary>
    protected override void OnClipStart()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMachineGunLoop();
    }

    /// <summary>
    /// 弹夹结束时停止射击音效。
    /// </summary>
    protected override void OnClipEnd()
    {
        StopFireLoop();
    }

    /// <summary>
    /// 协程可能被禁用/销毁打断，兜底停掉循环音
    /// </summary>
    private void OnDisable()
    {
        StopFireLoop();
    }

    /// <summary>
    /// 对象销毁时停止射击音效。
    /// </summary>
    private void OnDestroy()
    {
        StopFireLoop();
    }

    /// <summary>
    /// 停止循环射击音效。
    /// </summary>
    private void StopFireLoop()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopMachineGunLoop();
    }
}
