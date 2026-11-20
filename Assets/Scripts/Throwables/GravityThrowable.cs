using System.Collections;
using UnityEngine;
using UnityEngine.Events;
public class GravityThrowable : Throwable
{
    [SerializeField] protected GameObject gravityWell;
    [SerializeField] protected ParticleSystem wellParticles;
    [SerializeField] protected float gravityRadius = 1;
    [SerializeField] protected float gravityForce = 1;
    [SerializeField] protected float gravityDuration = 1;
    [SerializeField] protected float cutoffRadius = 0.2f;

    /// <summary>
    /// 落地后转为启动引力场流程。
    /// </summary>
    protected override void Explode()
    {
        StartCoroutine(Gravity());
    }

    /// <summary>
    /// 引力场扩张、持续吸引、再收缩，结束后回收投掷物。
    /// </summary>
    protected IEnumerator Gravity()
    {
        gravityWell.SetActive(true);
        throwableSpriteRenderer.enabled = false;
        wellParticles.enableEmission = true;
        float currentRadius = 0;
        float t = 0;
        while (t < 1.0f)
        {
            currentRadius = gravityRadius * t;
            gravityWell.transform.localScale = Vector2.one*currentRadius*2;
            ApplyGravity(currentRadius);
            t += Time.deltaTime * 3;
            yield return null;
        }
        gravityWell.transform.localScale = Vector2.one*gravityRadius*2;
        t = 0;
        while (t < gravityDuration)
        {
            ApplyGravity(gravityRadius);
            t += Time.deltaTime;
            yield return null;
        }
        t = 1.0f;
        while (t > 0)
        {
            currentRadius = gravityRadius * t;
            gravityWell.transform.localScale = Vector2.one*currentRadius*2;
            ApplyGravity(currentRadius);
            t -= Time.deltaTime * 3;
            yield return null;
        }
        gravityWell.transform.localScale = Vector2.zero;
        wellParticles.enableEmission = false;
        gravityWell.SetActive(false);
        throwableSpriteRenderer.enabled = true;
        DestroyThrowable();
    }

    /// <summary>
    /// 对范围内敌人施加指向中心的引力位移，过近的目标不再吸附。
    /// </summary>
    protected void ApplyGravity(float radius)
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, radius, targetLayer);
        foreach (Collider2D collider in hitColliders)
        {
            Vector2 dir = transform.position - collider.transform.position;
            float r2 = dir.magnitude;//*dir.magnitude;
            if (r2 > cutoffRadius)
                collider.GetComponentInParent<IDamageable>().Knockback(Time.deltaTime*gravityForce/r2*dir.normalized);
        }
    }
}
