/// 改编自以下 Unity 论坛帖子：
/// https://answers.unity.com/questions/1040319/whats-the-proper-way-to-queue-and-space-function-c.html

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class CoroutineQueue
{
    private MonoBehaviour owner = null;
    private Coroutine internalCoroutine = null;
    private Queue<IEnumerator> coroutineQueue = new Queue<IEnumerator>();

    /// <summary>
    /// 记录协程宿主。
    /// </summary>
    public CoroutineQueue(MonoBehaviour coroutineOwner)
    {
        owner = coroutineOwner;
    }

    /// <summary>
    /// 启动队列处理协程。
    /// </summary>
    public void StartLoop()
    {
        internalCoroutine = owner.StartCoroutine(Process());
    }
    /// <summary>
    /// 停止队列处理协程。
    /// </summary>
    public void StopLoop()
    {
        owner.StopCoroutine(internalCoroutine);
        internalCoroutine = null;
    }

    /// <summary>
    /// 把协程加入队列，按入队顺序依次执行。
    /// </summary>
    public void EnqueueCoroutine(IEnumerator coroutine)
    {
        coroutineQueue.Enqueue(coroutine);
    }

    /// <summary>
    /// 依次取出并执行队列中的协程。
    /// </summary>
    private IEnumerator Process()
    {
        while (true)
        {
            if (coroutineQueue.Count > 0)
                yield return owner.StartCoroutine(coroutineQueue.Dequeue());
            else
                yield return null;
        }
    }
}
