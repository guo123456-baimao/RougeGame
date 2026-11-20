using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.AddressableAssets;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
/// <summary>
/// 管理怪物及其附属对象（经验宝石、金币、宝箱等）的管理器类。
/// </summary>
public class EntityManager : MonoBehaviour
{
    [Header("Monster Spawning Settings")]
    [SerializeField] private float monsterSpawnBufferDistance;  // 在屏幕视野之外额外多远的距离刷怪
    [SerializeField] private float playerDirectionSpawnWeight;  // 刷怪时玩家移动方向的权重
    [Header("Chest Spawning Settings")]
    [SerializeField] private  float chestSpawnRange = 5;
    [Header("Object Pool Settings")]
    [SerializeField] private GameObject monsterPoolParent;
    private MonsterPool[] monsterPools;
    [SerializeField] private GameObject projectilePoolParent;
    private List<ProjectilePool> projectilePools;
    private Dictionary<GameObject, int> projectileIndexByPrefab;
    [SerializeField] private GameObject throwablePoolParent;
    private List<ThrowablePool> throwablePools;
    private Dictionary<GameObject, int> throwableIndexByPrefab;
    [SerializeField] private GameObject boomerangPoolParent;
    private List<BoomerangPool> boomerangPools;
    private Dictionary<GameObject, int> boomerangIndexByPrefab;
    [Tooltip("商店购买的消耗品投放：这里挂全量 CollectableType，进图后由 ResManager 按引用计数加载其预制体")]
    [SerializeField] private CollectableType[] collectableTypes;
    [SerializeField] private AssetReferenceGameObject expGemPrefab;
    [SerializeField] private ExpGemPool expGemPool;
    [SerializeField] private AssetReferenceGameObject coinPrefab;
    [SerializeField] private CoinPool coinPool;
    [SerializeField] private AssetReferenceGameObject chestPrefab;
    [SerializeField] private ChestPool chestPool;
    [SerializeField] private AssetReferenceGameObject textPrefab;
    [SerializeField] private DamageTextPool textPool;
    [Header("Spatial Hash Grid Settings")]
    [SerializeField] private Vector2 gridSize;
    [SerializeField] private Vector2Int gridDimensions;
    [Header("Dependencies")]
    [SerializeField] private SpriteRenderer flashSpriteRenderer;
    [SerializeField] private Camera playerCamera;  // 攝像頭
    private Character playerCharacter;  // 玩家的角色
    private StatsManager statsManager;
    private Inventory inventory;
    private InfiniteBackground infiniteBackground;
    private FastList<Monster> livingMonsters;
    private FastList<Collectable> magneticCollectables;
    public FastList<Chest> chests; 
    private float timeSinceLastMonsterSpawned;
    private float timeSinceLastChestSpawned;
    private float screenWidthWorldSpace, screenHeightWorldSpace, screenDiagonalWorldSpace;
    private float minSpawnDistance;
    private Coroutine flashCoroutine;
    private Coroutine shockwave;
    private SpatialHashGrid grid;
    private ChestBlueprint defaultChestBlueprint;
    private int eliteAliveCount;
    public FastList<Monster> LivingMonsters { get => livingMonsters; }
    /// <summary>精英死亡保底宝箱用的蓝图（来自当前关卡）。</summary>
    public ChestBlueprint DefaultChestBlueprint { get => defaultChestBlueprint; }
    /// <summary>场上存活的精英数量，用于 maxEliteAlive 限制。</summary>
    public int EliteAliveCount { get => eliteAliveCount; }
    public FastList<Collectable> MagneticCollectables { get => magneticCollectables; }
    public Inventory Inventory { get => inventory; }
    public AbilitySelectionDialog AbilitySelectionDialog { get; private set; }
    public SpatialHashGrid Grid { get => grid; }
    public Vector2 PlayerPosition => (Vector2)playerCharacter.CenterTransform.position;

