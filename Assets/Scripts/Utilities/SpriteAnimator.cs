using System.Collections.Generic;
using UnityEngine;
public struct AnimationSequence
{
    private Sprite[] spriteSequence;
}

public class SpriteAnimator : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private bool animating = false;
    private float frameTime;
    private Sprite[] spriteSequence;
    private Dictionary<string, AnimationSequence> animationsByName;
    private int currSequenceFrame = 0;
    private bool useGlobalTime;
    private float animationTime;
    public SpriteRenderer SpriteRenderer { get => spriteRenderer; }

    /// <summary>
    /// 缓存精灵渲染器。
    /// </summary>
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Init(Sprite[] spriteSequence, float frameTime, bool useGlobalTime = true)
    {
        this.spriteSequence = spriteSequence;
        this.frameTime = frameTime;
        this.useGlobalTime = useGlobalTime;
        Setup();
    }

    /// <summary>
    /// 按序列与帧间隔初始化动画参数。
    /// </summary>
    private void Setup()
    {
        if (spriteSequence == null || spriteSequence.Length == 0 || spriteRenderer == null) return;  // Init 前不设帧
        animationTime = useGlobalTime ? Time.time : 0;
        currSequenceFrame = Mathf.FloorToInt(animationTime / frameTime) % spriteSequence.Length;
        spriteRenderer.sprite = spriteSequence[currSequenceFrame];
    }

    public void StartAnimation(bool reset = false)
    {
        if (reset) Setup();
        animating = true;
    }

    public void StopAnimation(bool reset = false)
    {
        if (reset) Setup();
        animating = false;
    }

    public void StartAnimating(bool reset = false)
    {
        if (reset) Setup();
        animating = true;
    }

    public void StopAnimating(bool reset = false)
    {
        if (reset) Setup();
        animating = false;
    }

    /// <summary>
    /// 按帧间隔推进序列并刷新贴图。
    /// </summary>
    void Update()
    {
        if (animating)
        {
            animationTime = useGlobalTime ? Time.time : (animationTime + Time.deltaTime);
            int sequenceFrame = Mathf.FloorToInt(animationTime / frameTime) % spriteSequence.Length;
            if (sequenceFrame != currSequenceFrame)
            {
                spriteRenderer.sprite = spriteSequence[currSequenceFrame = sequenceFrame];
            }
        }
    }
}
