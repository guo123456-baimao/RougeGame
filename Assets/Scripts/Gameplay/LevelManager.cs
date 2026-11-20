using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
public class LevelManager : MonoBehaviour
{
    [SerializeField] private LevelBlueprint levelBlueprint;
    [Tooltip("全局铭文库，必须在 playerCharacter.Init 之前注入")]
    [SerializeField] private EngravingDatabase engravingDatabase;
    [SerializeField] private Character playerCharacter;
    [SerializeField] private EntityManager entityManager;
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private AbilitySelectionDialog abilitySelectionDialog;
    [SerializeField] private InfiniteBackground infiniteBackground;
    [SerializeField] private Inventory inventory;
    [SerializeField] private StatsManager statsManager;
    [SerializeField] private GameOverDialog gameOverDialog;
    [SerializeField] private GameTimer gameTimer;
    [Tooltip("相位2「血色地」的血池切片帧，渲染在角色下方")]
    [SerializeField] private Sprite[] bloodRainGroundFrames;
    [Tooltip("相位2「血色地」的血雨切片帧，渲染在角色上方")]
    [SerializeField] private Sprite[] bloodRainRainFrames;
    [Tooltip("相位3「虚空」的闪电精灵图，留空则不启用该机制")]
    [SerializeField] private Texture2D lightningSheet;
    [Tooltip("是否打印相位切换与每段刷怪分布日志（验收用，发布时可关闭）")]
    [SerializeField] private bool enablePhaseLogs = true;
    private float levelTime = 0;
    private float timeSinceLastMonsterSpawned;
    private float timeSinceLastChestSpawned;
    private bool miniBossSpawned = false;
    private bool finalBossSpawned = false;
    // 三段式相位状态：phases 配置无效时 activePhases 为 null，走原单表逻辑
    private LevelBlueprint.LevelPhase[] activePhases;
    private float[] phaseStartTimes;
    private int currentPhaseIndex;
    private int miniBossesSpawned;
    private int[] phaseSpawnCounts;  // 当前相位内各怪物实际刷出数量，用于日志验收
    private PhaseTransitionFX phaseTransitionFX;  // 相位切换演出（渐黑下沉/渐亮上升）
    private BloodRainHazard bloodRainHazard;      // 相位2「血色地」的血雨区域机制
    private LightningHazard lightningHazard;      // 相位3「虚空」的闪电区域机制
    private bool phaseTransitionStarted;          // 当前相位边界的演出是否已启动
    private InGameHud hud;
    private int normalsSinceLastElite = 0;  // 精英保底计数
    private bool initialized;  // 异步初始化完成后才允许 Update 跑玩法逻辑
    private bool runOver;      // 一局已结束（死亡/通关）：结算弹窗自己管理 timeScale，演出不得越权恢复
    private bool phaseFrozen;  // 相位切换演出冻结中：关卡时间用真实时间补偿推进
    private PauseMenu pauseMenu;  // 用于判断外部冻结（暂停菜单/升级三选一弹窗）

