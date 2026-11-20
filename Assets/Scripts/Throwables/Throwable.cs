using System.Collections;
using UnityEngine;
using UnityEngine.Events;
public class Throwable : MonoBehaviour
{
    [SerializeField] protected SpriteRenderer throwableSpriteRenderer;
    [SerializeField] protected SpriteRenderer shadowSpriteRenderer;
    [SerializeField] protected float maxDistance;
    [SerializeField] protected int maxBounceCount = 4;
    [SerializeField] protected float throwHeight = 1;
    [SerializeField] protected float bounciness = 0.5f;
    [SerializeField] protected float delayTime = 1;
    [SerializeField] protected float initialRotationSpeed = 2;
    protected float rotationSpeed;
    protected LayerMask targetLayer;
    protected float damage;
    protected float knockback;
    protected EntityManager entityManager;
    protected Character playerCharacter;
    protected Collider2D col;
    protected ZPositioner zPositioner;
    protected int throwableIndex;
    protected float throwTime;
    public UnityEvent<float> OnHitDamageable { get; private set; }
    public float ThrowTime { get => throwTime; }
    public float Range => maxDistance;

    /// <summary>
    /// 缓存碰撞体并挂上 Z 轴排序组件。
    /// </summary>
    protected virtual void Awake()
    {
        col = GetComponent<Collider2D>();
        zPositioner = gameObject.AddComponent<ZPositioner>();
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
    /// 重置位置、伤害、击退、目标层等投掷参数，并预估飞行时长。
    /// </summary>
    public virtual void Setup(int throwableIndex, Vector2 position, float damage, float knockback, float timeInAir, LayerMask targetLayer)
    {
        transform.position = position;
        this.throwableIndex = throwableIndex;
        this.damage = damage;
        this.knockback = knockback;
        this.targetLayer = targetLayer;
        col.enabled = true;
        OnHitDamageable = new UnityEvent<float>();
        throwTime = ComputeThrowTime();
    }

    /// <summary>
    /// 朝目标点投掷（距离受最大投掷距离限制），并启动投掷协程。
    /// </summary>
    public virtual void Throw(Vector2 toPosition)
    {
        Vector2 direction = (toPosition - (Vector2)transform.position);
        rotationSpeed = (direction.x > 0) ? initialRotationSpeed : -initialRotationSpeed;
        float throwDistance = direction.magnitude;
        if (throwDistance > maxDistance) throwDistance = maxDistance;
        direction.Normalize();
        StartCoroutine(ThrowRoutine(direction, throwDistance));
    }

    /// <summary>
    /// 抛物线弹跳飞行并落地滚动，结束后触发爆炸。
    /// </summary>
    public virtual IEnumerator ThrowRoutine(Vector2 direction, float throwDistance)
    {
        float distance = 0;
        int bounceCount = 0;
        Vector3 initialPosition = transform.position;
        // 初始化竖直方向与水平方向速度
        float vy = Mathf.Sqrt(2*PhysicsConstants.g*throwHeight);
        float initialLinearVelocity = InitialVelocityForThrowDistance(throwDistance, vy);
        float linearSpeed = initialLinearVelocity;
        float y = 0;
        while (distance < throwDistance && linearSpeed > 0)
        {
            // 水平方向线性推进位置
            Vector3 linearPosition = initialPosition + distance * (Vector3)direction;

            // 用物理公式计算高度，模拟弹跳
            vy -= PhysicsConstants.g*Time.deltaTime;
            y += vy * Time.deltaTime;
            if (y < 0)
            {
                y = 0;
                vy = -vy * bounciness;
                bounceCount++;
                linearSpeed = (initialLinearVelocity * (maxBounceCount-bounceCount))/maxBounceCount;
                rotationSpeed *= bounciness;
            }
            transform.position = linearPosition + Vector3.up * y;
            zPositioner.ManuallySetZByY(linearPosition.y);
            if (bounceCount >= maxBounceCount)
                break;

            // 旋转
            throwableSpriteRenderer.transform.RotateAround(throwableSpriteRenderer.transform.position, Vector3.back, Time.deltaTime*100*rotationSpeed);

            // 阴影
            shadowSpriteRenderer.transform.position = linearPosition;
            shadowSpriteRenderer.transform.rotation = Quaternion.identity;

            distance += linearSpeed * Time.deltaTime;
            yield return null;
        }
        zPositioner.AutomaticallySetZ();
        float t = 0;
        float rollingSpeed = 1.0f/maxBounceCount*initialLinearVelocity;
        while (t < delayTime)
        {
            Vector3 linearPosition = initialPosition + distance * (Vector3)direction;
            transform.position = linearPosition;
            shadowSpriteRenderer.transform.position = linearPosition;
            shadowSpriteRenderer.transform.rotation = Quaternion.identity;
            t += Time.deltaTime;
            distance += rollingSpeed * Time.deltaTime;
            throwableSpriteRenderer.transform.RotateAround(throwableSpriteRenderer.transform.position, Vector3.back, Time.deltaTime*100*rotationSpeed);
            rollingSpeed *= 0.95f;
            rotationSpeed *= 0.99f;
            yield return null;
        }
        Explode();
    }

    /// <summary>
    /// 根据目标距离、初速度与弹性系数反推所需的水平初速度。
    /// </summary>
    private float InitialVelocityForThrowDistance(float throwDistance, float vy)
    {
        // 每次弹跳后速度与高度递减的调和级数
        float H = 1;
        for (int i = 1; i <= maxBounceCount; i++)
        {
            H += Mathf.Pow(bounciness, i) * (1 - ((float)i)/maxBounceCount);
        }
        // 给定重力、初速度与弹性系数时，达到目标投掷距离所需的总速度
        return PhysicsConstants.g*throwDistance/(2*vy*H);
    }

    /// <summary>
    /// 估算包含所有弹跳在内的总飞行时长。
    /// </summary>
    protected float ComputeThrowTime()
    {
        float vy = Mathf.Sqrt(2*PhysicsConstants.g*throwHeight);
        float t = 0;
        for (int i = 1; i <= maxBounceCount; i++)
        {
            t += Mathf.Pow(bounciness,i) * 2*vy/PhysicsConstants.g;
        }
        return t;
    }

    /// <summary>
    /// 落地后的爆炸逻辑，默认直接销毁投掷物。
    /// </summary>
    protected virtual void Explode()
    {
        DestroyThrowable();
    }

    /// <summary>
    /// 启动消失流程。
    /// </summary>
    protected virtual void DestroyThrowable()
    {
        StartCoroutine(DestroyThrowableAnimation());
    }

    /// <summary>
    /// 隐藏贴图后把投掷物归还对象池。
    /// </summary>
    protected IEnumerator DestroyThrowableAnimation()
    {
        throwableSpriteRenderer.enabled = false;
        //destructionParticleSystem.Play();
        yield return new WaitForSeconds(0.0f);
        throwableSpriteRenderer.enabled = true;
        entityManager.DespawnThrowable(throwableIndex, this);
    }
}
