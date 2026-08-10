# Day 3 개발 인수인계서 - 실시간

> 대상: Day 3 시스템/콘텐츠 구현 담당자
>
> 목적: 기존 Day 1/Day 2 공통 루프를 유지한 채 Day 3의 **시간 불일치·서버실·CAM-00** 콘텐츠를 안전하게 추가한다.
>
> 이 문서는 구현 지시서이자, AI 코딩 도구에 그대로 전달할 수 있는 작업 프롬프트를 포함한다.

---

## 0. 시작 전에 반드시 읽을 것

### 기준 문서 우선순위

서로 다른 문서가 충돌할 수 있다. 판단 우선순위는 반드시 아래 순서다.

1. `Assets/Documents/이상현상 콘텐츠 기획서_v1.0.0 (1).xlsx`
   - 최신 콘텐츠 ID, 보고 정답, 타겟 ID, Action, 랜덤 풀, 애니메이션 여부의 기준
2. `Assets/Documents/미기록_구역_완성형_PRD_Codex용.md`
   - 공통 게임 규칙, 보고/미보고/현장 모드 분리 규칙의 기준
3. `Assets/Documents/미기록_구역_시나리오_바이블.docx`
   - Day 3의 서사 목적, 필수 장면, 플래그, 감정 흐름의 기준
4. `Assets/Documents/현장 이동 상호작용 이벤트 설계서_v1.0.0.pdf`
   - 현장 상호작용, 입력 잠금, 현장 공포 연출의 기준
5. `Assets/Documents/미기록_구역_이상현상_기획목록_선행조건정리.xlsm`, 초기 CSV, 구 시스템 문서
   - 초기안/보조 자료. 최신 XLSX와 충돌하면 최신 XLSX를 따른다.

### 절대 지켜야 할 공통 규칙

- 보고 정답은 항상 `[AreaId] + [ReportTargetId] + [AnomalyReportType]`으로 판정한다.
- 일반 보고형 이상현상은 정확 보고 시 성공 노이즈 후 기준 배치로 복원한다.
- 미보고 공포 이벤트(`D3_NRxx`, `D3_FAxx`)는 보고 정답도 아니고 오보고/미보고 카운트에도 영향을 주지 않는다.
- 현장 모드 중에는 일반 이상현상의 생성·타이머·미보고 처리를 멈춘다.
- 판정 불가능한 화면을 만들어 플레이어에게 오답을 강요하지 않는다.
- 기존 Day 1/Day 2 프리팹, 공용 스크립트, 씬 참조를 무단으로 초기화하거나 삭제하지 않는다.

---

## 1. Day 3의 서사 목표

Day 3의 장 제목은 **실시간**이다. 플레이어가 CCTV가 현재를 보여 준다는 전제를 의심하게 만드는 날이다.

핵심 질문은 다음이다.

> CCTV는 현재를 보여주는가?

플레이어가 경험해야 하는 흐름:

1. Day 1에서는 물건을, Day 2에서는 사람을 "정상화"했다.
2. Day 3에서는 영상의 시간·순서·공간이 현실과 다르게 보이기 시작한다.
3. 서버실 현장 사건을 통해 CCTV가 단순 카메라가 아니라 기준선을 교정하는 장치임을 암시한다.
4. `CAM-00`에서 실제 플레이어보다 약 12초 앞선 장면을 보여준다.
5. 귀환 전/후 제어실에는 이미 누군가 앉아 있는 듯한 장면을 보여 주되, 이 장면은 **보고 대상이 아니다**.

반복 모티프:

- 12초
- 02:17
- 빈 의자
- 서버/반복 영상
- O-06(윤서진)
- 정상화한 장면이 다시 돌아오는 불신

---

## 2. 현재 프로젝트 구조 - 건드리지 말고 재사용할 것

### 주요 씬/에셋

- `Assets/Scenes/Day3.unity`: Day 3 작업 씬
- `Assets/Datas/DayDefinitions/Day3Definition.asset`: Day 3 스케줄/랜덤 풀
- `Assets/Resources/Prefabs/CommonRoot.prefab`: 공용 GameManager, UI, 전환, 메인룸 흐름
- `Assets/Resources/Prefabs/AreaPrefabs/`: CCTV 관찰 구역 프리팹
- `Assets/Datas/AnomalyDefinitions/D3/`: Day 3 `AnomalyDefinition` 에셋 위치

### 이미 있는 공용 흐름

```text
Title/전화
  -> StoryUI 대사
  -> 메인룸 CCTV 클릭
  -> CCTV 감시/보고
  -> 긴급 이벤트
  -> 메인룸 문
  -> FieldMode
  -> 목표 완료
  -> 메인룸
  -> CCTV 감시 재개
```

