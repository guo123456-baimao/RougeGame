using System.Collections;
using UnityEngine;
using UnityEngine.Events;
public class Projectile : MonoBehaviour
{
    [SerializeField] protected SpriteRenderer projectileSpriteRenderer;
    [SerializeField] protected float maxDistance;
    [SerializeField] protected float rotationSpeed = 0;
    [SerializeField] protected float airResistance = 0;
    [SerializeField] protected ParticleSystem destructionParticleSystem;
    protected float despawnTime = 1;  // 离开屏幕后多久被回收
    protected LayerMask targetLayer;
    protected float speed;
    protected float damage;
    protected float knockback;
    protected EntityManager entityManager;
    protected Character playerCharacter;
    protected Collider2D col;
    protected ZPositioner zPositioner;
    protected Coroutine moveCoroutine;
    protected int projectileIndex;
    protected Vector2 direction;
    protected TrailRenderer trailRenderer = null;
    public UnityEvent<float> OnHitDamageable { get; private set; }

    /// <summary>
    /// 缓存碰撞体与 Z 轴排序组件，并尝试获取拖尾。
    /// </summary>
    protected virtual void Awake()
    {
        col = GetComponent<Collider2D>();
        zPositioner = gameObject.AddComponent<ZPositioner>();
        TryGetComponent<TrailRenderer>(out trailRenderer);
    }

    /// <summary>
    /// 注入实体管理器与玩家，并初始化 Z 轴排序。
    /// </summary>
    public virtual void Init(EntityManager entityManager, Character playerCharacter)
    {
        this.entityManager = entityManager;
        this.playerCharacter = playerCharacter;
        zPositioner.Init(playerCharacter.transform);
    }
    
    /// <summary>
    /// 重置位置、伤害、速度、目标层与命中事件等发射参数。
    /// </summary>
    public virtual void Setup(int projectileIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
    {
        transform.position = position;
        trailRenderer?.Clear();
        this.projectileIndex = projectileIndex;
        this.damage = damage;
        this.knockback = knockback;
        this.speed = speed;
        this.targetLayer = targetLayer;
        col.enabled = true;
        OnHitDamageable = new UnityEvent<float>();
    }

    /// <summary>
    /// 记录飞行方向并启动移动协程。
    /// </summary>
    public virtual void Launch(Vector2 direction)
    {
        this.direction = direction.normalized;
        //transform.rotation = Quaternion.LookRotation(direction, Vector3.back);
        moveCoroutine = StartCoroutine(Move());
    }

    /// <summary>
    /// 沿方向前进并受空气阻力减速，超出射程或速度耗尽后判定为未命中。
    /// </summary>
    public virtual IEnumerator Move()
    {
        float distanceTravelled = 0;
        float timeOffScreen = 0;
        while (distanceTravelled < maxDistance && timeOffScreen < despawnTime && speed > 0)
        {
            float step = speed * Time.deltaTime;
            transform.position += step * (Vector3)direction;
            distanceTravelled += step;
            transform.RotateAround(transform.position, Vector3.back, Time.deltaTime*100*rotationSpeed);
            // if (entityManager.TransformOnScreen(transform, Vector2.one))
            //     timeOffScreen = 0;
            // else
            //     timeOffScreen += Time.deltaTime;
            speed -= airResistance * Time.deltaTime;
            yield return null;
        }
        HitNothing();
    }

    /// <summary>
    /// 命中可伤害目标：结算伤害与击退后销毁弹丸。
    /// </summary>
    protected virtual void HitDamageable(IDamageable damageable)
    {
        damageable.TakeDamage(damage, knockback * direction);
        OnHitDamageable.Invoke(damage);
        DestroyProjectile();
    }

    /// <summary>
    /// 未命中任何目标时销毁弹丸。
    /// </summary>
    protected virtual void HitNothing()
    {
        DestroyProjectile();
    }

    /// <summary>
    /// 启动销毁流程。
    /// </summary>
    protected virtual void DestroyProjectile()
    {
        StartCoroutine(DestroyProjectileAnimation());
    }

    /// <summary>
    /// 隐藏贴图、播放销毁粒子，结束后把弹丸归还对象池。
    /// </summary>
    protected IEnumerator DestroyProjectileAnimation()
    {
        projectileSpriteRenderer.gameObject.SetActive(false);
        destructionParticleSystem.Play();
        yield return new WaitForSeconds(destructionParticleSystem.main.duration);
        projectileSpriteRenderer.gameObject.SetActive(true);
        entityManager.DespawnProjectile(projectileIndex, this);
    }

    /// <summary>
    /// 判断碰撞对象是否属于目标层，命中则停止移动并结算。
    /// </summary>
    protected void CollisionCheck(Collider2D collider)
    {
        if ((targetLayer & (1 << collider.gameObject.layer)) != 0)
        {
            col.enabled = false;
            StopCoroutine(moveCoroutine);
            if (collider.transform.parent.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                HitDamageable(collider.gameObject.GetComponentInParent<IDamageable>());
            }
            else
            {
                HitNothing();
            }
        }
    }

    /// <summary>
    /// 进入触发器时执行一次碰撞判定。
    /// </summary>
    protected virtual void OnTriggerEnter2D(Collider2D collider)
    {
        CollisionCheck(collider);
    }

    // void OnColliderEnter2D(Collider2D collider)
    // {
    //     CollisionCheck(collider);
    // }
}
