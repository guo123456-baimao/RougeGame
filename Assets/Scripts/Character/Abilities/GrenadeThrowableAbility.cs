using System.Collections;
using UnityEngine;
public class GrenadeThrowableAbility : ThrowableAbility
{
    [Header("Grenade Stats")]
    [SerializeField] protected UpgradeableProjectileCount fragmentCount;

    /// <summary>
    /// 生成手雷、设置破片数量后朝附近随机位置投出。
    /// </summary>
    protected override void LaunchThrowable()
    {
        GrenadeThrowable throwable = (GrenadeThrowable) entityManager.SpawnThrowable(throwableIndex, playerCharacter.CenterTransform.position, damage.Value, knockback.Value, 0, monsterLayer);
        throwable.SetupGrenade(fragmentCount.Value);
        throwable.Throw((Vector2)playerCharacter.transform.position + Random.insideUnitCircle * throwRadius);
        throwable.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
    }
}
