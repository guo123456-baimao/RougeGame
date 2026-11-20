using System.Collections;
using UnityEngine;
public class SlashAbility : MeleeAbility
{
    [Header("Slash Stats")]
    [SerializeField] protected float slashAngle;
    [SerializeField] protected float slashOffset; 
    [SerializeField] protected float slashTime;
    [SerializeField] protected float scaleInTime = 0.1f;
    protected Vector2 weaponSize;
    protected Vector2 initialWeaponScale;
    private FastList<GameObject> hitMonsters;

    /// <summary>
    /// 启动挥砍循环。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        weaponSize = weaponSpriteRenderer.bounds.size;
        initialWeaponScale = weaponSpriteRenderer.transform.localScale;
        weaponSpriteRenderer.enabled = false;
    }

    /// <summary>
    /// 按冷却触发一次挥砍。
    /// </summary>
    protected override void Attack()
    {
        StartCoroutine(Slash());
    }

    /// <summary>
    /// 挥砍表现：弧线展开并扩大判定，命中范围内敌人后淡出。
    /// </summary>
    protected IEnumerator Slash()
    {
        hitMonsters = new FastList<GameObject>();
        timeSinceLastAttack -= slashTime;
        float t = 0;
        weaponSpriteRenderer.enabled = true;
        Vector2 initialDir = FindNearestEnemyDirection();
        while (t < slashTime)
        {
            float scaleMultiplier = GetScaleMultiplier(t);
            float theta = slashAngle * (slashTime/2 - t) / slashTime;
            Vector2 dir = new Vector2(
                initialDir.x * Mathf.Cos(Mathf.Deg2Rad * theta) - initialDir.y * Mathf.Sin(Mathf.Deg2Rad * theta),
                initialDir.x * Mathf.Sin(Mathf.Deg2Rad * theta) + initialDir.y * Mathf.Cos(Mathf.Deg2Rad * theta)
            );
            Vector2 attackBoxPosition = (Vector2)playerCharacter.CenterTransform.position + dir * (scaleMultiplier*weaponSize.x/2 + slashOffset);
            Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackBoxPosition, scaleMultiplier*weaponSize, Vector2.SignedAngle(Vector2.right, dir), targetLayer);
            
            weaponSpriteRenderer.transform.position = attackBoxPosition;
            weaponSpriteRenderer.transform.localRotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.right, dir));
            weaponSpriteRenderer.transform.localScale = initialWeaponScale * scaleMultiplier;

            foreach (Collider2D collider in hitColliders)
            {
                if (!hitMonsters.Contains(collider.gameObject))
                {
                    hitMonsters.Add(collider.gameObject);
                    Monster monster = collider.gameObject.GetComponentInParent<Monster>();
                    monster.TakeDamage(damage.Value, dir * knockback.Value);
                    AudioManager.Instance.PlayAxeAttack();
                    playerCharacter.OnDealDamage.Invoke(damage.Value);
                }
            }

            t += Time.deltaTime;
            yield return null;
        }
        weaponSpriteRenderer.enabled = false;
    }

    /// <summary>
    /// 同一敌人一次挥砍只结算一次，挥砍结束后清理登记。
    /// </summary>
    private void DeregisterMonster(Monster monster)
    {
        hitMonsters.Remove(monster.gameObject);
    }

    /// <summary>
    /// 按挥砍进度返回弧线的缩放倍率。
    /// </summary>
    private float GetScaleMultiplier(float t)
    {
        if (t < scaleInTime)
        {
            return EasingUtils.EaseOutQuad(t/scaleInTime);
        }
        else if (t > slashTime - scaleInTime)
        {
            return EasingUtils.EaseOutQuad((slashTime-t)/scaleInTime);
        }
        else
        {
            return 1;
        }
    }
}
