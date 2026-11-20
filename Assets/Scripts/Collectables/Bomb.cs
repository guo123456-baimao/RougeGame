using UnityEngine;
public class Bomb : Collectable
{
    [SerializeField] protected float bombDamage;

    /// <summary>
    /// 对屏幕内所有敌人造成炸弹伤害。
    /// </summary>
    protected override void OnCollected()
    {
        entityManager.DamageAllVisibileEnemies(bombDamage);
        Destroy(gameObject);
    }
}