    /// <summary>
    /// 异步初始化链路：HUD 主题与全部怪物/掉落物预制体经 ResManager 按引用计数加载完成后，
    /// 才继续构建对象池与玩法系统。
    /// </summary>
    private IEnumerator InitRoutine(LevelBlueprint levelBlueprint)
    {
        initialized = false;
        // HUD 主题（常驻资源）与怪物/掉落物预制体并发加载：这里只发起不等待，
        // 等entityManager.InitRoutine（加载大头）跑完后再收尾，缩短进局的加载卡顿
        bool themeLoaded = false;
        ResManager.AssetLoadAsync<HudTheme>(HudTheme.DefaultThemeAddress, _ => themeLoaded = true, isPersistent: true);

        AudioManager.Instance.PlayBGMGame();
        // 铭文结算发生在 playerCharacter.Init 里，库必须先就位
        EngravingManager.SetDatabase(engravingDatabase);
        this.levelBlueprint = levelBlueprint;
        levelTime = 0;
        normalsSinceLastElite = 0;

        // 解析三段式相位配置；无效时只警告并回退到单表逻辑，不报错、不空引用
        activePhases = null;
        phaseStartTimes = null;
        currentPhaseIndex = 0;
        miniBossesSpawned = 0;
        phaseSpawnCounts = null;
        if (this.levelBlueprint.TryGetValidPhases(out float[] starts))
        {
            activePhases = this.levelBlueprint.phases;
            phaseStartTimes = starts;
            phaseSpawnCounts = new int[this.levelBlueprint.MonsterIndexMap.Count];
        }
        else if (this.levelBlueprint.phases != null && this.levelBlueprint.phases.Length > 0)
        {
            Debug.LogWarning($"[LevelManager] {this.levelBlueprint.name} 的 phases 配置无效（为空/时长总和≠levelTime/缺刷怪表），回退到单表刷怪逻辑");
        }
        
        // 初始化实体管理器（等待怪物/掉落物预制体按引用计数加载并建池）
        yield return entityManager.InitRoutine(this.levelBlueprint, playerCharacter, inventory, statsManager, infiniteBackground, abilitySelectionDialog);
        while (!themeLoaded) yield return null;  // HUD 主题就位后才允许构建 HUD
        CrossSceneData.EntityManager = entityManager;
        // 初始化技能管理器
        abilityManager.Init(this.levelBlueprint, entityManager, playerCharacter, abilityManager);
        abilitySelectionDialog.Init(abilityManager, entityManager, playerCharacter);
        // 初始化角色
        playerCharacter.Init(entityManager, abilityManager, statsManager);
        playerCharacter.OnDeath.AddListener(GameOver);
        // 相位2「血色地」的血雨机制：运行时挂到 LevelManager 上，避免场景里漏挂组件
        if (bloodRainGroundFrames != null && bloodRainGroundFrames.Length > 0
            && bloodRainRainFrames != null && bloodRainRainFrames.Length > 0)
        {
            bloodRainHazard = GetComponent<BloodRainHazard>();
            if (bloodRainHazard == null) bloodRainHazard = gameObject.AddComponent<BloodRainHazard>();
            bloodRainHazard.Init(entityManager, playerCharacter, bloodRainGroundFrames, bloodRainRainFrames);
        }
        // 相位3「虚空」的闪电机制
        if (lightningSheet != null)
        {
            lightningHazard = GetComponent<LightningHazard>();
            if (lightningHazard == null) lightningHazard = gameObject.AddComponent<LightningHazard>();
            lightningHazard.Init(entityManager, playerCharacter, lightningSheet);
        }
        // 技能栏 / 进度条 / 受击反馈都挂在这里，统一注入依赖
        hud = InGameHud.Ensure();
        hud?.Bind(this.levelBlueprint, playerCharacter, abilityManager, entityManager);
        // 在玩家周围生成初始经验宝石
        entityManager.SpawnGemsAroundPlayer(this.levelBlueprint.initialExpGemCount, this.levelBlueprint.initialExpGemType);
        // 生成一个初始宝箱
        entityManager.SpawnChest(levelBlueprint.chestBlueprint);
        // 初始化无限滚动背景
        // 三段式时首屏用第一相位的背景，否则沿用整局背景
        Texture2D initBackground = activePhases != null && activePhases[0].background != null
            ? activePhases[0].background
            : this.levelBlueprint.backgroundTexture;
        infiniteBackground.Init(initBackground, playerCharacter.transform);
        // 相位切换演出：全屏黑幕 + 玩家下沉/上升（仅视觉，不动物理与相机）
        phaseTransitionFX = PhaseTransitionFX.Create(playerCharacter.transform);
        phaseTransitionStarted = false;
        // 初始化背包
        inventory.Init();
        initialized = true;
    }

    /// <summary>
    /// 第一帧更新前启动异步初始化流程。
    /// </summary>
    void Start()
    {
        StartCoroutine(InitRoutine(levelBlueprint));
    }

