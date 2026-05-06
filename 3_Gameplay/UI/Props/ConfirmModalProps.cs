using System;

/// <summary>
/// 确认弹窗数据：仅快照 + Command，无业务引用。
/// </summary>
public struct ConfirmModalProps
{
    public string Title;
    public string Message;
    public Action OnConfirm;
    public Action OnCancel;
}
