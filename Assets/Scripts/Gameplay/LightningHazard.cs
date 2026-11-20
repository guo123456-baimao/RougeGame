using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 相位3「虚空」专用：把屏幕划成若干块，每隔一段时间随机选多块播放闪电，
/// 区域内玩家与普通敌人按固定间隔持续扣最大生命百分比。
/// </summary>
public class LightningHazard : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("闪电触发间隔。需要小于 effectDuration，否则“再次命中延长持续时间”不会触发。")]
    [SerializeField] private float triggerInterval = 2f;
    [SerializeField] private float effectDuration = 3f;
    [SerializeField, Range(0f, 1f)] private float maxHealthDamagePerTick = 0.03f;
    [SerializeField, Min(0.05f)] private float damageInterval = 0.5f;

    [Header("Area")]
    [SerializeField] private int gridColumns = 3;
    [SerializeField] private int gridRows = 2;
    [SerializeField] private int areasPerTrigger = 2;
    [SerializeField] private float areaRadius = 1.75f;

    [Header("Visual")]
    [SerializeField] private int fps = 30;
    [SerializeField] private float pixelsPerUnit = 100f;
    [SerializeField, Min(1f)] private float visualRadiusPixels = 62f;
    [SerializeField] private int sortingOrder = 30;

    [Header("Range Indicator")]
    [SerializeField] private Color rangeOutlineColor = new Color(1f, 1f, 1f, 0.95f);
    [Tooltip("亮环厚度占半径的比例。越小越细。")]
    [SerializeField, Range(0.01f, 0.2f)] private float rangeOutlineThickness = 0.03f;
    [SerializeField, Range(0f, 1f)] private float rangeFillAlpha = 0.14f;
    [SerializeField] private int rangeSortingOrder = -4;

    private class AreaVisual
    {
        public GameObject root;
        public SpriteRenderer renderer;
        public GameObject rangeRoot;
        public SpriteRenderer rangeRenderer;
    }

    /// <summary>
    /// 单个目标的闪电伤害状态。用“剩余结算次数 + 下次结算时间”控制，
    /// 避免绝对时间比较在 3.0s / 0.5s = 6 次的边界上漏掉最后一次。
    /// </summary>
    private class DamageState
    {
        public float nextTickTime;
        public int remainingTicks;
    }

    private EntityManager entityManager;
    private Character playerCharacter;
    private Sprite[] frames;
    private Texture2D rangeTexture;
    private Sprite rangeSprite;

    private readonly List<AreaVisual> visuals = new List<AreaVisual>();
    private readonly Dictionary<Character, DamageState> playerDamageStates = new Dictionary<Character, DamageState>();
    private readonly Dictionary<Monster, DamageState> monsterDamageStates = new Dictionary<Monster, DamageState>();
    private Coroutine loopRoutine;
    private Coroutine damageRoutine;
    private Coroutine[] visualRoutines;
    private bool running;

    private int TicksPerEffect => Mathf.Max(1, Mathf.RoundToInt(effectDuration / Mathf.Max(0.01f, damageInterval)));

    public void Init(EntityManager entities, Character player, Sprite[] lightningFrames)
    {
        entityManager = entities;
        playerCharacter = player;
        frames = lightningFrames;
        BuildRangeSprite();
        BuildVisual();
    }

    public void Begin()
    {
        if (running || frames == null || frames.Length == 0
            || entityManager == null || playerCharacter == null)
            return;
        running = true;
        loopRoutine = StartCoroutine(LightningLoop());
        damageRoutine = StartCoroutine(DamageTick());
    }

    public void Stop()
    {
        running = false;
        if (loopRoutine != null) StopCoroutine(loopRoutine);
        loopRoutine = null;
        if (visualRoutines != null)
            for (int i = 0; i < visualRoutines.Length; i++)
                if (visualRoutines[i] != null) StopCoroutine(visualRoutines[i]);
        if (damageRoutine != null) StopCoroutine(damageRoutine);
        damageRoutine = null;
        playerDamageStates.Clear();
        monsterDamageStates.Clear();
        foreach (AreaVisual visual in visuals)
        {
            if (visual.root != null) visual.root.SetActive(false);
            if (visual.rangeRoot != null) visual.rangeRoot.SetActive(false);
        }
    }

    private IEnumerator LightningLoop()
    {
        while (running)
        {
            List<Vector2> centers = PickRandomAreaCenters(visuals.Count);
            for (int i = 0; i < centers.Count && i < visuals.Count; i++)
            {
                visualRoutines[i] = StartCoroutine(PlayVisual(centers[i], visuals[i]));
                MarkDamage(centers[i]);
            }
            yield return new WaitForSeconds(Mathf.Max(0.1f, triggerInterval));
        }
    }

    private List<Vector2> PickRandomAreaCenters(int count)
    {
        var result = new List<Vector2>();
        Vector2 origin = playerCharacter != null && playerCharacter.CenterTransform != null
            ? (Vector2)playerCharacter.CenterTransform.position
            : Vector2.zero;

        int cols = Mathf.Max(1, gridColumns), rows = Mathf.Max(1, gridRows);
        var cells = new List<Vector2Int>();
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
                cells.Add(new Vector2Int(col, row));
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2Int tmp = cells[i];
            cells[i] = cells[j];
            cells[j] = tmp;
        }

        Camera cam = Camera.main;
        float cellWidth = 1f, cellHeight = 1f;
        if (cam != null)
        {
            Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0f, cam.nearClipPlane));
            Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1f, 1f, cam.nearClipPlane));
            cellWidth = Mathf.Max(0.1f, (topRight.x - bottomLeft.x) / cols);
            cellHeight = Mathf.Max(0.1f, (topRight.y - bottomLeft.y) / rows);
        }

        int take = Mathf.Min(Mathf.Max(0, count), cells.Count);
        for (int i = 0; i < take; i++)
        {
            Vector2Int cell = cells[i];
            Vector2 offset = new Vector2(
                (cell.x + 0.5f - cols * 0.5f) * cellWidth,
                (cell.y + 0.5f - rows * 0.5f) * cellHeight);
            result.Add(origin + offset);
        }
        return result;
    }

    private IEnumerator PlayVisual(Vector2 center, AreaVisual visual)
    {
        if (visual == null || visual.root == null || visual.renderer == null || frames.Length == 0)
            yield break;

        visual.root.transform.position = new Vector3(center.x, center.y, -1f);
        // 让特效的可见边界和 areaRadius 严格一致。
        visual.root.transform.localScale = Vector3.one * Mathf.Max(
            0.01f,
            areaRadius * Mathf.Max(1f, pixelsPerUnit) / Mathf.Max(1f, visualRadiusPixels));
        visual.root.SetActive(true);

        if (visual.rangeRoot != null && visual.rangeRenderer != null)
        {
            visual.rangeRoot.transform.position = new Vector3(center.x, center.y, -0.9f);
            // rangeSprite 的世界半径为 1，乘以 areaRadius 后与伤害判定圆严格一致。
            visual.rangeRoot.transform.localScale = Vector3.one * Mathf.Max(0.01f, areaRadius);
            visual.rangeRenderer.color = rangeOutlineColor;
            visual.rangeRoot.SetActive(true);
        }

        float elapsed = 0f, lastFrame = -1f;
        while (running && elapsed < effectDuration)
        {
            int index = Mathf.FloorToInt(elapsed * Mathf.Max(1, fps));
            if (index != lastFrame)
            {
                visual.renderer.sprite = frames[index % frames.Length];
                lastFrame = index;
            }
            if (visual.rangeRoot != null && visual.rangeRoot.activeSelf)
            {
                float pulse = 1f + 0.04f * Mathf.Sin(elapsed * 12f);
                visual.rangeRoot.transform.localScale = Vector3.one * Mathf.Max(0.01f, areaRadius * pulse);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        visual.root.SetActive(false);
        if (visual.rangeRoot != null) visual.rangeRoot.SetActive(false);
    }

    /// <summary>
    /// 命中区域时登记玩家与普通敌人的伤害状态。
    /// 若目标已处于闪电伤害状态，则延长剩余次数而不是重开一个独立循环。
    /// </summary>
    private void MarkDamage(Vector2 center)
    {
        float radiusSquared = areaRadius * areaRadius;
        float now = Time.time;

        if (playerCharacter != null && playerCharacter.CenterTransform != null
            && ((Vector2)playerCharacter.CenterTransform.position - center).sqrMagnitude <= radiusSquared)
            AddDamageState(playerCharacter, now);

        FastList<Monster> monsters = entityManager?.LivingMonsters;
        if (monsters == null) return;
        foreach (Monster monster in new List<Monster>(monsters))
        {
            if (monster == null || !monster.Alive || !monster.gameObject.activeInHierarchy
                || monster.IsElite || monster is BossMonster)
                continue;
            if (monster.CenterTransform == null
                || ((Vector2)monster.CenterTransform.position - center).sqrMagnitude > radiusSquared)
                continue;
            AddDamageState(monster, now);
        }
    }

    private void AddDamageState(Character target, float now)
    {
        if (playerDamageStates.TryGetValue(target, out DamageState state))
            state.remainingTicks += TicksPerEffect;
        else
            playerDamageStates[target] = new DamageState {
                nextTickTime = now + damageInterval,
                remainingTicks = TicksPerEffect
            };
    }

    private void AddDamageState(Monster target, float now)
    {
        if (monsterDamageStates.TryGetValue(target, out DamageState state))
            state.remainingTicks += TicksPerEffect;
        else
            monsterDamageStates[target] = new DamageState {
                nextTickTime = now + damageInterval,
                remainingTicks = TicksPerEffect
            };
    }

    /// <summary>
    /// 统一按固定间隔结算所有仍处于闪电伤害状态的目标。
    /// 每个目标按自己的 nextTickTime 结算，保证命中后恰好得到 TicksPerEffect 次飘字。
    /// </summary>
    private IEnumerator DamageTick()
    {
        while (running)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, damageInterval));
            if (!running) yield break;

            float now = Time.time;
            float damagePerTick = Mathf.Max(0f, maxHealthDamagePerTick);

            if (playerCharacter != null && playerDamageStates.TryGetValue(playerCharacter, out DamageState playerState))
            {
                while (playerState.remainingTicks > 0 && now >= playerState.nextTickTime)
                {
                    float damage = Mathf.Max(1f, playerCharacter.MaxHealth * damagePerTick);
                    playerCharacter.TakeTrueDamage(damage);
                    entityManager?.SpawnDamageText(playerCharacter.CenterTransform.position, damage);
                    playerState.remainingTicks--;
                    playerState.nextTickTime += damageInterval;
                    if (!running) yield break;
                }
                if (playerState.remainingTicks <= 0)
                    playerDamageStates.Remove(playerCharacter);
            }

            if (monsterDamageStates.Count > 0)
            {
                // 伤害可能让怪物回池并修改集合，先快照再遍历
                foreach (Monster monster in new List<Monster>(monsterDamageStates.Keys))
                {
                    if (monster == null || !monster.Alive || !monster.gameObject.activeInHierarchy
                        || !monsterDamageStates.TryGetValue(monster, out DamageState state))
                        continue;

                    while (state.remainingTicks > 0 && now >= state.nextTickTime)
                    {
                        monster.TakeDamage(Mathf.Max(1f, monster.MaxHealth * damagePerTick));
                        state.remainingTicks--;
                        state.nextTickTime += damageInterval;
                        if (!running) yield break;
                    }
                    if (state.remainingTicks <= 0)
                        monsterDamageStates.Remove(monster);
                }
            }
        }
    }

    /// <summary>
    /// 程序化生成白色圆形范围精灵（外圈亮环 + 内部淡填充）。
    /// 世界半径恰好为 1，缩放 areaRadius 后与伤害判定圆一致；不依赖外部资源。
    /// </summary>
    private void BuildRangeSprite()
    {
        if (rangeSprite != null) return;
        const int size = 128;
        rangeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        rangeTexture.filterMode = FilterMode.Bilinear;
        rangeTexture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        float outer = center - 1f;
        float inner = outer * (1f - Mathf.Clamp01(rangeOutlineThickness));
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                Color c;
                if (dist > outer) c = Color.clear;
                else if (dist >= inner) c = new Color(1f, 1f, 1f, 0.95f);
                else c = new Color(1f, 1f, 1f, rangeFillAlpha);
                pixels[y * size + x] = c;
            }
        }
        rangeTexture.SetPixels(pixels);
        rangeTexture.Apply();
        // PPU = size / 2 => 世界直径 2、半径 1
        rangeSprite = Sprite.Create(rangeTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size / 2f);
    }

    private void BuildVisual()
    {
        if (visuals.Count > 0) return;
        int maxAreas = Mathf.Max(1, gridColumns) * Mathf.Max(1, gridRows);
        int count = Mathf.Clamp(areasPerTrigger, 1, maxAreas);
        for (int i = 0; i < count; i++)
        {
            GameObject root = new GameObject($"Lightning {i}");
            root.transform.SetParent(transform, false);
            root.SetActive(false);
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            GameObject rangeRoot = new GameObject($"Lightning Range {i}");
            rangeRoot.transform.SetParent(transform, false);
            rangeRoot.SetActive(false);
            SpriteRenderer rangeRenderer = rangeRoot.AddComponent<SpriteRenderer>();
            rangeRenderer.sortingOrder = rangeSortingOrder;
            if (rangeSprite != null) rangeRenderer.sprite = rangeSprite;
            rangeRenderer.color = rangeOutlineColor;

            visuals.Add(new AreaVisual {
                root = root, renderer = renderer,
                rangeRoot = rangeRoot, rangeRenderer = rangeRenderer
            });
        }
        visualRoutines = new Coroutine[count];
    }

    private void OnDestroy()
    {
        Stop();
        if (rangeSprite != null) Destroy(rangeSprite);
        if (rangeTexture != null) Destroy(rangeTexture);
    }
}
