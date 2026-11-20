using System.Collections;
using UnityEngine;
using UnityEngine.Events;
public class GrenadeThrowable : Throwable
{
    [SerializeField] GameObject fragmentPrefab;
    [SerializeField] protected float fragmentSpeed;
    [SerializeField] protected int fragmentCount;
    protected int projectileIndex = -1;

    /// <summary>
    /// 注册破片弹丸的对象池并完成基类初始化。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter)
    {
        base.Init(entityManager, playerCharacter);
        projectileIndex = entityManager.AddPoolForProjectile(fragmentPrefab);
    }

    /// <summary>
    /// 设置爆炸产生的破片数量。
    /// </summary>
    public void SetupGrenade(int fragmentCount)
    {
        this.fragmentCount = fragmentCount;
    }

    /// <summary>
    /// 向四周均匀发射破片，播放爆炸音效与震屏后回收自身。
    /// </summary>
    protected override void Explode()
    {
        AudioManager.Instance.PlayExplosion();
        CameraShake.ShakeLight();
        for (int i = 0; i < fragmentCount; i++)
        {
            float theta = i * Mathf.PI * 2.0f / fragmentCount;
            Vector2 direction = new Vector2(Mathf.Sin(theta), Mathf.Cos(theta));
            Projectile projectile = entityManager.SpawnProjectile(projectileIndex, transform.position, damage, knockback, fragmentSpeed, targetLayer);
            projectile.OnHitDamageable.AddListener(OnHitDamageable.Invoke);
            projectile.Launch(direction);
        }
        DestroyThrowable();
    }
}
