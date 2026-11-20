using System.Collections;
using UnityEngine;
public class GunAbility : ProjectileAbility
{
    [Header("Gun Stats")]
    [SerializeField] protected UpgradeableProjectileCount projectileCount;
    [SerializeField] protected UpgradeableDamageRate firerate;

    /// <summary>
    /// 开始一轮弹夹射击。
    /// </summary>
    protected override void Attack()
    {
        StartCoroutine(FireClip());
    }

    /// <summary>
    /// 按射速逐发开火，打空后等待射击间隔重置冷却。
    /// </summary>
    protected IEnumerator FireClip()
    {
        int clipSize = projectileCount.Value;
        timeSinceLastAttack -= clipSize/firerate.Value;
        OnClipStart();
        for (int i = 0; i < clipSize; i++)
        {
            LaunchProjectile();
            yield return new WaitForSeconds(1/firerate.Value);
        }
        OnClipEnd();
    }

    /// <summary>
    /// 弹夹开始射击 / 打完进入换弹时的回调，供子类挂循环音效等
    /// </summary>
    protected virtual void OnClipStart() { }
    /// <summary>
    /// 弹夹结束时的回调，供子类扩展。
    /// </summary>
    protected virtual void OnClipEnd() { }
}