### 재사용해야 하는 핵심 코드

- `Day1FlowController` / `DayFlowController`
  - 이름은 Day1이지만 DayDefinition 기반 공용 흐름이다.
  - 긴급 이벤트 상태: `EmergencyDispatch` -> `EmergencyRecovery`.
- `DayRuntimeController`
  - 일차 시간, 정답 수, 오보고/미보고 카운트, 종료를 관리한다.
- `AnomalyScheduler`, `AnomalyService`, `CCTVAreaInstance`
  - 이상현상 선택, Action 적용, baseline 복원을 담당한다.
- `Day1AreaTransitionController`, `TransitionEffect`
  - 메인룸/CCTV/FieldMode 전환과 암전/페이드 연출을 담당한다.
- `FieldModeController`
  - 현장 진입 시 CCTV를 끄고 플레이어/현장 카메라를 활성화한다.
- `FieldStoryRecordInspect`, `FieldStoryRecordPopup`
  - Day 2 기록물에 사용 중. Day 3 문서/서버 로그 확대 확인에도 재사용 가능하다.
- `StoryFlagStore`
  - 문서 확인/필수 장면 완료 등의 플래그 저장에 사용한다.

### Day 3 씬 작업 원칙

- Day 2를 복제해서 구성하는 경우 `FieldModeController`의 아래 네 참조가 Day3 로컬 오브젝트로 지정돼 있는지 반드시 확인한다.
  - `Field Mode Root`
  - `Field Camera`
  - `Field Player`
  - `Field Camera Follow Controller`
- `CommonRoot`는 공용 프리팹이므로 Day3에 필요한 차이는 **씬 오버라이드 또는 Day3 전용 콘텐츠 루트**에 둔다.
- `FieldModeRoot`는 시작 시 꺼져 있어야 한다.

---

## 3. Day 3 일정/진입 조건

최신 XLSX의 `05_Day_Schedule` 기준:

| 항목 | 값 |
|---|---:|
| 일차 | 3 |
| 근무 시간 | 600초 |
| 최대 미보고 | 3 |
| 활성 CCTV 구역 | 생활관 복도(100), 치료실(200), 설비실(300), 서버실(400) |
| 랜덤 간격 | 35~70초 |
| 일반 이상현상 | D3_A01~D3_A11 |
| CCTV 미보고 공포 | D3_NR01~D3_NR03 |
| 현장 공포 | D3_FA01~D3_FA03 |
| 필수 현장 이벤트 조건 | 진행률 65% 이상 |

### 구현 전 확인이 필요한 문서 충돌

최신 표와 시나리오 바이블이 `D3_F01` 명칭을 다르게 사용한다.

- 최신 XLSX: `D3_F01 = 시야 끝 이전 관측자 돌진` (서버실, 필수 현장 공포 연출)
- 시나리오 바이블: Day 3 필수 현장 사건 = **서버 재부팅**, 이후 `CAM-00`과 귀환 연출

따라서 현 시점 권장 구현은 다음처럼 분리한다.

```text
Day3 Emergency Dispatch (진입 조건: 진행률 65%)
  -> 서버실 FieldMode
  -> 서버 재부팅 상호작용(실제 완료 목표)
  -> 시야를 복도로 돌렸을 때 D3_F01 관측자 돌진 1회
  -> CAM-00/귀환 연출
```

`D3_F01` 자체를 서버 재부팅 완료 조건으로 바꾸거나, 보고형 이상현상으로 만들면 안 된다.

---

## 4. Day 3 보고형 이상현상 - 최신 XLSX 기준

모두 `active_sec = 20`, 랜덤 풀, 반복 허용이다. 아래 ID/정답/타겟 ID를 바꾸지 않는다.

