using System.Collections.Generic;
using UnityEngine;
public class MolotovAbility : ThrowableAbility
{
    [Header("Molotov Stats")]
    [SerializeField] protected UpgradeableDuration duration;
    [SerializeField] protected UpgradeableAOE fireRadius;
    [SerializeField] protected UpgradeableDamageRate fireDamageRate;

    /// <summary>
    /// 生成燃烧瓶并朝最近敌人投出（没有敌人时随机方向）。
    /// </summary>
    protected override void LaunchThrowable()
    {
        MolotovThrowable throwable = (MolotovThrowable)entityManager.SpawnThrowable(throwableIndex, playerCharacter.CenterTransform.position, damage.Value, knockback.Value, 0, monsterLayer);
        throwable.SetupFire(duration.Value, fireRadius.Value, fireDamageRate.Value);
        // 朝最近的敌人投掷
        List<ISpatialHashGridClient> nearbyEnemies = entityManager.Grid.FindNearbyInRadius(playerCharacter.transform.position, throwRadius);
        Vector2 throwPosition;
        if (nearbyEnemies.Count > 0)
        {
            Vector2 closestPos = nearbyEnemies[0].Position;
            float minDist = Vector2.Distance(closestPos, (Vector2)playerCharacter.transform.position);
            foreach (var enemy in nearbyEnemies)
            {
                float d = Vector2.Distance(enemy.Position, (Vector2)playerCharacter.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    closestPos = enemy.Position;
                }
            }
            throwPosition = closestPos;
        }
        else
        {
            throwPosition = (Vector2)playerCharacter.transform.position + Random.insideUnitCircle * throwRadius;
        }
        throwable.Throw(throwPosition);
        throwable.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
    }
}
