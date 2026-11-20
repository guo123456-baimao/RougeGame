using System.Collections;
using UnityEngine;
public class ShurikenAbility : ProjectileAbility
{
    [Header("Shuriken Stats")]
    [SerializeField] protected UpgradeableProjectileCount projectileCount;
    [SerializeField] protected float shurikenDelay;

    /// <summary>
    /// 投出一组手里剑。
    /// </summary>
    protected override void Attack()
    {
        StartCoroutine(LuanchShurikens());
    }

    /// <summary>
    /// 按弹丸数量扇形发射手里剑，随后进入冷却。
    /// </summary>
    protected IEnumerator LuanchShurikens()
    {
        timeSinceLastAttack -= projectileCount.Value*shurikenDelay;
        for (int i = 0; i < projectileCount.Value; i++)
        {
            LaunchProjectile(FindNearestEnemyDirection());
            yield return new WaitForSeconds(shurikenDelay);
        }
    }

    /// <summary>
    /// 朝指定方向生成并发射一枚手里剑。
    /// </summary>
    protected void LaunchProjectile(Vector2 direction)
    {
        Projectile projectile = entityManager.SpawnProjectile(projectileIndex, playerCharacter.CenterTransform.position, damage.Value, knockback.Value, speed.Value, monsterLayer);
        projectile.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
        projectile.Launch(direction);
    }
}
