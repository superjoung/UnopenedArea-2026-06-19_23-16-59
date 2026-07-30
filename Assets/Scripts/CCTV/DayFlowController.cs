using UnityEngine;

/// <summary>
/// 일차 공통 진행 컨트롤러입니다.
///
/// DayDefinition에 튜토리얼 이상현상을 넣으면 Day 1과 같이
/// 브리핑 → 기준선 확인 → 튜토리얼 → 감시 흐름을 사용합니다.
/// TutorialAnomaly가 비어 있으면 브리핑 → 기준선 확인 → 감시로 바로 진입합니다.
///
/// 기존 Day1FlowController를 상속해 현재 Day 1 씬/UI 참조와 호환됩니다.
/// 새 일차는 이 컴포넌트를 사용하고 해당 일차의 DayDefinition만 연결하면 됩니다.
/// </summary>
public class DayFlowController : Day1FlowController
{
}