| ID | 구역 | 보고 대상 | 보고 타입 | Target Object ID | Action |
|---|---|---|---|---|---|
| D3_A01 | 치료실 | 벽시계 | 비정상 행동 | `OBJ_TREAT_CLOCK_02` | `ChangeSprite`, `Clock_Reverse`, 애니메이션 |
| D3_A02 | 치료실 | 인물 | 비정상 행동 | `OBJ_TREAT_PERSON_03` | `ChangeSprite`, `Gesture_Repeat`, 애니메이션 |
| D3_A03 | 생활관 복도 | 문 | 비정상 행동 | `OBJ_DORM_DOOR_03` | `ChangeSprite`, `Door_Loop`, 애니메이션 |
| D3_A04 | 생활관 복도 | 휠체어 | 비정상 행동 | `OBJ_DORM_WHEELCHAIR_02` | 두 지점 왕복 연출 |
| D3_A05 | 설비실 | 모니터 | 형태 변화 | `OBJ_UTILITY_MONITOR_01` | `ChangeSprite`, `Monitor_Pulse`, 애니메이션 |
| D3_A06 | 서버실 | 서버 랙 | 비정상 행동 | `OBJ_SERVER_RACK_01` | `ChangeSprite`, `ServerLight_Reverse`, 애니메이션 |
| D3_A07 | 생활관 복도 | 벽시계 | 형태 변화 | `OBJ_DORM_CLOCK_PAIR_01` | `ChangeSprite`, `Clock_Mismatch` |
| D3_A08 | 서버실 | 환기구 | 비정상 행동 | `OBJ_SERVER_VENT_01` | `ChangeSprite`, `Vent_Shake`, 애니메이션 |
| D3_A09 | 설비실 | 공구함 | 위치 변화 | `OBJ_UTILITY_TOOLCART_01` | `MoveToLocalPosition`, 교환 좌표 입력 |
| D3_A10 | 치료실 | 조명 | 비정상 행동 | `OBJ_TREAT_LIGHT_ROW_01` | `ChangeSprite`, `Light_Wave`, 애니메이션 |
| D3_A11 | 생활관 복도 | 의자 | 위치 변화 | `OBJ_DORM_CHAIR_03` | `MoveToLocalPosition`, 복도 중앙 좌표 |

### 콘텐츠 제작 순서

각 항목마다 아래 순서를 지킨다.

1. 해당 CCTV 구역 프리팹 `ObjRoot` 아래에 정상 오브젝트를 둔다.
2. 해당 오브젝트에 정확한 `Target Object Id`를 부여한다.
3. baseline 상태에서 오브젝트/스프라이트/좌표가 정상인지 확인한다.
4. `AnomalyDefinition`에 Area, Target, Report Type, Action을 입력한다.
5. Action으로 변화가 나타나는지 확인한다.
6. 정답 제출 시 baseline으로 돌아오는지 확인한다.
7. 미보고 시에도 다음 후보가 깨지지 않도록 baseline 복원이 되는지 확인한다.

---

## 5. 보고 대상이 아닌 CCTV 공포 이벤트

아래는 보고 패널 선택지에 넣지 않는다.

| ID | 내용 | 구역 | 연출 | 사운드 |
|---|---|---|---|---|
| D3_NR01 | 서버 화면 얼굴 노이즈 | 서버실 | 노이즈와 함께 0.5초 얼굴 노출 | `SFX_D3_DIGITAL_HIT` |
| D3_NR02 | 생활관 천장 인물 스침 | 생활관 복도 | 화면 상단을 가로지르는 침입 | `SFX_D3_CEILING_STEP` |
| D3_NR03 | 치료실 유리 반사 인물 | 치료실 | 반사면에 잠깐 나타났다가 사라짐 | `SFX_D3_GLASS` |

규칙:

- `report_required = false`
- 오보고/미보고 카운트 변경 금지
- 중요한 보고형 오브젝트를 가릴 정도의 강한 노이즈 금지
- 같은 구역에 보고형 이상현상이 있을 때는 과도한 동시 실행 금지

---

## 6. 현장 이벤트와 FieldMode 공포

### 서버 재부팅 - 실제 필수 목표

권장 구성:

1. Day3Definition의 긴급 이벤트를 서버실 현장 진입으로 연결한다.
2. 서버 제어반에 `HoldProgress` 또는 `KeySequence` 상호작용을 둔다.
3. 완료 시 서버 재부팅 플래그를 저장한다. 권장 플래그: `SERVER_REBOOTED`.
4. 일반 이상현상 스케줄은 현장 진입부터 복귀까지 멈춘다.
5. 서버 재부팅 후 바로 복도 쪽 시야를 볼 때 D3_F01을 한 번 실행한다.
6. 이후 CAM-00 및 귀환 연출을 진행한다.

### 현장 공포 이벤트

| ID | 구역 | 방식 | 비고 |
|---|---|---|---|
| D3_FA01 | 서버실 | 화면 얼굴 노이즈 | D3_NR01 리소스 재사용, 보고 불가 |
| D3_FA02 | 서버실 | 고정 위치/시야 진입 환기구 진동 | D3_A08 리소스 재사용 |
| D3_FA03 | 생활관 복도 | 천장 인물 viewport 침입 | D3_NR02 리소스 재사용 |
| D3_F01 | 서버실 | 시야 끝 이전 관측자 돌진 | 필수 1회, 25도 시야 조건, 거리 8, 0.2초 돌진 |

### D3_F01 구현 규칙

