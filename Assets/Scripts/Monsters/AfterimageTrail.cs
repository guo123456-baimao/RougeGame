using UnityEngine;
/// <summary>
/// 疾行精英的黑色残影。固定容量的小对象池，稳态运行零 GC 分配。
/// 残影直接复制精英当前帧的 sprite，压暗成黑色后淡出。
/// </summary>
public class AfterimageTrail
{
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[EliteAffixTuning.AfterimageCapacity];
    private readonly float[] lives = new float[EliteAffixTuning.AfterimageCapacity];
    private readonly Transform root;
    private float spawnTimer;
    private int cursor;

    /// <summary>
    /// 创建残影根节点并挂到指定父节点下。
    /// </summary>
    public AfterimageTrail(Transform parent)
    {
        GameObject rootObject = new GameObject("Elite Afterimages");
        if (parent != null)
            rootObject.transform.SetParent(parent, false);
        root = rootObject.transform;
    }

    /// <summary>
    /// 每帧推进残影淡出，并在间隔到达、移动速度足够时生成新残影。
    /// </summary>
    public void Tick(SpriteRenderer source, Vector2 velocity, float deltaTime)
    {
        Fade(deltaTime);

        spawnTimer += deltaTime;
        if (source == null || source.sprite == null || velocity.sqrMagnitude < 0.09f)
            return;
        if (spawnTimer < EliteAffixTuning.AfterimageInterval)
            return;

        spawnTimer = 0f;
        Emit(source);
    }

    /// <summary>
    /// 清空所有残影并重置生成计时。
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < ghosts.Length; i++)
        {
            lives[i] = 0f;
            if (ghosts[i] != null)
                ghosts[i].gameObject.SetActive(false);
        }
        spawnTimer = 0f;
    }

    /// <summary>
    /// 逐帧衰减残影剩余寿命，寿命耗尽后隐藏对应渲染器。
    /// </summary>
    private void Fade(float deltaTime)
    {
        for (int i = 0; i < ghosts.Length; i++)
        {
            if (lives[i] <= 0f)
                continue;

            lives[i] -= deltaTime;
            SpriteRenderer ghost = ghosts[i];
            if (lives[i] <= 0f)
            {
                if (ghost != null)
                    ghost.gameObject.SetActive(false);
                continue;
            }
            if (ghost == null)
                continue;

            Color color = EliteAffixTuning.AfterimageColor;
            color.a *= lives[i] / EliteAffixTuning.AfterimageLifetime;
            ghost.color = color;
        }
    }

    /// <summary>
    /// 复用池中槽位，复制精英当前帧的贴图、翻转与层级后生成一个残影。
    /// </summary>
    private void Emit(SpriteRenderer source)
    {
        SpriteRenderer ghost = ghosts[cursor];
        if (ghost == null)
        {
            GameObject go = new GameObject("Afterimage");
            go.transform.SetParent(root, false);
            ghost = go.AddComponent<SpriteRenderer>();
            ghosts[cursor] = ghost;
        }

        Transform sourceTransform = source.transform;
        Transform ghostTransform = ghost.transform;
        ghostTransform.position = sourceTransform.position;
        ghostTransform.rotation = sourceTransform.rotation;
        ghostTransform.localScale = sourceTransform.lossyScale;

        ghost.sprite = source.sprite;
        ghost.flipX = source.flipX;
        ghost.flipY = source.flipY;
        ghost.sortingLayerID = source.sortingLayerID;
        ghost.sortingOrder = source.sortingOrder - 1;
        ghost.color = EliteAffixTuning.AfterimageColor;
        ghost.gameObject.SetActive(true);

        lives[cursor] = EliteAffixTuning.AfterimageLifetime;
        cursor = (cursor + 1) % ghosts.Length;
    }
}
