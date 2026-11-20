using UnityEngine;
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource bgmSource;
    // 循环类音效（如机枪连发）单独用一个 source，避免和 PlayOneShot 互相打断
    [SerializeField] private AudioSource loopSource;
    private bool machineGunLoopPaused;
    // UI/结算类音效单独一条通道：游戏冻结（暂停/弹窗/演出）时也照常播放
    private AudioSource uiSource;
    private bool sfxFrozen;      // 当前是否处于「特效音冻结」状态（跟随 Time.timeScale）

    [SerializeField] private AudioClip[] clickButtonClips;
    [SerializeField] private AudioClip defeat;
    [SerializeField] private AudioClip magnet;
    [SerializeField] private AudioClip itemPickup;
    [SerializeField] private AudioClip healthPotion;
    [SerializeField] private AudioClip[] collectXPClips;
    [SerializeField] private AudioClip levelUp;
    [SerializeField] private AudioClip axeAttack;
    [SerializeField] private AudioClip[] explosionClips;
    [SerializeField] private AudioClip deathExplosion;  // 精英怪自爆（DeathExplosionEffect 专用）
    [SerializeField] private AudioClip machineGunFire;
    [SerializeField] private AudioClip gravityWell;
    [SerializeField] private AudioClip fireBurn;
    [SerializeField] private AudioClip thrust;
    [SerializeField] private AudioClip book;

    [Header("BGM")]
    [SerializeField] private AudioClip bgmMainMenu;
    [SerializeField] private AudioClip bgmGame;

    /// <summary>
    /// 初始化各音源并注册为全局单例。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || !sfxSource.gameObject.activeInHierarchy) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlaySFXRandom(AudioClip[] clips, float volume = 1f)
    {
        if (clips == null || clips.Length == 0) return;
        PlaySFX(clips[Random.Range(0, clips.Length)], volume);
    }

    /// <summary>
    /// 跟随 Time.timeScale 的特效音冻结：timeScale=0 时暂停正在播的特效音（含循环音），
    /// 恢复后接着播；BGM 不受影响。覆盖所有冻结来源（暂停菜单/三选一弹窗/相位切换演出）。
    /// </summary>
    private void Update()
    {
        bool frozen = Time.timeScale == 0f;
        if (frozen == sfxFrozen) return;
        sfxFrozen = frozen;

        if (frozen)
        {
            if (sfxSource != null && sfxSource.isPlaying)
                sfxSource.Pause();
            PauseMachineGunLoop();
        }
        else
        {
            if (sfxSource != null)
                sfxSource.UnPause();
            ResumeMachineGunLoop();
        }
    }

    /// <summary>冻结期间也要能出声的 UI/结算类音效走这里（按钮点击、升级提示等）。</summary>
    private void PlayUISFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        UiSource.PlayOneShot(clip, volume);
    }

    private AudioSource UiSource
    {
        get
        {
            if (uiSource == null)
            {
                var sourceObject = new GameObject("UI SFX Source");
                sourceObject.transform.SetParent(transform, false);
                uiSource = sourceObject.AddComponent<AudioSource>();
                uiSource.playOnAwake = false;
                uiSource.spatialBlend = 0f;
            }
            return uiSource;
        }
    }

    public void PlayBGM(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || !bgmSource.gameObject.activeInHierarchy) return;
        bgmSource.Stop();
        bgmSource.loop = loop;
        bgmSource.volume = volume;
        bgmSource.PlayOneShot(clip);
    }

    /// <summary>
    /// 切换背景音乐（与当前曲目相同则忽略）。
    /// </summary>
    public void SwitchBGM(AudioClip newClip)
    {
        if (newClip == null || !bgmSource.gameObject.activeInHierarchy) return;
        bgmSource.Stop();
        bgmSource.loop = true;
        bgmSource.clip = newClip;
        bgmSource.Play();
    }

    public void PlayBGMMain() => SwitchBGM(bgmMainMenu);
    public void PlayBGMGame() => SwitchBGM(bgmGame);

    /// <summary>
    /// 停止背景音乐。
    /// </summary>
    public void StopBGM()
    {
        if (bgmSource == null) return;
        bgmSource.Stop();
    }

    /// <summary>
    /// 按钮点击与升级提示在冻结状态下触发（暂停菜单/弹窗/升级瞬间），走不受冻结影响的 UI 通道
    /// </summary>
    public void PlayClick()
    {
        if (clickButtonClips == null || clickButtonClips.Length == 0) return;
        PlayUISFX(clickButtonClips[Random.Range(0, clickButtonClips.Length)]);
    }
    public void PlayPickup() => PlaySFX(itemPickup);
    public void PlayHealthPotion() => PlaySFX(healthPotion);
    public void PlayCollectXP() => PlaySFXRandom(collectXPClips);
    public void PlayLevelUp() => PlayUISFX(levelUp);
    public void PlayAxeAttack() => PlaySFX(axeAttack);
    public void PlayExplosion() => PlaySFXRandom(explosionClips);
    public void PlayDeathExplosion() => PlaySFX(deathExplosion);

    // 机枪连发：整个弹夹期间循环播放同一段开火音，而不是每颗子弹播一次
    private AudioSource LoopSource
    {
        get
        {
            if (loopSource == null)
            {
                var sourceObject = new GameObject("SFX Loop Source");
                sourceObject.transform.SetParent(transform, false);
                loopSource = sourceObject.AddComponent<AudioSource>();
                loopSource.playOnAwake = false;
                loopSource.loop = true;
                loopSource.spatialBlend = 0f;
            }
            return loopSource;
        }
    }

    public void PlayMachineGunLoop(float volume = 1f)
    {
        if (machineGunFire == null) return;
        // 冻结期间不（重）启动循环音：能力的 Update 在 timeScale=0 时照跑，
        // 若不拦住会用 Play() 每帧把刚暂停的循环音重新拉起来
        if (Time.timeScale == 0f) return;

        var source = LoopSource;
        if (!source.gameObject.activeInHierarchy) return;

        source.clip = machineGunFire;
        source.loop = true;
        source.volume = volume;
        if (!source.isPlaying)
            source.Play();
    }

    /// <summary>
    /// 停止机枪循环音效。
    /// </summary>
    public void StopMachineGunLoop()
    {
        machineGunLoopPaused = false;
        if (loopSource != null && loopSource.isPlaying)
            loopSource.Stop();
    }

    /// <summary>
    /// 游戏被冻结（升级面板 / 暂停菜单）时把循环音一起停掉，恢复后接着播剩下的一段
    /// </summary>
    public void PauseMachineGunLoop()
    {
        if (loopSource == null || !loopSource.isPlaying) return;
        loopSource.Pause();
        machineGunLoopPaused = true;
    }

    /// <summary>
    /// 恢复机枪循环音效。
    /// </summary>
    public void ResumeMachineGunLoop()
    {
        if (!machineGunLoopPaused) return;
        machineGunLoopPaused = false;
        if (loopSource != null)
            loopSource.UnPause();
    }

    public void PlayGravityWell() => PlaySFX(gravityWell);
    public void PlayFireBurn() => PlaySFX(fireBurn);
    public void PlayThrust() => PlaySFX(thrust);
    public void PlayBook() => PlaySFX(book);
    public void PlayDefeat() => PlaySFX(defeat);
    public void PlayMagnet() => PlaySFX(magnet);
}
