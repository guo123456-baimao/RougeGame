using System.Collections;
using UnityEngine;
using UnityEngine.Events;
public class MolotovThrowable : Throwable
{
    [SerializeField] protected MolotovFire molotovFire;
    [SerializeField] protected ParticleSystem molotovExplosion;
    protected float duration;
    protected float fireRadius;
    protected float fireDamageRate;

    /// <summary>
    /// 设置燃烧持续时间、火焰半径与点燃频率。
    /// </summary>
    public void SetupFire(float duration, float fireRadius, float fireDamageRate)
    {
        this.duration = duration;
        this.fireRadius = fireRadius;
        this.fireDamageRate = fireDamageRate;
    }

    /// <summary>
    /// 落地后转为启动燃烧流程，而不是直接销毁。
    /// </summary>
    protected override void Explode()
    {
        StartCoroutine(Burn());
    }

    /// <summary>
    /// 展开火焰持续灼烧敌人，燃烧结束后收起特效并回收自身。
    /// </summary>
    protected IEnumerator Burn()
    {
        throwableSpriteRenderer.enabled = false;
        shadowSpriteRenderer.enabled = false;
        molotovFire.gameObject.SetActive(true);
        molotovExplosion.Play();
        yield return StartCoroutine(molotovFire.Burn(this, damage, knockback, duration, fireRadius, fireDamageRate, targetLayer));
        molotovFire.gameObject.SetActive(false);
        throwableSpriteRenderer.enabled = true;
        shadowSpriteRenderer.enabled = true;
        DestroyThrowable();
    }

    /// <summary>
    /// 对单个目标结算火焰伤害并向上层广播造成伤害事件。
    /// </summary>
    public void Damage(IDamageable damageable)
    {
        Vector2 knockbackDirection = (damageable.transform.position - transform.position).normalized;
        damageable.TakeDamage(damage, knockback * knockbackDirection);
        playerCharacter.OnDealDamage.Invoke(damage);
    }
}
