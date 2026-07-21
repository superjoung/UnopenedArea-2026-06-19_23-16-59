using System;
using UnityEngine;

[Serializable]
public class AnomalyAction
{
    [Header("Target")]
    [SerializeField] private AnomalyActionType actionType = AnomalyActionType.None;
    [SerializeField] private string targetObjectId;
    [SerializeField] private float delaySec;

    [Header("Set Active")]
    [SerializeField] private bool activeValue;

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
    public bool ActiveValue => activeValue;
    public Vector3 TargetLocalPosition => targetLocalPosition;
    public Vector3 LocalPositionOffset => localPositionOffset;
    public Vector3 TargetLocalEulerAngles => targetLocalEulerAngles;
    public Sprite TargetSprite => targetSprite;
    public Color TargetColor => targetColor;
}

