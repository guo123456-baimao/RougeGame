using System.Collections;
using UnityEngine;
public class GravityProjectile : Projectile
{
    [SerializeField] protected LayerMask gravityLayerMask;
    [SerializeField] protected float gravityRadius;
    [SerializeField] protected float gravityForce;
    [SerializeField] protected float gravityDuration;

    /// <summary>
    /// 命中目标后附着在该目标上持续产生引力场。
    /// </summary>
    protected override void HitDamageable(IDamageable damageable)
    {
        StartCoroutine(Gravity(damageable));
    }

    /// <summary>
    /// 未命中目标时在原地产生引力场。
    /// </summary>
    protected override void HitNothing()
    {
        StartCoroutine(Gravity());
    }

    /// <summary>
    /// 在持续时间内不断把范围内敌人吸向中心（命中目标时跟随其移动）。
    /// </summary>
    protected IEnumerator Gravity(IDamageable damageable = null)
    {
        float t = 0;
        Vector3 offset = Vector3.zero;
        if (damageable != null) offset = transform.position - damageable.transform.position;
        while (t < gravityDuration)
        {
            if (damageable != null)
                transform.position = damageable.transform.position + offset;

            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, gravityRadius, gravityLayerMask);
            foreach (Collider2D collider in hitColliders)
            {
                Vector2 dir = transform.position - collider.transform.position;
                float r2 = dir.magnitude*dir.magnitude;
                collider.GetComponentInParent<IDamageable>().Knockback(Time.deltaTime*gravityForce/r2*dir.normalized);
            }
            t += Time.deltaTime;
            yield return null;
        }
        entityManager.DespawnProjectile(projectileIndex, this);
    }
}
