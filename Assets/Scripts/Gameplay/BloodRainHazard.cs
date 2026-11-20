using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 相位2「血色地」专用：把屏幕划成若干块，每隔一段时间随机选多块播放血雨，
/// 并在持续时间内按最大生命百分比对区域内玩家与普通敌人结算伤害。
/// </summary>
public class BloodRainHazard : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float triggerInterval = 5f;
    [SerializeField] private float effectDuration = 3f;
    [SerializeField] private float damageInterval = 1f;
    [SerializeField, Range(0f, 1f)] private float maxHealthDamagePerSecond = 0.05f;

    [Header("Area")]
    [SerializeField] private int gridColumns = 3;
    [SerializeField] private int gridRows = 2;
    [SerializeField] private int areasPerTrigger = 3;
    [SerializeField] private float areaRadius = 1.75f;

    [Header("Visual")]
    [SerializeField] private int fps = 30;
    [SerializeField] private float effectScale = 3f;
    [SerializeField] private int groundSortingOrder = -5;
    [SerializeField] private int rainSortingOrder = 30;

    private EntityManager entityManager;
    private Character playerCharacter;
    private Sprite[] groundFrames;
    private Sprite[] rainFrames;

    private class AreaVisual
    {
        public GameObject groundRoot;
        public SpriteRenderer groundRenderer;
        public GameObject rainRoot;
        public SpriteRenderer rainRenderer;
    }

    private readonly List<AreaVisual> visuals = new List<AreaVisual>();
    private Coroutine loopRoutine;
    private Coroutine[] visualRoutines;
    private Coroutine[] damageRoutines;
    private bool running;

    public void Init(EntityManager entities, Character player, Sprite[] ground, Sprite[] rain)
    {
        entityManager = entities;
        playerCharacter = player;
        groundFrames = ground;
        rainFrames = rain;
        BuildVisual();
    }

    public void Begin()
    {
        if (running || groundFrames == null || groundFrames.Length == 0
            || rainFrames == null || rainFrames.Length == 0
            || entityManager == null || playerCharacter == null)
            return;
        running = true;
        loopRoutine = StartCoroutine(BloodRainLoop());
    }

    public void Stop()
    {
        running = false;
        StopSafely(loopRoutine);
        loopRoutine = null;
        if (visualRoutines != null)
        {
            for (int i = 0; i < visualRoutines.Length; i++) StopSafely(visualRoutines[i]);
        }
        if (damageRoutines != null)
        {
            for (int i = 0; i < damageRoutines.Length; i++) StopSafely(damageRoutines[i]);
        }
        foreach (AreaVisual visual in visuals)
        {
            if (visual.groundRoot != null) visual.groundRoot.SetActive(false);
            if (visual.rainRoot != null) visual.rainRoot.SetActive(false);
        }
    }

    private void StopSafely(Coroutine routine)
    {
        if (routine != null) StopCoroutine(routine);
    }

    private IEnumerator BloodRainLoop()
    {
        while (running)
        {
            List<Vector2> centers = PickRandomAreaCenters(visuals.Count);
            for (int i = 0; i < centers.Count && i < visuals.Count; i++)
            {
                visualRoutines[i] = StartCoroutine(PlayVisual(centers[i], visuals[i]));
                damageRoutines[i] = StartCoroutine(ApplyDamage(centers[i]));
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
        if (visual == null || visual.groundRoot == null || visual.rainRoot == null
            || visual.groundRenderer == null || visual.rainRenderer == null
            || groundFrames == null || rainFrames == null
            || groundFrames.Length == 0 || rainFrames.Length == 0)
            yield break;

        Vector3 position = new Vector3(center.x, center.y, -1f);
        Vector3 scale = Vector3.one * Mathf.Max(0.01f, effectScale);
        visual.groundRoot.transform.position = position;
        visual.rainRoot.transform.position = position;
        visual.groundRoot.transform.localScale = scale;
        visual.rainRoot.transform.localScale = scale;
        visual.groundRoot.SetActive(true);
        visual.rainRoot.SetActive(true);

        float elapsed = 0f, lastFrame = -1f;
        while (running && elapsed < effectDuration)
        {
            int index = Mathf.FloorToInt(elapsed * Mathf.Max(1, fps));
            if (index != lastFrame)
            {
                visual.groundRenderer.sprite = groundFrames[index % groundFrames.Length];
                visual.rainRenderer.sprite = rainFrames[index % rainFrames.Length];
                lastFrame = index;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        visual.groundRoot.SetActive(false);
        visual.rainRoot.SetActive(false);
    }

    private IEnumerator ApplyDamage(Vector2 center)
    {
        float rate = Mathf.Max(0f, maxHealthDamagePerSecond);
        float damagePerTick = rate * Mathf.Max(0.01f, damageInterval);
        int ticks = Mathf.Max(1, Mathf.FloorToInt(effectDuration / Mathf.Max(0.01f, damageInterval)));
        float radiusSquared = areaRadius * areaRadius;

        for (int i = 0; i < ticks && running; i++)
        {
            if (playerCharacter != null && playerCharacter.CenterTransform != null
                && ((Vector2)playerCharacter.CenterTransform.position - center).sqrMagnitude <= radiusSquared)
            {
                playerCharacter.TakeTrueDamage(playerCharacter.MaxHealth * damagePerTick);
                if (!running) yield break;
            }

            FastList<Monster> monsters = entityManager.LivingMonsters;
            if (monsters != null)
            {
                // 伤害可能导致怪物回池并修改 LivingMonsters，先快照再遍历
                foreach (Monster monster in new List<Monster>(monsters))
                {
                    if (monster == null || !monster.Alive || monster.IsElite || monster is BossMonster)
                        continue;
                    if (monster.CenterTransform == null
                        || ((Vector2)monster.CenterTransform.position - center).sqrMagnitude > radiusSquared)
                        continue;
                    monster.TakeDamage(monster.MaxHealth * damagePerTick);
                }
            }
            yield return new WaitForSeconds(Mathf.Max(0.01f, damageInterval));
        }
    }

    private void BuildVisual()
    {
        if (visuals.Count > 0) return;
        int maxAreas = Mathf.Max(1, gridColumns) * Mathf.Max(1, gridRows);
        int count = Mathf.Clamp(areasPerTrigger, 1, maxAreas);
        for (int i = 0; i < count; i++)
        {
            var visual = new AreaVisual();
            visual.groundRoot = CreateLayer($"Blood Rain Ground {i}", groundSortingOrder, out visual.groundRenderer);
            visual.rainRoot = CreateLayer($"Blood Rain Rain {i}", rainSortingOrder, out visual.rainRenderer);
            visuals.Add(visual);
        }
        visualRoutines = new Coroutine[count];
        damageRoutines = new Coroutine[count];
    }

    private GameObject CreateLayer(string name, int order, out SpriteRenderer renderer)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(transform, false);
        root.SetActive(false);
        renderer = root.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        return root;
    }

    private void OnDestroy()
    {
        Stop();
    }
}
