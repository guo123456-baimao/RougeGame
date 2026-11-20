using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Book : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 1;
    private BookAbility bookAbility;
    private LayerMask monsterLayer;

    /// <summary>
    /// 绑定所属书本技能与敌人层，供碰撞判定使用。
    /// </summary>
    public void Init(BookAbility bookAbility, LayerMask monsterLayer)
    {
        this.bookAbility = bookAbility;
        this.monsterLayer = monsterLayer;
    }

    /// <summary>
    /// 让书本绕自身位置匀速自转（转速受升级影响）。
    /// </summary>
    void Update()
    {
        transform.RotateAround(transform.position, Vector3.back, Time.deltaTime*100*rotationSpeed);
    }

    /// <summary>
    /// 触碰到敌人层时通知书本技能结算伤害。
    /// </summary>
    void OnTriggerEnter2D(Collider2D collider)
    {
        if ((monsterLayer & (1 << collider.gameObject.layer)) != 0)
        {
            bookAbility.Damage(collider.gameObject.GetComponentInParent<Monster>());
        }
    }
}
