using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class BookAbility : Ability
{
    [Header("Book Stats")]
    [SerializeField] protected GameObject bookPrefab;
    [SerializeField] protected LayerMask monsterLayer;
    [SerializeField] protected UpgradeableProjectileCount projectileCount;
    [SerializeField] protected UpgradeableAOE radius;
    [SerializeField] protected UpgradeableDamage damage;
    [SerializeField] protected UpgradeableKnockback knockback;
    [SerializeField] protected UpgradeableRotationSpeed speed;
    private List<Book> books;

    /// <summary>
    /// 启用书本并刷新环绕数量。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        gameObject.SetActive(true);
        projectileCount.OnChanged.AddListener(RefreshBooks);
        books = new List<Book>();
        for (int i = 0; i < projectileCount.Value; i++)
        {
            AddBook();
        }
    }

    /// <summary>
    /// 升级后增加书本数量并重新均分环绕角度。
    /// </summary>
    protected override void Upgrade()
    {
        base.Upgrade();
        RefreshBooks();
    }

    /// <summary>
    /// 按书本数量均分角度，让书本围绕玩家做圆周运动。
    /// </summary>
    void Update()
    {
        for (int i = 0; i < books.Count; i++)
        {
            float theta = (2 * Mathf.PI * i)/books.Count;
            books[i].transform.localPosition = new Vector3(Mathf.Sin(theta + Time.time*speed.Value), Mathf.Cos(theta + Time.time * speed.Value), 0);
        }
    }

    /// <summary>
    /// 对敌人结算书本伤害，并广播玩家造成伤害事件。
    /// </summary>
    public void Damage(IDamageable damageable)
    {
        AudioManager.Instance.PlayBook();
        Vector2 knockbackDirection = (damageable.transform.position - playerCharacter.transform.position).normalized;
        damageable.TakeDamage(damage.Value, knockback.Value * knockbackDirection);
        playerCharacter.OnDealDamage.Invoke(damage.Value);
    }

    /// <summary>
    /// 按当前书本数量增删书本，并重新均分角度。
    /// </summary>
    private void RefreshBooks()
    {
        for (int i = books.Count; i < projectileCount.Value; i++)
            AddBook();
    }

    /// <summary>
    /// 创建一本新书并加入环绕阵列。
    /// </summary>
    private void AddBook()
    {
        Book book = Instantiate(bookPrefab, playerCharacter.transform).GetComponent<Book>();
        book.Init(this, monsterLayer);
        books.Add(book);
    }
}
