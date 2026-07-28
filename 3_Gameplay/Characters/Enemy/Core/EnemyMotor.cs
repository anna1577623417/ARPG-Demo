using UnityEngine;

/// <summary>
/// 220.5 B4：Enemy 正式 Motor V1。
/// <para>当前只实现水平速度与水平冲量；不再由 Enemy / Training 脚本直接改 Transform。</para>
/// <para>垂直 Launch、重力、接地和 KCC 碰撞求解留给后续 Motor Landing，避免用假地面规则污染当前验收。</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyMotor : MonoBehaviour, IEntityMotor, IImpulseReceiver
{
    [Header("Impulse")]
    [SerializeField, Min(0f)] float planarDamping = 8f;

    [Header("Debug")]
    [SerializeField] bool logMotorFrames;

    Enemy _enemy;
    Vector3 _planarVelocity;
    float _verticalSpeed;

    public Vector3 PlanarVelocity => _planarVelocity;
    public float VerticalSpeed => _verticalSpeed;
    public bool IsGrounded => true;

    void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    void FixedUpdate()
    {
        ApplyMotor(MotorSolveContext.Locomotion);
    }

    public void SetPlanarVelocity(Vector3 planarVelocity)
    {
        _planarVelocity = new Vector3(planarVelocity.x, 0f, planarVelocity.z);
    }

    public void SetVerticalSpeed(float verticalSpeed)
    {
        _verticalSpeed = verticalSpeed;
    }

    public void ApplyMotor(in MotorSolveContext context)
    {
        if (_enemy != null && _enemy.IsDead)
        {
            _planarVelocity = Vector3.zero;
            _verticalSpeed = 0f;
            return;
        }

        var delta = (_planarVelocity + Vector3.up * _verticalSpeed) * Time.fixedDeltaTime;
        if (delta.sqrMagnitude > 0.0000001f)
        {
            transform.position += delta;
        }

        _planarVelocity = Vector3.MoveTowards(
            _planarVelocity,
            Vector3.zero,
            Mathf.Max(0f, planarDamping) * Time.fixedDeltaTime);

        if (logMotorFrames && _planarVelocity.sqrMagnitude > 0.0001f)
        {
            Debug.Log(
                $"[EnemyMotor] Apply planar={_planarVelocity} pos={transform.position} " +
                $"grounded={IsGrounded} policy={context.GroundingPolicy}",
                this);
        }
    }

    public ImpulseApplyResult TryApplyImpulse(in ImpulseRequest request)
    {
        if (_enemy != null && _enemy.IsDead)
        {
            return ImpulseApplyResult.RejectedDead;
        }

        var planarDirection = request.Direction;
        planarDirection.y = 0f;
        if (request.Force > 0.01f && planarDirection.sqrMagnitude > 0.0001f)
        {
            SetPlanarVelocity(planarDirection.normalized * request.Force);
            if (EnemyRuntimeDiag.IsEnabled)
            {
                Debug.Log(
                    $"[EnemyMotor] channel=Impulse result=Applied force={request.Force:F1} " +
                    $"velocity={_planarVelocity} kind={request.Kind}",
                    this);
            }

            return ImpulseApplyResult.Applied;
        }

        if (request.LaunchUpSpeed > 0.01f)
        {
            if (EnemyRuntimeDiag.IsEnabled)
            {
                Debug.Log(
                    $"[EnemyMotor] channel=Impulse result=IgnoredByProfile launch={request.LaunchUpSpeed:F1} " +
                    "reason=vertical-motor-open",
                    this);
            }

            return ImpulseApplyResult.IgnoredByProfile;
        }

        return ImpulseApplyResult.IgnoredByProfile;
    }
}
