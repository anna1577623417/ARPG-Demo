using UnityEngine;

[RequireComponent(typeof(Animator))]
public class FootIKSystem : MonoBehaviour {
    private Animator anim;

    [Header("IK 设置")]
    [Tooltip("指定哪些层是地面")]
    public LayerMask groundLayer;
    [Range(0, 1)] public float ikWeight = 1f;
    [Tooltip("脚底到地面的微调偏移量")]
    public float footOffset = 0.05f;

    void Start() {
        anim = GetComponent<Animator>();
    }

    // 当 Animator 开启了 IK Pass 后，每一帧会自动调用此方法
    void OnAnimatorIK(int layerIndex) {
        if (anim == null) return;

        // 1. 设置左右脚的 IK 权重 (1表示完全由代码控制脚的位置)
        anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, ikWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, ikWeight);
        anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, ikWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, ikWeight);

        // 2. 分别调整左右脚
        AdjustFootTarget(AvatarIKGoal.LeftFoot);
        AdjustFootTarget(AvatarIKGoal.RightFoot);
    }

    private void AdjustFootTarget(AvatarIKGoal foot) {
        // 获取动画当前帧原本应该在的脚部位置
        Vector3 footPos = anim.GetIKPosition(foot);
        RaycastHit hit;

        // 从脚部上方0.5米处，向下发射一条长度为1米的射线检测地面
        if (Physics.Raycast(footPos + Vector3.up * 0.5f, Vector3.down, out hit, 1f, groundLayer)) {
            // 将脚的位置强行设置在射线击中的地面上，并加上偏移量防止脚面陷入
            Vector3 newFootPos = hit.point;
            newFootPos.y += footOffset;
            anim.SetIKPosition(foot, newFootPos);

            // 【进阶】如果需要脚踝根据地形倾斜（比如站在斜坡上）：
            Quaternion footRotation = Quaternion.LookRotation(transform.forward, hit.normal);
            anim.SetIKRotation(foot, footRotation);
        }
    }

}