    /// <summary>消耗品类型名 → 已加载预制体（引用计数由 ResManager 持有，场景卸载时统一释放）。</summary>
    private Dictionary<string, GameObject> collectablePrefabByName;
    private Transform collectableSpawnParent;

    /// <summary>
    /// 异步初始化：怪物/Boss/掉落物预制体全部经 ResManager 按引用计数加载，
    /// 加载完成后再建对象池。调用方（LevelManager）用协程等待整个链路。
    /// </summary>
    public IEnumerator InitRoutine(LevelBlueprint levelBlueprint, Character character, Inventory inventory, StatsManager statsManager, InfiniteBackground infiniteBackground, AbilitySelectionDialog abilitySelectionDialog)
    {
        this.playerCharacter = character;
        this.inventory = inventory;
        this.infiniteBackground = infiniteBackground;
        this.statsManager = statsManager;
        this.defaultChestBlueprint = levelBlueprint != null ? levelBlueprint.chestBlueprint : null;
        this.eliteAliveCount = 0;
        AbilitySelectionDialog = abilitySelectionDialog;

        // 计算屏幕在世界空间中的尺寸，以便在屏幕之外刷怪
        Vector2 bottomLeft = playerCamera.ViewportToWorldPoint(new Vector3(0, 0, playerCamera.nearClipPlane));
        Vector2 topRight = playerCamera.ViewportToWorldPoint(new Vector3(1, 1, playerCamera.nearClipPlane));
        screenWidthWorldSpace = topRight.x - bottomLeft.x;
        screenHeightWorldSpace = topRight.y - bottomLeft.y;
        screenDiagonalWorldSpace = (topRight - bottomLeft).magnitude;
        minSpawnDistance = screenDiagonalWorldSpace/2;

        // 初始化快速列表
        livingMonsters = new FastList<Monster>();
        magneticCollectables = new FastList<Collectable>();
        chests = new FastList<Chest>();

        // 每个怪物容器一个对象池：预制体按引用计数加载，池生命周期内借用（不重复加载）
        // 加载策略：所有预制体一次性并发发起（Addressables 底层并行执行），总耗时≈最慢一个而非逐个相加
        // 槽位顺序：monsters…, boss, expGem, coin, chest, damageText, collectables…
        var requests = new List<(int slot, string address)>();
        int slot = 0;
        for (int i = 0; i < levelBlueprint.monsters.Length; i++)
            requests.Add((slot++, GetPrefabAddress(levelBlueprint.monsters[i].monstersPrefab)));
        requests.Add((slot++, GetPrefabAddress(levelBlueprint.finalBoss.bossPrefab)));
        requests.Add((slot++, GetPrefabAddress(expGemPrefab)));
        requests.Add((slot++, GetPrefabAddress(coinPrefab)));
        requests.Add((slot++, GetPrefabAddress(chestPrefab)));
        requests.Add((slot++, GetPrefabAddress(textPrefab)));
        if (collectableTypes != null)
        {
            foreach (CollectableType type in collectableTypes)
            {
                if (type == null) continue;
                requests.Add((slot++, GetPrefabAddress(type.itemPrefab)));
            }
        }

        GameObject[] prefabs = new GameObject[slot];
        int pendingCount = 0;
        foreach (var (requestSlot, requestAddress) in requests)
        {
            if (string.IsNullOrEmpty(requestAddress)) continue;  // 未赋值引用：GetPrefabAddress 已报错，槽位保持 null
            pendingCount++;
            int index = requestSlot;
            ResManager.AssetLoadAsync<GameObject>(requestAddress, p => { prefabs[index] = p; pendingCount--; });
        }
        while (pendingCount > 0)    // 等全部并发加载完成（成功回填预制体，失败回填 null）
            yield return null;

        int cursor = 0;
        monsterPools = new MonsterPool[levelBlueprint.monsters.Length + 1];
        for (int i = 0; i < levelBlueprint.monsters.Length; i++)
        {
            monsterPools[i] = monsterPoolParent.AddComponent<MonsterPool>();
            monsterPools[i].Init(this, playerCharacter, prefabs[cursor++]);
        }
        monsterPools[monsterPools.Length-1] = monsterPoolParent.AddComponent<MonsterPool>();
        monsterPools[monsterPools.Length-1].Init(this, playerCharacter, prefabs[cursor++]);
        // 为每种远程弹丸类型初始化一个对象池
        projectileIndexByPrefab = new Dictionary<GameObject, int>();
        projectilePools = new List<ProjectilePool>();
        // 为每种投掷物类型初始化一个对象池
        throwableIndexByPrefab = new Dictionary<GameObject, int>();
        throwablePools = new List<ThrowablePool>();
        // 为每种回旋镖类型初始化一个对象池
        boomerangIndexByPrefab = new Dictionary<GameObject, int>();
        boomerangPools = new List<BoomerangPool>();
        // 初始化剩余的一次性对象池（掉落物预制体同样走引用计数加载）
        expGemPool.Init(this, playerCharacter, prefabs[cursor++]);
        coinPool.Init(this, playerCharacter, prefabs[cursor++]);
        chestPool.Init(this, playerCharacter, prefabs[cursor++]);
        textPool.Init(this, playerCharacter, prefabs[cursor++]);

        // 初始化空间哈希网格
        Vector2[] bounds = new Vector2[] { (Vector2)playerCharacter.transform.position - gridSize/2, (Vector2)playerCharacter.transform.position + gridSize/2 };
        grid = new SpatialHashGrid(bounds, gridDimensions);
        collectableSpawnParent = new GameObject("Collectables").transform;
        collectableSpawnParent.SetParent(transform);

        // 消耗品（商店购买投放用）：预加载全部类型的拾取物预制体，按类型名注册
        collectablePrefabByName = new Dictionary<string, GameObject>();
        if (collectableTypes != null)
        {
            foreach (CollectableType type in collectableTypes)
            {
                if (type == null) continue;
                GameObject prefab = prefabs[cursor++];
                if (prefab != null)
                    collectablePrefabByName[type.name] = prefab;
            }
        }
    }

