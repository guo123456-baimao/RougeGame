using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
public class Boomerang : MonoBehaviour
{
    [SerializeField] protected SpriteRenderer boomerangSpriteRenderer;
    [SerializeField] protected float rotationSpeed = 2;
    [SerializeField] protected float damageDelay = 0.1f;
    protected Vector2 initialScale;
    protected float throwTime = 1;
    protected float maxDistance;
    protected float radius;
    protected LayerMask targetLayer;
    protected float damage;
    protected float knockback;
    protected EntityManager entityManager;
    protected Character playerCharacter;
    protected ZPositioner zPositioner;
    protected int boomerangIndex;
    protected TrailRenderer trailRenderer = null;
    public UnityEvent<float> OnHitDamageable { get; private set; }
    public float Range => maxDistance;
    

    /// <summary>
    /// 按贴图尺寸计算回旋镖半径，缓存初始缩放并挂上 Z 轴排序组件。
    /// </summary>
    protected virtual void Awake()
    {
        radius = Mathf.Max(boomerangSpriteRenderer.bounds.size.x, boomerangSpriteRenderer.bounds.size.y)/2;
        initialScale = boomerangSpriteRenderer.transform.localScale;
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
    /// 重置位置、伤害、投掷距离与目标层等参数。
    /// </summary>
    public virtual void Setup(int boomerangIndex, Vector2 position, float damage, float knockback, float throwDistance, float throwTime, LayerMask targetLayer)
    {
        transform.position = position;
        trailRenderer?.Clear();
        this.boomerangIndex = boomerangIndex;
        this.damage = damage;
        this.knockback = knockback;
        this.targetLayer = targetLayer;
        this.maxDistance = throwDistance;
        this.throwTime = throwTime;
        OnHitDamageable = new UnityEvent<float>();
    }

    /// <summary>
    /// 朝目标点方向投出回旋镖。
    /// </summary>
    public virtual void Throw(Transform returnTransform, Vector2 toPosition)
    {
        Vector2 direction = (toPosition - (Vector2)transform.position);
        // float throwDistance = direction.magnitude;
        // if (throwDistance > maxDistance) throwDistance = maxDistance;
        direction.Normalize();
        StartCoroutine(ThrowRoutine(returnTransform, direction, maxDistance));
    }

    /// <summary>
    /// 先缓动飞出、再缓动飞回投掷者，途中用圆形射线检测持续判定命中。
    /// </summary>
    public virtual IEnumerator ThrowRoutine(Transform returnTransform, Vector2 direction, float throwDistance)
    {
        //FastList<GameObject> hitMonsters = new FastList<GameObject>();
        Dictionary<GameObject, float> hitMonsterTimes = new Dictionary<GameObject, float>();
        
        Vector2 prevPosition = transform.position;
        Vector2 a = transform.position;
        Vector2 c = (Vector2)transform.position + direction * throwDistance;
        
        float t = 0;
        while (t < 1)
        {
            transform.position = Vector2.Lerp(a, c, EasingUtils.EaseOutQuad(t));

            Vector2 circleCastDir = (Vector2)transform.position-prevPosition;
            RaycastHit2D[] raycastHits = Physics2D.CircleCastAll(prevPosition, radius, circleCastDir.normalized, circleCastDir.magnitude, targetLayer);
            foreach (RaycastHit2D raycastHit in raycastHits)
            {
                GameObject hitGameObject = raycastHit.collider.gameObject;
                if (!hitMonsterTimes.ContainsKey(hitGameObject) || hitMonsterTimes[hitGameObject] > damageDelay)
                {
                    hitMonsterTimes[hitGameObject] = 0.0f;
                    IDamageable damageable = hitGameObject.GetComponentInParent<IDamageable>();
                    damageable.TakeDamage(damage, circleCastDir.normalized*knockback);
                    OnHitDamageable.Invoke(damage);
                }
            }
            prevPosition = transform.position;

            // 旋转
            boomerangSpriteRenderer.transform.RotateAround(boomerangSpriteRenderer.transform.position, Vector3.back, Time.deltaTime*100*rotationSpeed);

            // 缩放
            boomerangSpriteRenderer.transform.localScale = Vector3.Lerp(Vector3.zero, initialScale, t*5);

            GameObject[] keys = hitMonsterTimes.Keys.ToArray();
            foreach (GameObject key in keys)
            {
                hitMonsterTimes[key] += Time.deltaTime;
            }

            t += Time.deltaTime/throwTime;
            yield return null;
        }
        
        //hitMonsters = new FastList<GameObject>();
        t = 0;//0.1f;
        while (t < 1)
        {
            a = returnTransform.position;

            transform.position = Vector2.Lerp(c, a, EasingUtils.EaseInQuad(t));;

            Vector2 circleCastDir = (Vector2)transform.position-prevPosition;
            RaycastHit2D[] raycastHits = Physics2D.CircleCastAll(prevPosition, radius, circleCastDir.normalized, circleCastDir.magnitude, targetLayer);
            foreach (RaycastHit2D raycastHit in raycastHits)
            {
                GameObject hitGameObject = raycastHit.collider.gameObject;
                if (!hitMonsterTimes.ContainsKey(hitGameObject) || hitMonsterTimes[hitGameObject] > damageDelay)
                {
                    hitMonsterTimes[hitGameObject] = 0.0f;
                    IDamageable damageable = hitGameObject.GetComponentInParent<IDamageable>();
                    damageable.TakeDamage(damage);//-circleCastDir.normalized*knockback);
                    OnHitDamageable.Invoke(damage);
                }
            }
            prevPosition = transform.position;

            // 旋转
            boomerangSpriteRenderer.transform.RotateAround(boomerangSpriteRenderer.transform.position, Vector3.back, Time.deltaTime*100*rotationSpeed);

            // 缩放
            boomerangSpriteRenderer.transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, (t-0.8f)*5);

            GameObject[] keys = hitMonsterTimes.Keys.ToArray();
            foreach (GameObject key in keys)
            {
                hitMonsterTimes[key] += Time.deltaTime;
            }

            t += Time.deltaTime/throwTime;
            yield return null;
        }

        DestroyThrowable();
    }

    /// <summary>
    /// 启动回旋镖的消失流程。
    /// </summary>
    protected virtual void DestroyThrowable()
    {
        StartCoroutine(DestroyThrowableAnimation());
    }

    /// <summary>
    /// 隐藏贴图后把回旋镖归还对象池。
    /// </summary>
    protected IEnumerator DestroyThrowableAnimation()
    {
        boomerangSpriteRenderer.enabled = false;
        yield return new WaitForSeconds(0.0f);
        boomerangSpriteRenderer.enabled = true;
        entityManager.DespawnBoomerang(boomerangIndex, this);
    }
}
