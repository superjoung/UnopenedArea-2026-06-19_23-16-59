using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DayDefinition", menuName = "Unrecorded Area/Day Definition")]
public class DayDefinition : ScriptableObject
{
    [Header("Day")]
    [SerializeField] private int day = 1;
    [SerializeField] private float durationSec = 600f;
    [SerializeField] private int maxMissed = 3;

    [Header("CCTV Areas")]
    [SerializeField] private CCTVAreaDefinition[] activeAreas;

    public int Day => day;
    public float DurationSec => durationSec;
    public int MaxMissed => maxMissed;
    public IReadOnlyList<CCTVAreaDefinition> ActiveAreas => activeAreas;
}