- 서버 재부팅 전후 중, 기획 확정 시점에 단 한 번만 실행한다.
- 플레이어가 팝업/상호작용 중일 때 실행하지 않는다.
- 시야를 복도 쪽으로 돌린 뒤에만 판정한다.
- 마지막 프레임에서 O-06 표식을 짧게 보여 줄 수 있다.
- 플레이어 피해, 즉사, 추격 AI로 확장하지 않는다.

---

## 7. CAM-00 및 귀환 연출

시나리오 바이블의 핵심 요구:

- 서버실 복구 후 CAM-00은 플레이어가 실제로 들어가기 약 12초 전의 장면을 보여준다.
- 화면 속 플레이어가 고개를 돌리지만 실제 플레이어는 아직 돌리지 않은 상태여야 한다.
- 귀환 직전/후 제어실 CCTV에는 누군가 이미 의자에 앉아 있는 장면을 잠깐 보여 준다.
- 이 장면은 보고 대상이 아니며, 보고 패널을 열 수 없게 해야 한다.

권장 구현 단계:

1. `CAM-00`을 Day3 전용 CCTV 채널로 추가한다.
2. 실제 플레이어를 그대로 복제하기보다, 미리 만든 12초 길이의 연출 오브젝트/애니메이션으로 구현한다.
3. CAM-00 시청 완료 시 `CAM00_VIEWED` 플래그를 저장한다.
4. 귀환 직전에는 `CTRL_ChairOccupied` 같은 연출 오브젝트를 켠다.
5. 귀환 장면 동안 보고 UI를 잠그고, 관찰이 끝나면 정상 감시 루프로 돌린다.

권장 플래그:

- `SERVER_REBOOTED`
- `CAM00_VIEWED`
- `LOG_O06_02`
- `SERVER_PREDICTION_DOCUMENT`
- `STORY_DAY3_COMPLETE`

---

## 8. 구현 우선순위

### P0 - 먼저 끝낼 것

1. Day3 씬/Day3Definition이 Day 3 데이터와 네 개 구역을 참조하는지 확인
2. 서버실 CCTV 구역 프리팹과 `OBJ_SERVER_RACK_01`, `OBJ_SERVER_VENT_01` 배치
3. D3_A01~A11의 Target Object ID와 baseline 확보
4. D3_A01~A11이 정답/복원까지 작동하는지 확인
5. Day3 긴급 진입 -> 메인룸 -> 문 -> FieldMode 전환 확인
6. 서버 재부팅 상호작용과 `SERVER_REBOOTED` 완료 처리

### P1 - 서사/공포 완성

1. D3_NR01~NR03 미보고 공포 이벤트
2. D3_FA01~FA03 현장 공포 이벤트
3. D3_F01 관측자 돌진
4. CAM-00 12초 선행 영상
5. 귀환 전후 제어실 의자 점유 연출과 보고 UI 잠금

### P2 - 마감 전 QA

1. Day1/Day2 씬 회귀 테스트
2. 정답/오답/미보고/정전/현장 복귀 후 상태 복원 확인
3. `F7` 디버그 긴급 이벤트 확인
4. Day3 완료 -> Day4 미구현 상태에서의 임시 처리 결정

---

## 9. QA 체크리스트

- [ ] Day3은 4개 CCTV 구역을 Q/E로 정상 전환한다.
- [ ] 각 D3_A01~A11은 정확한 보고 조합을 가진다.
- [ ] 정답 시 성공 노이즈가 한 번만 나오고 정상 배치가 복원된다.
- [ ] 오답은 성공 처리되지 않는다.
- [ ] 미보고 공포 이벤트가 실패 카운트를 올리지 않는다.
- [ ] 현장 모드 중 랜덤 이상현상 타이머가 진행되지 않는다.
- [ ] 서버실 상호작용 중 이동/카메라/보고 UI가 중복 동작하지 않는다.
- [ ] D3_F01은 한 번만 실행되며, 팝업 위에 겹쳐 뜨지 않는다.
- [ ] CAM-00과 제어실 의자 연출은 보고 대상이 아니다.
- [ ] Day1/Day2의 FieldMode 진입 참조가 깨지지 않는다.

---

## 10. 담당자에게 전달할 구현 프롬프트

아래 블록을 그대로 AI 코딩 도구 또는 담당 개발자에게 전달한다.

