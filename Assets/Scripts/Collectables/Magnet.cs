public class Magnet : Collectable
{   
    /// <summary>
    /// 吸取场上所有金币与经验宝石。
    /// </summary>
    protected override void OnCollected()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMagnet();
        entityManager.CollectAllCoinsAndGems();
        Destroy(gameObject);
    }
}
