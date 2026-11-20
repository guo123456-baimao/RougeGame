using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class ExplosiveProjectile : Projectile
{
    [SerializeField] protected LayerMask explosionLayerMask;
    [SerializeField] protected float explosionRadius;
    [SerializeField] protected float explosionDamage;
    [SerializeField] protected float explosionKnockback;
    [SerializeField] protected float explosionDuration = 0.25f;
    [SerializeField] protected SpriteRenderer explosionSpriteRenderer;

    /// <summary>
    /// 设置爆炸伤害、爆炸半径与爆炸击退。
    /// </summary>
    public void SetupExplosion(float explosionDamage, float explosionRadius, float explosionKnockback)
    {
        this.explosionDamage = explosionDamage;
        this.explosionRadius = explosionRadius;
        this.explosionKnockback = explosionKnockback;
    }

    /// <summary>
    /// 命中目标时不结算单体伤害，改为引爆。
    /// </summary>
    protected override void HitDamageable(IDamageable damageable)
    {
        // 爆炸
        StartCoroutine(Explosion());
    }

    /// <summary>
    /// 未命中目标时同样引爆。
    /// </summary>
    protected override void HitNothing()
    {
        // 爆炸
        StartCoroutine(Explosion());
    }

    /// <summary>
    /// 逐帧扩大检测半径，对范围内敌人各造成一次爆炸伤害并播放特效。
    /// </summary>
    protected IEnumerator Explosion()
    {
        AudioManager.Instance.PlayExplosion();
        CameraShake.ShakeLight();
        projectileSpriteRenderer.gameObject.SetActive(false);
        destructionParticleSystem.Play();
        float t = 0;
        Dictionary<Collider2D, bool> damagedColliders = new Dictionary<Collider2D, bool>();
        while (t < 1)
        {
            Color c = explosionSpriteRenderer.color;
            c.a = EasingUtils.EaseOutQuart(1-t)*0.5f;
            explosionSpriteRenderer.color = c;
            explosionSpriteRenderer.transform.localScale = Vector3.one * EasingUtils.EaseOutQuart(t) * explosionRadius * 10;
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, t*explosionRadius, explosionLayerMask);
            foreach (Collider2D collider in hitColliders)
            {
                if (!damagedColliders.ContainsKey(collider))
                {
                    Vector2 dir = collider.transform.position - transform.position;
                    collider.GetComponentInParent<IDamageable>().TakeDamage(explosionDamage, dir.normalized * explosionKnockback);
                    damagedColliders[collider] = true;
                    OnHitDamageable.Invoke(explosionDamage);
                }
            }
            t += Time.deltaTime/explosionDuration;
            yield return null;
        }
        explosionSpriteRenderer.color = explosionSpriteRenderer.color - Color.black;
        if (explosionDuration < destructionParticleSystem.main.duration)
            yield return new WaitForSeconds(destructionParticleSystem.main.duration - explosionDuration);
        projectileSpriteRenderer.gameObject.SetActive(true);
        entityManager.DespawnProjectile(projectileIndex, this);
    }
}
