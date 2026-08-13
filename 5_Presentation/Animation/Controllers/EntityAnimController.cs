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
    private AnimationClip _currentClipAsset;
    private string _lastPlaySource = "-";

    private Animator _animator;

    public string CurrentClipName { get; private set; } = "";
    public bool IsGraphValid => _graph.IsValid();

    /// <summary>227.5.1 探针用实例 Id（Player 侧可覆盖为 Entity.GetInstanceID）。</summary>
    protected virtual int GetAnimProbeInstanceId() => GetInstanceID();

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

            AnimTransition227Probe.SampleMixerIfNeeded(
                GetAnimProbeInstanceId(),
                t,
                _previousPort,
                _currentPort,
                _mixer.GetInputWeight(_previousPort),
                _mixer.GetInputWeight(_currentPort),
                ReadLocalTime(_previousPort),
                ReadLocalTime(_currentPort));

            if (t >= 1f) {
                _isTransitioning = false;

                AnimTransition227Probe.EndMixerTransition(
                    GetAnimProbeInstanceId(),
                    _previousPort,
                    _currentPort,
                    _mixer.GetInputWeight(_previousPort),
                    _mixer.GetInputWeight(_currentPort),
                    ReadLocalTime(_previousPort),
                    ReadLocalTime(_currentPort));

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
                     float speed = 1f, bool isLooping = true, float normalizedStart = 0f,
                     bool restartIfSameClip = true, string requestSource = "unspecified") {
        if (clip == null || !_graph.IsValid()) return;

        // 227.5.1.1：连续表现与 Jump 双入口可以请求同一 Clip。对明确声明幂等的入口，
        // 保留当前 Playable 的局部时间和 Mixer 权重，禁止重新从 0 建图导致卡壳。
        if (ShouldSuppressSameClipReplay(
                _currentClipAsset,
                clip,
                _clips[_currentPort].IsValid(),
                restartIfSameClip))
        {
            var currentDuration = _clips[_currentPort].GetDuration();
            var currentCodeLooping = currentDuration == double.MaxValue;
            var loopUpgraded = ShouldUpgradeLoopContract(currentCodeLooping, isLooping);
            if (loopUpgraded)
            {
                // 连续语义比 FBX 的 Loop Time 更权威；原地升级，不重建 Playable/不重置局部时间。
                _clips[_currentPort].SetDuration(double.MaxValue);
            }

            var currentSpeed = _clips[_currentPort].GetSpeed();
            var speedUpdated = System.Math.Abs(currentSpeed - speed) > 0.0001d;
            if (speedUpdated)
            {
                _clips[_currentPort].SetSpeed(speed);
            }

            AnimTransition227BugProbe.LogSameClipSuppressed(
                GetAnimProbeInstanceId(),
                clip,
                _lastPlaySource,
                requestSource,
                _isTransitioning,
                ReadLocalTime(_currentPort),
                requestedLoop: isLooping,
                effectiveLoop: currentCodeLooping || loopUpgraded,
                loopUpgraded: loopUpgraded,
                speedUpdated: speedUpdated);
            return;
        }

        var supersede = _isTransitioning;
        var fromClip = _currentClipAsset;
        var prevPortBefore = _currentPort;
        var prevWeightBefore = _clips[_currentPort].IsValid() ? _mixer.GetInputWeight(_currentPort) : 0f;
        var prevTimeBefore = ReadLocalTime(_currentPort);

        if (supersede)
        {
            AnimTransition227BugProbe.LogSupersede(
                GetAnimProbeInstanceId(),
                fromClip,
                clip,
                _lastPlaySource,
                requestSource);
        }

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

        var startT = Mathf.Clamp01(normalizedStart);
        if (startT > 0.0001f)
        {
            clipPlayable.SetTime(startT * clip.length);
        }

        _clips[_currentPort] = clipPlayable;
        _mixer.ConnectInput(_currentPort, clipPlayable, 0);
        _currentClipAsset = clip;
        _lastPlaySource = requestSource;

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

        AnimTransition227Probe.BeginMixerTransition(
            GetAnimProbeInstanceId(),
            supersede ? $"{requestSource}:playDuringBlend" : requestSource,
            fromClip,
            clip,
            transitionDuration,
            speed,
            isLooping,
            prevPortBefore,
            _currentPort,
            prevWeightBefore,
            _mixer.GetInputWeight(_currentPort),
            prevTimeBefore,
            ReadLocalTime(_currentPort),
            supersede);
    }

    /// <summary>227.5.1.1 — 可独立测试的同 Clip 重播门禁；默认调用仍保持允许重播。</summary>
    public static bool ShouldSuppressSameClipReplay(
        AnimationClip currentClip,
        AnimationClip requestedClip,
        bool currentPlayableValid,
        bool restartIfSameClip)
    {
        return !restartIfSameClip
               && currentPlayableValid
               && currentClip != null
               && currentClip == requestedClip;
    }

    /// <summary>227.5.1.2 — 幂等请求可把有限时长 Playable 原地升级为连续循环，但不反向降级。</summary>
    public static bool ShouldUpgradeLoopContract(bool currentCodeLooping, bool requestedLoop)
    {
        return requestedLoop && !currentCodeLooping;
    }

    /// <summary>182.6 — Playable 主 Clip 进度 → Action 归一化时间（与 MapActionTimeToClipNormalized 互逆）。</summary>
    public bool TryGetPrimaryClipActionNormalizedTime(ActionDataSO action, out float actionNormalizedTime)
    {
        actionNormalizedTime = -1f;
        if (action?.MainClip == null || !_graph.IsValid() || !_clips[_currentPort].IsValid())
        {
            return false;
        }

        var clip = action.MainClip;
        var clipNorm = clip.length > 0.0001f
            ? Mathf.Clamp01((float)(_clips[_currentPort].GetTime() / clip.length))
            : 0f;
        var segStart = ActionTimeAuthority.ResolveSegmentStart(action);
        var segEnd = ActionTimeAuthority.ResolveSegmentEnd(action);
        var segLen = segEnd - segStart;
        if (segLen < 0.001f)
        {
            return false;
        }

        actionNormalizedTime = Mathf.Clamp01((clipNorm - segStart) / segLen);
        return true;
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

    /// <summary>164.1 L6：Per-Action Clip RootMotion 开关（与 MotionProfile 程序化位移二选一）。</summary>
    public void SetClipRootMotionEnabled(bool enabled)
    {
        if (_animator != null)
        {
            _animator.applyRootMotion = enabled;
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
        _currentClipAsset = null;
        _lastPlaySource = "-";
    }

    double ReadLocalTime(int port)
    {
        return _clips[port].IsValid() ? _clips[port].GetTime() : 0d;
    }
}