    /// <summary>AssetReference → Addressables 地址（GUID 本身就是合法的加载 key）。</summary>
    private static string GetPrefabAddress(AssetReferenceGameObject reference)
    {
        if (reference == null || string.IsNullOrEmpty(reference.AssetGUID))
        {
            Debug.LogError("[EntityManager] 存在未赋值的可寻址预制体引用，请运行迁移菜单或检查 Inspector");
            return null;
        }
        return reference.AssetGUID;
    }

    /// <summary>
    /// 场景卸载时释放本局借用的预制体引用。
    /// </summary>
    private void OnDestroy()
    {
        // 场景卸载（通关/死亡/返回主菜单/重开）时统一释放本局借用的预制体引用。
        // 对象池随本组件销毁，预制体不会在释放后被再次实例化。
        ResManager.ReleaseSession();
    }

    /// <summary>
    /// 玩家靠近空间网格边缘时重建网格（异步初始化完成前跳过）。
    /// </summary>
    void Update()
    {
        // 玩家靠近网格边缘时重建网格
        if (grid == null) return;  // 异步初始化（建池/建网格）完成前不重建
        if (grid.CloseToEdge(playerCharacter))
        {
            grid.Rebuild(playerCharacter.transform.position);
        }
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 特殊功能
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public void CollectAllCoinsAndGems()
    {
        if (shockwave != null) StopCoroutine(shockwave);
        shockwave = StartCoroutine(infiniteBackground.Shockwave(screenDiagonalWorldSpace/2));
        foreach (Collectable collectable in magneticCollectables.ToList())
        {
            collectable.Collect();
        }
    }

    /// <summary>
    /// 播放闪屏，并对屏幕内所有怪物造成伤害。
    /// </summary>
    public void DamageAllVisibileEnemies(float damage)
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(Flash());
        foreach (Monster monster in livingMonsters.ToList() )
        {
            if (TransformOnScreen(monster.transform, Vector2.one))
                monster.TakeDamage(damage, Vector2.zero);
        }
    }

