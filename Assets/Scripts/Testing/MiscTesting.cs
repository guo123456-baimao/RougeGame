using UnityEngine;

public class MiscTesting : MonoBehaviour
{
    [SerializeField] private PointBar healthBar, expBar;
    [SerializeField] private EntityManager entityManager;
    [SerializeField] private Character character;
    [SerializeField] private AudioClip audioClip;

    /// <summary>
    /// 第一帧更新前调用，当前仅保留被注释掉的调试初始化代码。
    /// </summary>
    void Start()
    {
        // healthBar.Init(75, 0, 100);
        // expBar.Init(158, 102, 300);
    }

    /// <summary>
    /// 每帧检测调试按键：吸金币与经验、秒杀可见敌人、加经验、播放音效。
    /// </summary>
    void Update()
    {
        // if (Input.GetKeyDown(KeyCode.RightArrow))
        //     healthBar.AddPoints(1);
        // if (Input.GetKeyDown(KeyCode.LeftArrow))
        //     healthBar.SubtractPoints(1);
        // if (Input.GetKey(KeyCode.UpArrow))
        //     expBar.AddPoints(1);
        // if (Input.GetKey(KeyCode.DownArrow))
        //     expBar.SubtractPoints(1);
        if (Input.GetKeyDown(KeyCode.Space))
            entityManager.CollectAllCoinsAndGems();//entityManager.DamageAllVisibileEnemies(1);
        if (Input.GetKeyDown(KeyCode.G))
            entityManager.DamageAllVisibileEnemies(500);
        if (Input.GetKeyDown(KeyCode.E))
            character.GainExp(1000);
        if (Input.GetKeyDown(KeyCode.A))
            GetComponent<AudioSource>().Play();
    }
}
