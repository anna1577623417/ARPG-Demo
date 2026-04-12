/// <summary>
/// 固定容量环形意图队列：战斗帧内无 GC（除非扩容以外路径；容量固定不扩容）。
/// </summary>
public sealed class GameplayIntentBuffer
{
    private readonly GameplayIntent[] _items;
    private int _head;
    private int _count;

    public GameplayIntentBuffer(int capacity = 16)
    {
        _items = new GameplayIntent[capacity < 4 ? 4 : capacity];
    }

    public int Count => _count;

    public void Clear()
    {
        _head = 0;
        _count = 0;
    }

    /// <summary>丢弃所有已过期意图。</summary>
    public void FlushExpired(float time)
    {
        while (_count > 0)
        {
            ref var front = ref _items[_head];
            if (front.ExpireTime >= time)
            {
                break;
            }

            Pop();
        }
    }

    public void Enqueue(in GameplayIntent intent)
    {
        if (_count >= _items.Length)
        {
            // 队列满：丢弃最旧的一条，保持“最新输入优先”的街机手感
            Pop();
        }

        var index = (_head + _count) % _items.Length;
        _items[index] = intent;
        _count++;
    }

    public bool TryPeek(out GameplayIntent intent)
    {
        if (_count <= 0)
        {
            intent = default;
            return false;
        }

        intent = _items[_head];
        return true;
    }

    public void Pop()
    {
        if (_count <= 0)
        {
            return;
        }

        _head = (_head + 1) % _items.Length;
        _count--;
    }
}