    /// <summary>
    /// 让场上所有非 Boss 怪物直接死亡。
    /// </summary>
    public void KillAllMonsters()
    {
        foreach (Monster monster in livingMonsters.ToList() )
        {
            if (!(monster as BossMonster))
                StartCoroutine(monster.Killed(false));
        }
    }

    /// <summary>
    /// 全屏白图淡出的闪屏动画。
    /// </summary>
    private IEnumerator Flash()
    {
        flashSpriteRenderer.enabled = true;
        float t = 0;
        while (t < 1)
        {
            flashSpriteRenderer.color = new Color(1, 1, 1, 1-EasingUtils.EaseOutQuart(t));
            t += Time.unscaledDeltaTime * 4;
            yield return null;
        }
        flashSpriteRenderer.enabled = false;
    }

    /// <summary>
    /// 判断目标是否位于玩家可见范围内（可附加缓冲距离）。
    /// </summary>
    public bool TransformOnScreen(Transform transform, Vector2 buffer = default(Vector2))
    {
        return (
            transform.position.x > playerCharacter.transform.position.x - screenWidthWorldSpace/2 - buffer.x &&
            transform.position.x < playerCharacter.transform.position.x + screenWidthWorldSpace/2 + buffer.x &&
            transform.position.y > playerCharacter.transform.position.y - screenHeightWorldSpace/2 - buffer.y &&
            transform.position.y < playerCharacter.transform.position.y + screenHeightWorldSpace/2 + buffer.y
        );
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 怪物刷新
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public Monster SpawnMonsterRandomPosition(int monsterPoolIndex, MonsterBlueprint monsterBlueprint, float hpBuff = 0, EliteSpawnInfo eliteInfo = default(EliteSpawnInfo))
    {
        // 在屏幕外随机找一个位置


        Vector2 spawnPosition = (playerCharacter.Velocity != Vector2.zero) ? GetRandomMonsterSpawnPositionPlayerVelocity() : GetRandomMonsterSpawnPosition();
        // Vector2 spawnDirection = Random.insideUnitCircle.normalized;
        // Vector2 spawnPosition = (Vector2)playerCharacter.transform.position + spawnDirection * (minSpawnDistance + monsterSpawnBufferDistance);
        // 生成怪物
        return SpawnMonster(monsterPoolIndex, spawnPosition, monsterBlueprint, hpBuff, eliteInfo);
    }

    /// <summary>
    /// 从对象池取出怪物并初始化，同时登记到空间网格与精英计数。
    /// </summary>
    public Monster SpawnMonster(int monsterPoolIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0, EliteSpawnInfo eliteInfo = default(EliteSpawnInfo))
    {
        Monster newMonster = monsterPools[monsterPoolIndex].Get();
        newMonster.Setup(monsterPoolIndex, position, monsterBlueprint, hpBuff, eliteInfo);
        if (newMonster.IsElite)
            eliteAliveCount++;
        grid.InsertClient(newMonster);
        return newMonster;
    }

    /// <summary>
    /// 统计击杀、维护精英计数与网格，并把怪物归还对象池。
    /// </summary>
    public void DespawnMonster(int monsterPoolIndex, Monster monster, bool killedByPlayer = true)
    {
        if (killedByPlayer)
        {
            statsManager.IncrementMonstersKilled();
        }
        if (monster.IsElite && eliteAliveCount > 0)
            eliteAliveCount--;
        grid.RemoveClient(monster);
        monsterPools[monsterPoolIndex].Release(monster);
    }

    /// <summary>
    /// 在屏幕外随机取一个刷怪点（含在屏幕外的缓冲距离）。
    /// </summary>
    private Vector2 GetRandomMonsterSpawnPosition()
    {
        Vector2[] sideDirections = new Vector2[] { Vector2.left, Vector2.up, Vector2.right, Vector2.down };
        int sideIndex = Random.Range(0,4);
        Vector2 spawnPosition;
        if (sideIndex % 2 == 0)
        {
            spawnPosition = (Vector2)playerCharacter.transform.position + sideDirections[sideIndex] * (screenWidthWorldSpace/2+monsterSpawnBufferDistance) + Vector2.up * Random.Range(-screenHeightWorldSpace/2-monsterSpawnBufferDistance, screenHeightWorldSpace/2+monsterSpawnBufferDistance);
        }
        else
        {
            spawnPosition = (Vector2)playerCharacter.transform.position + sideDirections[sideIndex] * (screenHeightWorldSpace/2+monsterSpawnBufferDistance) + Vector2.right * Random.Range(-screenWidthWorldSpace/2-monsterSpawnBufferDistance, screenWidthWorldSpace/2+monsterSpawnBufferDistance);
        }
        return spawnPosition;
    }

    /// <summary>
    /// 按玩家移动方向加权选择刷怪边，让怪物更多出现在前进方向前方。
    /// </summary>
    private Vector2 GetRandomMonsterSpawnPositionPlayerVelocity()
    {
        Vector2[] sideDirections = new Vector2[] { Vector2.left, Vector2.up, Vector2.right, Vector2.down };

        float[] sideWeights = new float[]
        {
            Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[0]),
            Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[1]),
            Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[2]),
            Vector2.Dot(playerCharacter.Velocity.normalized, sideDirections[3])
        };
        float extraWeight = sideWeights.Sum()/playerDirectionSpawnWeight;
        int badSideCount = sideWeights.Where(x => x <= 0).Count();
        for (int i = 0; i < sideWeights.Length; i++)
        {
            if (sideWeights[i] <= 0)
                sideWeights[i] = extraWeight / badSideCount; 
        }
        float totalSideWeight = sideWeights.Sum();

        float rand = Random.Range(0f, totalSideWeight);
        float cumulative = 0;
        int sideIndex = -1;
        for (int i = 0; i < sideWeights.Length; i++)
        {
            cumulative += sideWeights[i];
            if (rand < cumulative)
            {
                sideIndex = i;
                break;
            }
        }

        Vector2 spawnPosition;
        if (sideIndex % 2 == 0)
        {
            spawnPosition = (Vector2)playerCharacter.transform.position + sideDirections[sideIndex] * (screenWidthWorldSpace/2+monsterSpawnBufferDistance) + Vector2.up * Random.Range(-screenHeightWorldSpace/2-monsterSpawnBufferDistance, screenHeightWorldSpace/2+monsterSpawnBufferDistance);
        }
        else
        {
            spawnPosition = (Vector2)playerCharacter.transform.position + sideDirections[sideIndex] * (screenHeightWorldSpace/2+monsterSpawnBufferDistance) + Vector2.right * Random.Range(-screenWidthWorldSpace/2-monsterSpawnBufferDistance, screenWidthWorldSpace/2+monsterSpawnBufferDistance);
        }
        return spawnPosition;
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 经验宝石刷新
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public ExpGem SpawnExpGem(Vector2 position, GemType gemType = GemType.White1, bool spawnAnimation = true)
    {
        ExpGem newGem = expGemPool.Get();
        newGem.Setup(position, gemType, spawnAnimation);
        return newGem;
    }

    /// <summary>
    /// 回收经验宝石。
    /// </summary>
    public void DespawnGem(ExpGem gem)
    {
        expGemPool.Release(gem);
    }

    /// <summary>
    /// 在玩家周围随机生成若干经验宝石。
    /// </summary>
    public void SpawnGemsAroundPlayer(int gemCount, GemType gemType = GemType.White1)
    {
        for (int i = 0; i < gemCount; i++)
        {
            Vector2 spawnDirection = Random.insideUnitCircle.normalized;
            Vector2 spawnPosition = (Vector2)playerCharacter.transform.position + spawnDirection * Mathf.Sqrt(Random.Range(1, Mathf.Pow(minSpawnDistance, 2)));
            SpawnExpGem(spawnPosition, gemType, false);
        }
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 金币刷新
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public Coin SpawnCoin(Vector2 position, CoinType coinType = CoinType.Bronze1, bool spawnAnimation = true)
    {
        Coin newCoin = coinPool.Get();
        newCoin.Setup(position, coinType, spawnAnimation);
        return newCoin;
    }

    /// <summary>
    /// 回收金币；玩家拾取时计入本局金币收益。
    /// </summary>
    public void DespawnCoin(Coin coin, bool pickedUpByPlayer = true)
    {
        if (pickedUpByPlayer)
        {
            statsManager.IncreaseCoinsGained((int)coin.CoinType);
        }
        coinPool.Release(coin);
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 宝箱刷新
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public Chest SpawnChest(ChestBlueprint chestBlueprint)
    {
        Chest newChest = chestPool.Get();
        newChest.Setup(chestBlueprint);
        // 确保宝箱不会与其他宝箱重叠
        bool overlapsOtherChest = false;
        int tries = 0;
        do
        {
            Vector2 spawnDirection = Random.insideUnitCircle.normalized;
            Vector2 spawnPosition = (Vector2)playerCharacter.transform.position + spawnDirection * (minSpawnDistance + monsterSpawnBufferDistance + Random.Range(0, chestSpawnRange));
            newChest.transform.position = spawnPosition;
            overlapsOtherChest = false;
            foreach (Chest chest in chests)
            {
                if (Vector2.Distance(chest.transform.position, spawnPosition) < 0.5f)
                {
                    overlapsOtherChest = true;
                    break;
                }
            }
        } while (overlapsOtherChest && tries++ < 100);
        chests.Add(newChest);
        return newChest;
    }

    /// <summary>
    /// 在指定位置生成宝箱。
    /// </summary>
    public Chest SpawnChest(ChestBlueprint chestBlueprint, Vector2 position)
    {
        Chest newChest = chestPool.Get();
        newChest.transform.position = position;
        newChest.Setup(chestBlueprint);
        chests.Add(newChest);
        return newChest;
    }

    /// <summary>
    /// 回收宝箱。
    /// </summary>
    public void DespawnChest(Chest chest)
    {
        chests.Remove(chest);
        chestPool.Release(chest);
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 拾取物刷新
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public void SpawnCollectablesFromSession(Vector2 position)
    {
        var entries = RunState.TakePurchasedConsumableEntries();
        if (entries.Count == 0) return;

        // 待投放条目已被取走，同步落盘，避免强杀游戏后重登又发一份
        ShopSession.Save();
        foreach (var entry in entries)
        {
            var parts = entry.Split(':');
            if (parts.Length != 2) continue;
            // 条目用「类型资产名」做 key（跨会话稳定），不再用运行时 hash
            string typeName = parts[0];
            if (!int.TryParse(parts[1], out int amount)) continue;
            if (!collectablePrefabByName.TryGetValue(typeName, out var prefab))
            {
                Debug.LogWarning($"[EntityManager] 消耗品类型未注册或预制体未加载：{typeName}");
                continue;
            }
            for (int i = 0; i < amount; i++)
            {
                Vector2 spawnPos = position + Random.insideUnitCircle * 1.5f;
                GameObject go = Instantiate(prefab, collectableSpawnParent);
                go.transform.position = spawnPos;
                Collectable collectable = go.GetComponent<Collectable>();
                if (collectable != null)
                {
                    collectable.Init(this, playerCharacter);
                    collectable.Setup(true, true);
                }
            }
        }
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 飘字刷新
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public DamageText SpawnDamageText(Vector2 position, float damage)
    {
        DamageText newText = textPool.Get();
        newText.Setup(position, damage);
        return newText;
    }

    /// <summary>
    /// 回收伤害飘字。
    /// </summary>
    public void DespawnDamageText(DamageText text)
    {
        textPool.Release(text);
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 弹丸刷新
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public Projectile SpawnProjectile(int projectileIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
    {
        Projectile projectile = projectilePools[projectileIndex].Get();
        projectile.Setup(projectileIndex, position, damage, knockback, speed, targetLayer);
        return projectile;
    }
    
    /// <summary>
    /// 回收弹丸。
    /// </summary>
    public void DespawnProjectile(int projectileIndex, Projectile projectile)
    {
        projectilePools[projectileIndex].Release(projectile);
    }

    /// <summary>
    /// 为指定弹丸预制体注册对象池并返回其下标（已注册则复用）。
    /// </summary>
    public int AddPoolForProjectile(GameObject projectilePrefab)
    {
        if (!projectileIndexByPrefab.ContainsKey(projectilePrefab))
        {
            projectileIndexByPrefab[projectilePrefab] = projectilePools.Count;
            ProjectilePool projectilePool = projectilePoolParent.AddComponent<ProjectilePool>();
            projectilePool.Init(this, playerCharacter, projectilePrefab);
            projectilePools.Add(projectilePool);
            return projectilePools.Count - 1;
        }
        return projectileIndexByPrefab[projectilePrefab];
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 投掷物刷新
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public Throwable SpawnThrowable(int throwableIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
    {
        Throwable throwable = throwablePools[throwableIndex].Get();
        throwable.Setup(throwableIndex, position, damage, knockback, speed, targetLayer);
        return throwable;
    }

    /// <summary>
    /// 回收投掷物。
    /// </summary>
    public void DespawnThrowable(int throwableIndex, Throwable throwable)
    {
        throwablePools[throwableIndex].Release(throwable);
    }

    /// <summary>
    /// 为指定投掷物预制体注册对象池并返回其下标。
    /// </summary>
    public int AddPoolForThrowable(GameObject throwablePrefab)
    {
        if (!throwableIndexByPrefab.ContainsKey(throwablePrefab))
        {
            throwableIndexByPrefab[throwablePrefab] = throwablePools.Count;
            ThrowablePool throwablePool = throwablePoolParent.AddComponent<ThrowablePool>();
            throwablePool.Init(this, playerCharacter, throwablePrefab);
            throwablePools.Add(throwablePool);
            return throwablePools.Count - 1;
        }
        return throwableIndexByPrefab[throwablePrefab];
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 回旋镖刷新
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public Boomerang SpawnBoomerang(int boomerangIndex, Vector2 position, float damage, float knockback, float throwDistance, float throwTime, LayerMask targetLayer)
    {
        Boomerang boomerang = boomerangPools[boomerangIndex].Get();
        boomerang.Setup(boomerangIndex, position, damage, knockback, throwDistance, throwTime, targetLayer);
        return boomerang;
    }

    /// <summary>
    /// 回收回旋镖。
    /// </summary>
    public void DespawnBoomerang(int boomerangIndex, Boomerang boomerang)
    {
        boomerangPools[boomerangIndex].Release(boomerang);
    }

    /// <summary>
    /// 为指定回旋镖预制体注册对象池并返回其下标。
    /// </summary>
    public int AddPoolForBoomerang(GameObject boomerangPrefab)
    {
        if (!boomerangIndexByPrefab.ContainsKey(boomerangPrefab))
        {
            boomerangIndexByPrefab[boomerangPrefab] = boomerangPools.Count;
            BoomerangPool boomerangPool = boomerangPoolParent.AddComponent<BoomerangPool>();
            boomerangPool.Init(this, playerCharacter, boomerangPrefab);
            boomerangPools.Add(boomerangPool);
            return boomerangPools.Count - 1;
        }
        return boomerangIndexByPrefab[boomerangPrefab];
    }
}