```text
Unity 프로젝트 《미기록 구역》의 Day3 구현을 맡아줘.

먼저 Assets/Documents 안의 최신 `이상현상 콘텐츠 기획서_v1.0.0 (1).xlsx`, PRD, 시나리오 바이블, 현장 이동 상호작용 이벤트 설계서를 읽고 시작해. 최신 XLSX가 이상현상 ID/보고 타입/타겟/액션의 최우선 기준이다.

절대 규칙:
1) 일반 보고형 이상현상은 [구역 + 대상 + 유형]으로 판정하고, 정답이면 성공 노이즈 후 baseline으로 복원한다.
2) 미보고 공포 이벤트는 보고 대상도 아니고 오보고/미보고 카운트에 영향이 없다.
3) 현장 모드 중 일반 이상현상 타이머와 생성은 멈춘다.
4) 기존 Day1/Day2 공용 코드/프리팹/씬을 초기화하거나 삭제하지 말고, 변경 전 현재 상태를 먼저 확인한다.
5) 작업 중 새 오브젝트와 ID, Inspector 할당 방법을 마지막에 반드시 정리한다.

현재 공용 구조:
- Day1FlowController/DayFlowController는 이름과 달리 DayDefinition 기반 공용 흐름이다.
- Day1AreaTransitionController/TransitionEffect가 CCTV, 메인룸, FieldMode 전환을 담당한다.
- AnomalyScheduler/AnomalyService/CCTVAreaInstance가 이상현상 실행과 baseline 복원을 담당한다.
- Day3.unity, Day3Definition.asset, D3 AnomalyDefinition 에셋을 우선 사용한다.

Day3 P0 목표:
1) Day3에서 생활관 복도(100), 치료실(200), 설비실(300), 서버실(400) 4개 CCTV 구역이 작동하게 한다.
2) D3_A01~D3_A11을 최신 XLSX의 Area/Target/ReportType/Target Object ID/Action대로 씬과 SO에 배치한다.
3) Day3 긴급 이벤트는 진행률 65% 이후 서버실 FieldMode로 나가는 흐름으로 만든다.
4) 서버 제어반을 HoldProgress 또는 KeySequence로 완료하면 SERVER_REBOOTED 플래그가 저장되게 한다.
5) 현장 중 일반 이상현상 스케줄은 멈춰야 한다.

Day3 콘텐츠 ID:
- D3_A01 치료실 벽시계/비정상 행동/OBJ_TREAT_CLOCK_02/Clock_Reverse
- D3_A02 치료실 인물/비정상 행동/OBJ_TREAT_PERSON_03/Gesture_Repeat
- D3_A03 생활관 문/비정상 행동/OBJ_DORM_DOOR_03/Door_Loop
- D3_A04 생활관 휠체어/비정상 행동/OBJ_DORM_WHEELCHAIR_02/왕복
- D3_A05 설비실 모니터/형태 변화/OBJ_UTILITY_MONITOR_01/Monitor_Pulse
- D3_A06 서버실 서버 랙/비정상 행동/OBJ_SERVER_RACK_01/ServerLight_Reverse
- D3_A07 생활관 벽시계/형태 변화/OBJ_DORM_CLOCK_PAIR_01/Clock_Mismatch
- D3_A08 서버실 환기구/비정상 행동/OBJ_SERVER_VENT_01/Vent_Shake
- D3_A09 설비실 공구함/위치 변화/OBJ_UTILITY_TOOLCART_01/좌표 교환
- D3_A10 치료실 조명/비정상 행동/OBJ_TREAT_LIGHT_ROW_01/Light_Wave
- D3_A11 생활관 의자/위치 변화/OBJ_DORM_CHAIR_03/복도 중앙 좌표

P1 서사 목표:
- 서버 재부팅 후 CAM-00에서 실제보다 약 12초 앞선 플레이어 영상을 보인다.
- 귀환 전후 제어실 의자 점유 연출은 보고 대상이 아니며 보고 UI를 잠근다.
- D3_NR01~03, D3_FA01~03, D3_F01(서버실에서 시야 끝 관측자 0.2초 돌진)은 보고/실패 카운트와 분리한다.

중요한 문서 충돌:
- 최신 XLSX의 D3_F01은 '시야 끝 이전 관측자 돌진'이다.
- 시나리오 바이블의 Day3 필수 현장 사건은 '서버 재부팅'이다.
- 따라서 서버 재부팅은 실제 완료 목표, D3_F01은 그 전후에 1회 나오는 공포 연출로 분리해라.

작업 순서:
- 먼저 현재 Day3 씬/Day3Definition/프리팹 참조를 읽고, 누락 항목만 수정한다.
- 코드 수정은 최소 범위로 한다.
- 각 단계 후 Unity Console 오류와 정답->복원 흐름을 확인한다.
- 완료 시 수정 파일, Inspector 할당, 남은 아트/연출 의존성, QA 결과를 한국어로 보고한다.
```

