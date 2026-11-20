using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class StabAbility : MeleeAbility
{
    [Header("Stab Stats")]
    [SerializeField] protected float stabOffset;
    [SerializeField] protected float stabDistance;
    [SerializeField] protected float stabTime;
    protected Vector2 weaponSize;
    protected FastList<GameObject> hitMonsters;

    /// <summary>
    /// 启动突刺循环。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        weaponSize = weaponSpriteRenderer.bounds.size;
        weaponSpriteRenderer.enabled = false;
    }

    /// <summary>
    /// 按冷却执行一次突刺。
    /// </summary>
    protected override void Attack()
    {
        StartCoroutine(Stab());
    }

    /// <summary>
    /// 突刺表现：朝目标方向位移，并在路径上对敌人造成伤害。
    /// </summary>
    protected virtual IEnumerator Stab()
    {
        hitMonsters = new FastList<GameObject>();
        timeSinceLastAttack -= stabTime;
        float t = 0;
        weaponSpriteRenderer.enabled = true;
        Vector2 dir = FindNearestEnemyDirection();
        while (t < stabTime)
        {
            Vector2 attackBoxPosition = (Vector2)playerCharacter.CenterTransform.position + dir * (weaponSize.x/2 + stabOffset + stabDistance/stabTime*t);
            float attackAngle = Vector2.SignedAngle(Vector2.right, dir);
            Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackBoxPosition, weaponSize, attackAngle, targetLayer);
            
            weaponSpriteRenderer.transform.position = attackBoxPosition;
            weaponSpriteRenderer.transform.localRotation = Quaternion.Euler(0, 0, attackAngle);

            foreach (Collider2D collider in hitColliders)
            {
                if (!hitMonsters.Contains(collider.gameObject))
                {
                    hitMonsters.Add(collider.gameObject);
                    Monster monster = collider.gameObject.GetComponentInParent<Monster>();
                    DamageMonster(monster, damage.Value, dir*knockback.Value);
                    playerCharacter.OnDealDamage.Invoke(damage.Value);
                }
            }

            t += Time.deltaTime;
            yield return null;
        }
        Vector2 initialScale = weaponSpriteRenderer.transform.localScale;
        t = 0;
        while (t < 1)
        {
            weaponSpriteRenderer.transform.localPosition = (Vector2)playerCharacter.CenterTransform.position + dir * (weaponSpriteRenderer.transform.localScale.x/initialScale.x*weaponSize.x/2 + stabOffset + stabDistance);
            weaponSpriteRenderer.transform.localScale = Vector2.Lerp(initialScale, Vector2.zero, EasingUtils.EaseInQuart(t));
            t += Time.deltaTime * 4;
            yield return null;
        }
        weaponSpriteRenderer.transform.localScale = initialScale;
        weaponSpriteRenderer.enabled = false;
    }

    /// <summary>
    /// 对突刺路径上的敌人结算伤害。
    /// </summary>
    protected virtual void DamageMonster(Monster monster, float damage, Vector2 knockback)
    {
        monster.TakeDamage(damage, knockback);
        AudioManager.Instance.PlayAxeAttack();
    }
}