    /// <summary>
    /// 每帧推进关卡时间、刷怪计时、Boss 出场与相位切换逻辑。
    /// </summary>
    void Update()
    {
        if (!initialized) return;  // 等待异步初始化完成，避免在对象池就位前刷怪/计时
        // Time：相位切换演出冻结全场（timeScale=0）期间，关卡时间用真实时间推进，
        // 保证边界帧仍能在全黑窗口内跨过、背景与刷怪表照常切换
        levelTime += phaseFrozen ? Time.unscaledDeltaTime : Time.deltaTime;
        gameTimer.SetTime(levelTime);
        hud?.SetLevelTime(levelTime);
        // 相位推进与背景切换（清场/玩家物理/相机一概不动）
        UpdatePhase();
        // 相位切换演出：边界前 1.05s 启动，屏幕全黑窗口期正好覆盖边界帧
        UpdatePhaseTransition();
        // 怪物刷新计时器
        if (levelTime < levelBlueprint.levelTime)
        {
            timeSinceLastMonsterSpawned += Time.deltaTime;
            // 三段式时在相位内重新归一化 t，保证每段"开场稀疏、结尾密集"的节奏
            float spawnT;
            MonsterSpawnTable spawnTable = GetSpawnTable(out spawnT);
            float spawnRate = spawnTable.GetSpawnRate(spawnT);
            float monsterSpawnDelay = spawnRate > 0 ? 1.0f/spawnRate : float.PositiveInfinity;
            if (timeSinceLastMonsterSpawned >= monsterSpawnDelay)
            {
                (int monsterIndex, float hpMultiplier) = spawnTable.SelectMonsterWithHPMultiplier(spawnT);
                if (monsterIndex >= 0)
                {
                    (int poolIndex, int blueprintIndex) = levelBlueprint.MonsterIndexMap[monsterIndex];
                    MonsterBlueprint monsterBlueprint = levelBlueprint.monsters[poolIndex].monsterBlueprints[blueprintIndex];
                    EliteSpawnInfo eliteInfo = RollEliteSpawn();
                    entityManager.SpawnMonsterRandomPosition(poolIndex, monsterBlueprint, monsterBlueprint.hp * hpMultiplier, eliteInfo);
                    if (phaseSpawnCounts != null)
                        phaseSpawnCounts[monsterIndex]++;
                    timeSinceLastMonsterSpawned = Mathf.Repeat(timeSinceLastMonsterSpawned, monsterSpawnDelay);
                }
            }
        }
        // 小 Boss：三段式按相位刷新（miniBossSpawnTime 相对相位开始），否则回退原 miniBosses[0] 逻辑
        if (activePhases != null)
        {
            if (miniBossesSpawned < activePhases.Length)
            {
                LevelBlueprint.LevelPhase phase = activePhases[miniBossesSpawned];
                if (phase.phaseMiniBoss != null && phase.phaseMiniBoss.bossBlueprint != null
                    && levelTime > phaseStartTimes[miniBossesSpawned] + phase.miniBossSpawnTime)
                {
                    miniBossesSpawned++;
                    // 复用 finalBoss.bossPrefab 所在的 Boss 池（下标 monsters.Length），只换蓝图
                    Monster miniBoss = entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length, phase.phaseMiniBoss.bossBlueprint);
                    TrackBoss(miniBoss);
                }
            }
        }
        else if (!miniBossSpawned && levelTime > levelBlueprint.miniBosses[0].spawnTime)
        {
            miniBossSpawned = true;
            Monster miniBoss = entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length, levelBlueprint.miniBosses[0].bossBlueprint);
            TrackBoss(miniBoss);
        }
        // Boss 刷新
        if (!finalBossSpawned && levelTime > levelBlueprint.levelTime)
        {
            //entityManager.KillAllMonsters();
            finalBossSpawned = true;
            Monster finalBoss = entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length, levelBlueprint.finalBoss.bossBlueprint);
            finalBoss.OnKilled.AddListener(LevelPassed);
            TrackBoss(finalBoss);
        }
        // 宝箱刷新计时器
        timeSinceLastChestSpawned += Time.deltaTime;
        if (timeSinceLastChestSpawned >= levelBlueprint.chestSpawnDelay)
        {
            for (int i = 0; i < levelBlueprint.chestSpawnAmount; i++)
            {
                entityManager.SpawnChest(levelBlueprint.chestBlueprint);
            }
            timeSinceLastChestSpawned = Mathf.Repeat(timeSinceLastChestSpawned, levelBlueprint.chestSpawnDelay);
        }
    }

    /// <summary>
    /// 决定本次刷怪是否精英。概率 + 保底双旋钮，精英数量受 maxEliteAlive 限制。
    /// 返回 default(EliteSpawnInfo) 时走原始刷怪逻辑（数值与改动前完全一致）。
    /// </summary>
    private EliteSpawnInfo RollEliteSpawn()
    {
        EliteSpawnInfo info = default(EliteSpawnInfo);
        float chance = levelBlueprint.eliteChance;
        int pityCount = levelBlueprint.elitePityCount;
        // 两个旋钮都为 0 时彻底关闭精英
        if (chance <= 0f && pityCount <= 0)
            return info;

        bool pity = pityCount > 0 && normalsSinceLastElite >= pityCount;
        if (!pity && Random.value >= chance)
        {
            normalsSinceLastElite++;
            return info;
        }
        // 场上精英已达上限：本次降级为普通怪，保底进度继续累计
        int maxAlive = levelBlueprint.maxEliteAlive > 0 ? levelBlueprint.maxEliteAlive : 1;
        if (entityManager.EliteAliveCount >= maxAlive)
        {
            normalsSinceLastElite++;
            return info;
        }

        normalsSinceLastElite = 0;
        info.isElite = true;
        info.affix = (EliteAffix)Random.Range(1, EliteAffixTuning.AffixVariants + 1);
        info.hpMultiplier = levelBlueprint.eliteHpMultiplier > 0f ? levelBlueprint.eliteHpMultiplier : 1f;
        info.damageMultiplier = levelBlueprint.eliteDamageMultiplier > 0f ? levelBlueprint.eliteDamageMultiplier : 1f;
        info.attackSpeedMultiplier = levelBlueprint.eliteAttackSpeedMultiplier > 0f ? levelBlueprint.eliteAttackSpeedMultiplier : 1f;
        info.moveSpeedMultiplier = levelBlueprint.eliteSpeedMultiplier > 0f ? levelBlueprint.eliteSpeedMultiplier : 1f;
        info.scale = levelBlueprint.eliteScale > 0f ? levelBlueprint.eliteScale : 1f;
        info.chestDrop = levelBlueprint.eliteChestDrop;
        info.tint = levelBlueprint.eliteTint;
        return info;
    }

    /// <summary>
    /// 取当前生效的刷怪表与归一化 t：三段式返回当前相位刷怪表与相位内 t_local，
    /// 否则返回整局刷怪表与整局 t（原逻辑）。
    /// </summary>
    private MonsterSpawnTable GetSpawnTable(out float t)
    {
        if (activePhases != null)
        {
            int index = GetCurrentPhaseIndex(levelTime);
            float phaseStart = phaseStartTimes[index];
            float phaseDuration = (index + 1 < phaseStartTimes.Length
                ? phaseStartTimes[index + 1]
                : levelBlueprint.levelTime) - phaseStart;
            t = (levelTime - phaseStart) / phaseDuration;
            return activePhases[index].spawnTable;
        }
        t = levelTime / levelBlueprint.levelTime;
        return levelBlueprint.monsterSpawnTable;
    }

    /// <summary>当前时间落在哪个相位（超出最后一段时停在最后相位）。</summary>
    private int GetCurrentPhaseIndex(float t)
    {
        int index = activePhases.Length - 1;
        for (int i = 0; i < phaseStartTimes.Length - 1; i++)
        {
            if (t < phaseStartTimes[i + 1])
            {
                index = i;
                break;
            }
        }
        return index;
    }

    /// <summary>
    /// 相位推进与背景切换：只换贴图与打日志，不清空 livingMonsters、不移动玩家、不重置相机。
    /// 切换检查每帧执行，误差为一帧耗时，远小于 0.2s 要求；只换贴图，单帧开销可忽略。
    /// </summary>
    private void UpdatePhase()
    {
        if (activePhases == null) return;
        int target = GetCurrentPhaseIndex(levelTime);
        if (target == currentPhaseIndex) return;
        // 离开任一区域机制相位时立刻停止旧机制与结算
        bloodRainHazard?.Stop();
        lightningHazard?.Stop();
        // 离开的相位打印实际刷怪分布，用于验收"每段刷怪构成符合配置"
        LogPhaseSpawnDistribution(currentPhaseIndex);
        currentPhaseIndex = target;
        phaseTransitionStarted = false;  // 边界已过，允许为下一个边界启动演出
        LevelBlueprint.LevelPhase phase = activePhases[currentPhaseIndex];
        if (phase.background != null)
            infiniteBackground.SetBackground(phase.background);
        if (enablePhaseLogs)
            Debug.Log($"[LevelManager] 进入相位 {currentPhaseIndex + 1}/{activePhases.Length}：「{phase.displayName}」（{levelTime:F1}s）");
        if (IsBloodGroundPhase(currentPhaseIndex)) bloodRainHazard?.Begin();
        if (IsLightningPhase(currentPhaseIndex)) lightningHazard?.Begin();
    }

    /// <summary>相位3触发闪电区域机制。</summary>
    private bool IsLightningPhase(int phaseIndex)
    {
        return activePhases != null && phaseIndex == 2 && activePhases.Length > 2;
    }

    /// <summary>只认第 2 相位且显示名为「血色地」的关卡，避免误伤其他关卡的相位2。</summary>
    private bool IsBloodGroundPhase(int phaseIndex)
    {
        return activePhases != null && phaseIndex == 1
            && activePhases[phaseIndex].displayName != null
            && activePhases[phaseIndex].displayName.Contains("血色地");
    }

    /// <summary>
    /// 相位切换演出调度：在边界前 FadeOutDuration+0.05s 启动，屏幕全黑时正好跨过边界帧，
    /// 背景与刷怪表的真正切换仍由 UpdatePhase 在边界帧完成。
    /// 全程 3.5s：渐黑 1.0s + 全黑展示「你已坠入{相位名}！！！」1.5s + 渐亮 1.0s。
    /// 最后一段的边界（levelTime，大 Boss 出场）不做演出。
    /// </summary>
    private void UpdatePhaseTransition()
    {
        if (runOver) return;  // 一局已结束，不再启动演出（结算弹窗管理 timeScale）
        if (activePhases == null || phaseTransitionFX == null) return;
        if (currentPhaseIndex >= activePhases.Length - 1) return;
        float boundary = phaseStartTimes[currentPhaseIndex + 1];
        if (!phaseTransitionStarted && levelTime >= boundary - (PhaseTransitionFX.FadeOutDuration + 0.05f))
        {
            phaseTransitionStarted = true;
            StartCoroutine(TransitionFrozen(activePhases[currentPhaseIndex + 1].displayName));
        }
    }

    /// <summary>
    /// 带全场冻结的相位切换：演出期间 Time.timeScale=0，物理/怪物追击/攻击/拾取全部暂停，
    /// 防止黑屏窗口期玩家被磨死。演出用 unscaled 时间照常播放（见 PhaseTransitionFX）。
    /// 演出结束后恢复时间流速；若期间已死亡/通关或被暂停菜单/三选一弹窗冻结，则不越权恢复。
    /// </summary>
    private IEnumerator TransitionFrozen(string phaseDisplayName)
    {
        phaseFrozen = true;
        Time.timeScale = 0f;
        yield return phaseTransitionFX.Transition(phaseDisplayName);
        phaseFrozen = false;
        if (!runOver && !IsExternalFrozen())
            Time.timeScale = 1f;
    }

    /// <summary>暂停菜单或升级三选一弹窗等外部系统冻结时间时，不由相位演出越权恢复。</summary>
    private bool IsExternalFrozen()
    {
        if (pauseMenu == null) pauseMenu = FindObjectOfType<PauseMenu>();
        return pauseMenu != null && (pauseMenu.IsPaused || pauseMenu.TimeIsFrozen);
    }

    /// <summary>打印刚结束相位的实际刷怪分布，并清零计数开始统计下一段。</summary>
    private void LogPhaseSpawnDistribution(int phaseIndex)
    {
        if (!enablePhaseLogs || phaseIndex < 0 || phaseIndex >= activePhases.Length || phaseSpawnCounts == null)
            return;
        int total = 0;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < phaseSpawnCounts.Length; i++)
        {
            (int poolIndex, int blueprintIndex) = levelBlueprint.MonsterIndexMap[i];
            string name = levelBlueprint.monsters[poolIndex].monsterBlueprints[blueprintIndex].name;
            sb.AppendLine($"    {name}: {phaseSpawnCounts[i]}");
            total += phaseSpawnCounts[i];
        }
        Debug.Log($"[LevelManager] 相位「{activePhases[phaseIndex].displayName}」结束，共刷怪 {total} 只，分布：\n{sb}");
        System.Array.Clear(phaseSpawnCounts, 0, phaseSpawnCounts.Length);
    }

    /// <summary>把 Boss 接到 HUD 血条上，死亡/回池时自动收起。</summary>
    private void TrackBoss(Monster boss)
    {
        if (boss == null) return;
        hud?.ShowBoss(boss);
        boss.OnKilled.AddListener(delegate (Monster killed) { hud?.HideBoss(); });
    }

    /// <summary>
    /// 一局结束都要清掉单局数据：商店的本局购买记录、
    /// 一次性属性加成、幸运加成等，否则回到主菜单后商店还停在上局状态。
    /// 三种结束方式都要走这里：死亡 GameOver、通关 LevelPassed、
    /// 暂停菜单里直接返回首页 ReturnToMainMenu（中途弃局也算一局结束）。
    /// </summary>
    private void EndRun()
    {
        // 一局结束（死亡/通关/返回首页）时冻结时间，循环音不会再有 OnClipEnd 来收尾
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopMachineGunLoop();
        Character.ClearSessionData();
    }

    /// <summary>
    /// 玩家死亡：冻结时间、结算本局金币奖励并弹出失败结算界面。
    /// </summary>
    public void GameOver()
    {
        runOver = true;
        bloodRainHazard?.Stop();
        lightningHazard?.Stop();
        Time.timeScale = 0;
        DataStoreLocator.Store.SettleRunReward(statsManager.CoinsGained);
        EndRun();
        gameOverDialog.Open(false, statsManager);
    }

    /// <summary>
    /// 击败最终 Boss 通关：冻结时间、结算奖励并弹出通关界面。
    /// </summary>
    public void LevelPassed(Monster finalBossKilled)
    {
        runOver = true;
        bloodRainHazard?.Stop();
        lightningHazard?.Stop();
        Time.timeScale = 0;
        DataStoreLocator.Store.SettleRunReward(statsManager.CoinsGained);
        EndRun();
        gameOverDialog.Open(true, statsManager);
    }

    /// <summary>
    /// 重开本关：恢复时间流速并重新加载当前场景。
    /// </summary>
    public void Restart()
    {
        AudioManager.Instance.PlayBGMGame();
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// 返回主菜单：恢复时间流速、清理单局数据并加载首页场景。
    /// </summary>
    public void ReturnToMainMenu()
    {
        AudioManager.Instance.PlayBGMMain();
        Time.timeScale = 1;
        EndRun();
        SceneManager.LoadScene(0);
    }
}
