using System;
using UnityEngine;

[Serializable]
public class AnomalyAction
{
    [Header("Target")]
    [SerializeField] private AnomalyActionType actionType = AnomalyActionType.None;
    [SerializeField] private string targetObjectId;
    [SerializeField] private float delaySec;

    [Header("Presentation")]
    [Tooltip("Play Presentation일 때 실행할 프레젠테이션 ID입니다. 같은 오브젝트에 여러 연출을 둘 수 있습니다.")]
    [SerializeField] private string presentationId;

    [Header("Set Active")]
    [SerializeField] private bool activeValue;

    [Header("Animator")]
    [Tooltip("Set Animator Enabled 액션에서 사용할 Animator 활성 상태입니다.")]
    [SerializeField] private bool animatorEnabledValue = true;

    [Header("Move")]
    [SerializeField] private Vector3 targetLocalPosition;
    [SerializeField] private Vector3 localPositionOffset;

    [Header("Rotation")]
    [SerializeField] private Vector3 targetLocalEulerAngles;

    [Header("Sprite")]
    [SerializeField] private Sprite targetSprite;

    [Header("Color")]
    [SerializeField] private Color targetColor = Color.white;

    public AnomalyActionType ActionType => actionType;
    public string TargetObjectId => targetObjectId;
    public float DelaySec => Mathf.Max(0f, delaySec);
    public string PresentationId => presentationId;
    public bool ActiveValue => activeValue;
    public bool AnimatorEnabledValue => animatorEnabledValue;
    public Vector3 TargetLocalPosition => targetLocalPosition;
    public Vector3 LocalPositionOffset => localPositionOffset;
    public Vector3 TargetLocalEulerAngles => targetLocalEulerAngles;
    public Sprite TargetSprite => targetSprite;
    public Color TargetColor => targetColor;
}

