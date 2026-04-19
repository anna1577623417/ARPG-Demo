using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 实体动画管理器基类（基于 Playable API）。
/// </summary>
public abstract class EntityAnimController : MonoBehaviour {
    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private AnimationPlayableOutput _output;

    private readonly AnimationClipPlayable[] _clips = new AnimationClipPlayable[2];
    private int _currentPort;
    private int _previousPort;

    private float _transitionDuration;
    private float _transitionTimer;
    private bool _isTransitioning;

    private Animator _animator;

    public string CurrentClipName { get; private set; } = "";
    public bool IsGraphValid => _graph.IsValid();

    protected virtual void Awake() {
        _animator = GetComponentInChildren<Animator>();
        if (_animator == null) {
            Debug.LogError($"[EntityAnimManager] 找不到 Animator 组件: {name}", this);
            return;
        }

        _graph = PlayableGraph.Create($"{name}_AnimGraph");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        _mixer = AnimationMixerPlayable.Create(_graph, 2);

        _output = AnimationPlayableOutput.Create(_graph, "AnimOutput", _animator);
        _output.SetSourcePlayable(_mixer);

        _currentPort = 0;
        _previousPort = 1;

        _graph.Play();
    }

    protected virtual void OnDestroy() {
        if (_graph.IsValid()) {
            _graph.Destroy();
        }
    }

    protected virtual void Update() {
        // 1. ==== 手动循环处理 (容错机制) ====
        // 如果我们通过代码强行要求循环，但动画资产自身没勾选 Loop Time，我们需要手动 Wrap 时间
        for (int i = 0; i < 2; i++) {
            if (_clips[i].IsValid()) {
                var clip = _clips[i].GetAnimationClip();
                // 在 Play 中，我们将循环动画的 Duration 设为了 MaxValue
                bool isCodeLooping = _clips[i].GetDuration() == double.MaxValue;

                // 【BUG】只有代码要求循环，且底层未开启循环时介入，以SO资产上的勾选为准，这就是SO配置的意义
                if (isCodeLooping && !clip.isLooping) {
                    double time = _clips[i].GetTime();//time是Clip的Local Time
                    double length = clip.length;

                    // 当节点局部时间超过了动画片段长度，手动拨回起点（取模保留超出的小数部分以防丢帧）
                    if (time >= length) {
                        _clips[i].SetTime(time % length);
                        //取模回拨，例如50帧，时间来到50帧刚好播完一次，那么让切片的 时间回到0
                        //最安全有效的办法是让时间对长度取模（循环常用算法）
                    }
                }
            }
        }

        // 2. ==== 过渡(Crossfade)逻辑 ====
        if (_isTransitioning) {
            _transitionTimer += Time.deltaTime;
            float t = Mathf.Clamp01(
                _transitionDuration > 0f ? _transitionTimer / _transitionDuration : 1f);

            // 权重插值：上一个淡出，当前淡入
            _mixer.SetInputWeight(_previousPort, 1f - t);
            _mixer.SetInputWeight(_currentPort, t);

            if (t >= 1f) {
                _isTransitioning = false;

                // 淡出完成，释放上一个 ClipPlayable 的内存
                if (_clips[_previousPort].IsValid()) {
                    _mixer.DisconnectInput(_previousPort);
                    _clips[_previousPort].Destroy();
                }
            }
        }
    }

    /// <summary>
    /// 播放一个 AnimationClip，带 crossfade 过渡。
    /// </summary>
    public void Play(AnimationClip clip, float transitionDuration = 0.2f,
                     float speed = 1f, bool isLooping = true) {
        if (clip == null || !_graph.IsValid()) return;

        _previousPort = _currentPort;
        _currentPort = (_currentPort + 1) % 2;

        if (_clips[_currentPort].IsValid()) {
            _mixer.DisconnectInput(_currentPort);
            _clips[_currentPort].Destroy();
        }

        var clipPlayable = AnimationClipPlayable.Create(_graph, clip);
        clipPlayable.SetSpeed(speed);

        // 【Bug 修复点】：Playable 的 Duration 计算的是 Local Time，已经受到 Speed 缩放的影响。
        // 所以这里绝对不能再除以 Speed，否则会导致单次播放被提前截断！
        clipPlayable.SetDuration(isLooping ? double.MaxValue : clip.length);

        _clips[_currentPort] = clipPlayable;
        _mixer.ConnectInput(_currentPort, clipPlayable, 0);

        if (transitionDuration <= 0f || !_clips[_previousPort].IsValid()) {
            _mixer.SetInputWeight(_currentPort, 1f);
            _mixer.SetInputWeight(_previousPort, 0f);
            _isTransitioning = false;

            if (_clips[_previousPort].IsValid()) {
                _mixer.DisconnectInput(_previousPort);
                _clips[_previousPort].Destroy();
            }
        } else {
            _mixer.SetInputWeight(_currentPort, 0f);
            _mixer.SetInputWeight(_previousPort, 1f);
            _transitionDuration = transitionDuration;
            _transitionTimer = 0f;
            _isTransitioning = true;
        }

        CurrentClipName = clip.name;
    }

    /// <summary>调整当前主输出 Clip 的播放倍率（步幅匹配等）。</summary>
    protected void SetPrimaryClipPlayableSpeed(float speed)
    {
        if (!_graph.IsValid()) return;
        if (_clips[_currentPort].IsValid())
        {
            _clips[_currentPort].SetSpeed(Mathf.Max(0.01f, speed));
        }
    }

    public void Stop() {
        if (!_graph.IsValid()) return;

        for (int i = 0; i < 2; i++) {
            if (_clips[i].IsValid()) {
                _mixer.DisconnectInput(i);
                _clips[i].Destroy();
            }
            _mixer.SetInputWeight(i, 0f);
        }

        _isTransitioning = false;
        CurrentClipName = "";
    }
}