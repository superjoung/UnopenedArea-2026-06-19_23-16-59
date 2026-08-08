# 미기록 구역 콘텐츠 DB — Day 1~3

작성일: 2026-08-08  
용도: Day 1~3 구현의 단일 참조점. 기획 원문을 대체하지 않으며, Unity 세팅·아트 배정·런타임 구현의 기준표로 사용한다.

## 0. 문서 기준과 충돌 처리

| 우선순위 | 문서 | 이 DB에서 맡는 역할 |
|---|---|---|
| 1 | `이상현상 콘텐츠 기획서_v1.0.0 (1).xlsx` (개정 v1.3) | 이상현상 ID, 보고 정답, 대상 ID, 액션, 이미지/애니메이션 명세의 최신 기준 |
| 2 | `미기록_구역_시나리오_바이블.docx` | 3일차까지의 서사, 필수 발견물, 연출 의도 |
| 3 | `미기록_구역_이상현상_기획목록_선행조건정리.xlsm` | 일차별 진행 조건·선행 조건·구형 세부 흐름 |
| 4 | `현장 이동 상호작용 이벤트 설계서_v1.0.0.pdf` | 필드 상호작용·공포 이벤트의 구현 규칙 |
| 5 | PRD, 핵심로직 시나리오, 이상현상 시스템 설계서 | 공통 시스템/QA/아키텍처 기준 |

구형 문서와 v1.3의 개별 이상현상 내용이 다르면 **v1.3의 ID·보고유형·이미지·액션을 우선**한다. 구형 문서는 스토리 흐름을 보완하는 용도로만 쓴다. 대표 충돌: D2_A04(발소리 불일치 → 고개 회전), D2_A06(그림자 지연 → 피 흘리는 초상화), D2_A10/A11, D3_A01~A05의 구체 연출.

### 전체 문서 분석 결과

| 문서 | 반영한 핵심 |
|---|---|
| `미기록_구역_완성형_PRD_Codex용.md` | CCTV/필드 2모드, `[장소·대상·유형]` 보고, baseline 복구, 미보고 3회, 데이터 주도 구조 |
| `게임_핵심로직_시나리오_기능테스트.xlsm` | Day state와 테스트 관점: 정상/정답/오답/미보고/긴급/재시작이 독립적으로 복구되어야 함 |
| `미기록_구역_시나리오_바이블.docx` | 7일 구조 중 Day1~3의 테마, M0/O-06/CAM-00 서사, 문서 단서 배치 |
| `미기록_구역_이상현상_기획목록_선행조건정리.xlsm` | 각 일차 랜덤·필수·필드 이벤트의 선행 조건과 구형 상세 기획 |
| `이상현상 시스템 설계 문서_v1.0.0.docx` | SO 기반 이상현상, baseline/복구, 액션 기반 표현의 시스템 원칙 |
| `이상현상 콘텐츠 기획서_v1.0.0 (1).xlsx` | 이 DB의 최신 콘텐츠 원본. 보고형/미보고/필드/아트 명세 |
| `이상현상 콘텐츠 기획서_v1.0.0 - 01_이상현상.csv` | 구버전 목록. v1.3 xlsx와 충돌할 때 참조하지 않음 |
| `현장 이동 상호작용 이벤트 설계서_v1.0.0.pdf` | HoldProgress, InspectPopup, WireConnect/KeySequence 및 필드 공포 실행 규칙 |

## 1. 공통 데이터 규약

### 영역/보고 ID

| AreaId | 영역 | 보고 대상 예시 |
|---:|---|---|
| 100 | 생활관 복도 | 휠체어 100, 의자 101, 문 102, 조명 103, 초상화 104, 인물 200 |
| 200 | 치료실 | 문 102, 조명 103, 초상화 104, 커튼 105, 침대 107, 시계 109, 전화기 110, 인물 200 |
| 300 | 설비실 | 공구함 106, 모니터 202 |
| 400 | 서버실 | 서버 랙 203, 환기구 204 |

보고 유형: `PositionChange`, `Added`, `Missing`, `StateChange`, `ShapeChange`, `AbnormalBehavior`.

### 런타임 액션 해석

| 문서 액션 | Unity 액션/구성 | 비고 |
|---|---|---|
| 정적 추가/소실 | `SetActive` | 정상 상태와 이상 상태를 분리한다. |
| 정적 이미지 교체 | `ChangeSprite` | 변화 후의 단일 스프라이트를 `Target Sprite`에 배정한다. |
| 좌표 변화 | `MoveToLocalPosition` 또는 `MoveByLocalPositionOffset` | 목표 좌표는 프리팹 로컬 좌표 기준이다. |
| 애니메이션/반복 행동 | `PlayPresentation` + `AnomalyPresentationController` | 문서의 `use_animation=true`를 이 구성으로 흡수한다. `GlideHorizontal`, `AnimatorState`, 스프라이트 시퀀스용 프레젠테이션을 ID로 구분한다. |
| 색/회전 | `ChangeColor`, `SetLocalEulerAngles` | 회전만으로도 보고 유형은 대부분 `StateChange`다. |

정상 복구는 각 영역의 baseline snapshot을 기준으로 해야 한다. 하나의 대상에 중복 랜덤 이상현상을 동시에 적용하지 않고, 정답 보고/만료/일차 초기화 시 원상 복구한다.

## 2. 일차 공통 진행 DB

| Day | 감시 영역 | 최신 기획 시간/랜덤 간격 | 필수 현장 진입 | 핵심 서사 |
|---|---|---|---|---|
| 1 | 생활관·치료실·설비실 | 600초 / 45~90초 | D1_F01: 정답 3건 및 70% | 기준 화면을 학습하고, 제어 장치/정전 복구를 경험한다. |
| 2 | 생활관·치료실·설비실 | 600초 / 40~80초 | D2_F01: 정답 4건 또는 72% | 사람이 보고 대상이 되며, O-06 단서를 발견한다. |
| 3 | 생활관·치료실·설비실·서버실 | 600초 / 35~70초 | D3_F01: 서버실 필수 | 시간/영상 신뢰가 무너지고 O-06과 관측자의 연결이 드러난다. |

모든 날: 최대 미보고 3회, 같은 영역 동시 이상현상 금지, 미보고 공포는 보고·실패 카운트와 분리, 필드 중 일반 감시 이상현상 정지.

## 3. 스토리·필수 상호작용 DB

| Day | 진입/브리핑 의도 | 필수 상호작용/플래그 | 현장 연출과 귀환 |
|---|---|---|---|
| 1 | M0의 정상 배치 판별 업무를 처음 학습한다. | 차단기/배전반: `HoldProgress`로 기본 조작 학습. 문서 `DOC_001`은 M0 기본 지침, `DOC_002`는 선택 메모. | D1_F01에서 전력 복구. 게이지 중간에 조명 떨림·젖은 발자국 등 주변부 흔적을 1회 노출. |
| 2 | “사람도 보고 대상인가?”라는 불편함을 만든다. | D2_F01 기록 조각: `InspectPopup`, `D2_LOG_O06_READ`. 접근 1.5~2m + 화면 중앙 + 상호작용 키. | 확대 전환 0.2~0.35초, 65~75% 암전, 첫 1초 닫기 잠금. 확대 이미지에 찢긴 사진·지워진 문장·O-06·손자국. 재열람 가능, 공포 연출은 1회. |
| 3 | 실시간이라는 전제가 깨지고, 이전 관측자의 존재를 확인한다. | 서버 제어반: `WireConnect` 또는 `KeySequence`; 서버 재부팅 완료 플래그. | 조작 직후 복도를 볼 때 D3_F01을 단 한 번 판정. 마지막 프레임 O-06 명찰을 노출. |

스토리 플래그 서비스가 필요하다: `D1_F01_RESOLVED`, `D2_LOG_O06_READ`, `D3_SERVER_REBOOTED` 등. 필수 상호작용은 취소/실패해도 재시도 가능해야 하며 소프트락을 만들면 안 된다.

## 4. 보고형 이상현상 DB

### Day 1

| ID | 영역 / 정답 | 대상 Object ID | 액션 | 필요한 이미지/메모 |
|---|---|---|---|---|
| D1_A01 | 생활관 / 휠체어·위치 변경 | `OBJ_DORM_WHEELCHAIR_01` | 위치 이동(+ tween) | `wheelchair.png`; 목표 좌표 배정 |
| D1_A02 | 생활관 / 의자·추가됨 | `OBJ_DORM_CHAIR_EXTRA_01` | SetActive true | `chair.png`, `chair_x.png` |
| D1_A03 | 치료실 / 커튼·사라짐 | `OBJ_TREAT_CURTAIN_01` | SetActive false | `curtain1.png`, `curtain2.png`, `curtainRod.png` |
| D1_A04 | 생활관 / 초상화·형태 손상 | `OBJ_DORM_PORTRAIT_01` | ChangeSprite | `Portrait_x2.png` |
| D1_A05 | 생활관 / 조명·상태 변경 | `OBJ_DORM_LIGHT_02` | 상태/이미지 교체 | `Light_on.png`, `Light_off.png` |
| D1_A06 | 생활관 / 문·상태 변경 | `OBJ_DORM_DOOR_02` | ChangeSprite | 반쯤 열린 문 이미지 |
| D1_A07 | 치료실 / 환자 침대·형태 손상 | `OBJ_TREAT_BED_01` | ChangeSprite | `bed_x2.png` |
| D1_A08 | 설비실 / 공구함·위치 변경 | `OBJ_UTILITY_TOOLBOX_01` | 위치 이동(+ tween) | `toolbox.png`; 목표 좌표 배정 |
| D1_A09 | 생활관 / 비상구 표지·형태 손상 | `OBJ_DORM_EXIT_SIGN_01` | ChangeSprite | `exit_x.png` |
| D1_A10 | 치료실 / 벽시계·상태 변경 | `OBJ_TREAT_CLOCK_01` | ChangeSprite | `clock_x.png` (분·초침 및 중심축 없음) |
| D1_A11 | 치료실 / 전화기·위치 변경 | `OBJ_TREAT_PHONE_RECEIVER_01` | 위치 이동 | `TelShelf_x.png` |

### Day 2

| ID | 영역 / 정답 | 대상 Object ID | 액션 | 필요한 이미지/메모 |
|---|---|---|---|---|
| D2_A01 | 치료실 / 인물·추가됨 | `OBJ_TREAT_PATIENT_01` | SetActive true | `Art/Character/3. 치료실 환자/patient.png` |
| D2_A02 | 생활관 / 인물·비정상 행동 | `OBJ_DORM_STAFF_01` | GlideHorizontal | `Dorm_staff.png`; 걷지 않고 좌우 미끄러짐 |
| D2_A03 | 치료실 / 인물·추가됨 | `OBJ_TREAT_STAFF_DUP_01` | SetActive true | 생활관 직원 리소스 재사용 |
| D2_A04 | 생활관 / 인물·비정상 행동 | `OBJ_DORM_STAFF_02` | 스프라이트 시퀀스, 마지막 프레임 유지 | `고개만 돌아가는 생활관 직원/Dorm_staff_02-Sheet.png` |
| D2_A05 | 생활관 / 인물·상태 변경 | `OBJ_DORM_PERSON_END_01` | ChangeSprite | `Dorm_staff_back.png`; 일반 NPC는 끄고 끝 NPC만 표시 |
| D2_A06 | 치료실 / 초상화·형태 손상 | `OBJ_TREAT_PORTRAIT_FEMALE_01` | 4프레임 시퀀스, 마지막 유지 | `LabCorridor/Portrait_x.png` |
| D2_A07 | 생활관 / 초상화·형태 손상 | `OBJ_DORM_PORTRAIT_02` | ChangeSprite | `Portrait_x3.png` |
| D2_A08 | 치료실 / 커튼·비정상 행동 | `OBJ_TREAT_CURTAIN_02` | 6프레임 반복 | `호흡하듯 부푸는 커튼/curtain_ani-Sheet.png` |
| D2_A09 | 치료실 / 침대·형태 손상 | `OBJ_TREAT_BED_02` | 5프레임 반복 | `서서히 가라앉는 빈 침대/bed_ani-Sheet.png` |
| D2_A10 | 생활관 / 인물·형태 손상 | `OBJ_DORM_STAFF_01` | ChangeSprite | `Dorm_staff_x.png` |
| D2_A11 | 치료실 / 문·형태 손상 | `OBJ_TREAT_DOOR_01` | ChangeSprite | 괴물 입 문 1장 필요 |

### Day 3

| ID | 영역 / 정답 | 대상 Object ID | 액션 | 필요한 이미지/메모 |
|---|---|---|---|---|
| D3_A01 | 치료실 / 벽시계·비정상 행동 | `OBJ_TREAT_CLOCK_02` | 8프레임 반복 | 역회전 초침 애니메이션 필요 |
| D3_A02 | 치료실 / 인물·비정상 행동 | `OBJ_TREAT_PERSON_03` | 6프레임 반복 | 손 올렸다 내리는 루프 필요 |
| D3_A03 | 생활관 / 문·비정상 행동 | `OBJ_DORM_DOOR_03` | 7프레임 반복 | 닫힘→열림 루프 필요 |
| D3_A04 | 생활관 / 휠체어·비정상 행동 | `OBJ_DORM_WHEELCHAIR_02` | 두 지점 왕복 + 바퀴 애니메이션 | 4프레임 바퀴 필요 |
| D3_A05 | 설비실 / 모니터·형태 손상 | `OBJ_UTILITY_MONITOR_01` | 6프레임 반복 | 맥동하는 정지 화면 필요 |
| D3_A06 | 서버실 / 서버 랙·비정상 행동 | `OBJ_SERVER_RACK_01` | 6프레임 반복 | 아래→위 역순 점멸 필요 |
| D3_A07 | 생활관 / 벽시계·형태 손상 | `OBJ_DORM_CLOCK_PAIR_01` | ChangeSprite | 서로 다른 시각의 시계 2개 세트 필요 |
| D3_A08 | 서버실 / 환기구·비정상 행동 | `OBJ_SERVER_VENT_01` | 5프레임 반복 | 환기구 덮개 진동 필요 |
| D3_A09 | 설비실 / 공구함·위치 변경 | `OBJ_UTILITY_TOOLCART_01` | 위치 교환 | `toolcart.png`; 교환 좌표 필요 |
| D3_A10 | 치료실 / 조명·비정상 행동 | `OBJ_TREAT_LIGHT_ROW_01` | 5프레임 반복 | 좌→우 순차 소등 필요 |
| D3_A11 | 생활관 / 의자·위치 변경 | `OBJ_DORM_CHAIR_03` | 느린 위치 이동 | 복도 중앙 목표 좌표 필요 |

## 5. CCTV 미보고 공포 DB (보고 대상 아님)

| Day | ID | 영역 | 대상 / 이미지 | 트리거·확률·길이 | SFX 키 |
|---|---|---|---|---|---|
| 1 | D1_NR01 | 생활관 | `OBJ_DORM_EDGE_PERSON_01`, `Utility_Staff.png` | viewport 진입, 35%, 0.7초, 1회 | `SFX_D1_CLOTH_PASS` |
| 1 | D1_NR02 | 치료실 | UI overlay, `창백한 치료실 얼굴.png` | timed flash, 25%, 0.5초, 1회 | `SFX_D1_STATIC_HIT` |
| 1 | D1_NR03 | 설비실 | `OBJ_UTILITY_SHADOW_01` | viewport 진입, 30%, 0.6초, 1회 | `SFX_D1_METAL_TAP` |
| 2 | D2_NR01 | 치료실 | `OBJ_TREAT_CURTAIN_HAND_01`, `curtain_handshadow-Sheet.png` | viewport 진입, 35%, 0.8초, 1회 | `SFX_D2_CURTAIN` |
| 2 | D2_NR02 | 생활관 | UI overlay, 얼굴 이미지 재사용 | timed flash, 25%, 0.5초, 1회 | `SFX_D2_BREATH` |
| 2 | D2_NR03 | 치료실 | `OBJ_TREAT_UNDERBED_PERSON_01`, `bedunderstaff_ani-Sheet.png` | viewport 진입, 30%, 0.7초, 1회 | `SFX_D2_SCRATCH` |
| 3 | D3_NR01 | 서버실 | UI overlay, 서버 얼굴 이미지 필요 | timed flash, 30%, 0.5초, 1회 | `SFX_D3_DIGITAL_HIT` |
| 3 | D3_NR02 | 생활관 | `OBJ_DORM_CEILING_PERSON_01`, 천장 인물 5프레임 필요 | viewport 진입, 25%, 0.8초, 1회 | `SFX_D3_CEILING_STEP` |
| 3 | D3_NR03 | 치료실 | `OBJ_TREAT_REFLECTION_01`, 반사 인물 필요 | viewport 진입, 30%, 0.6초, 1회 | `SFX_D3_GLASS` |

## 6. 현장 이동 공포 DB

모두 한 방문 최대 1회, 기본 쿨다운 30초, 보고·실패 카운트 제외다.

| Day | ID | 필드 영역 | 대상 | 형식 | 이미지/상태 |
|---|---|---|---|---|---|
| 1 | D1_FA01 | 설비실 | `OBJ_FIELD_D1_EDGE_PERSON` | viewport / one-shot | `Utility_Staff.png` 재사용 |
| 1 | D1_FA02 | 설비실 | `OBJ_FIELD_D1_DOOR_GAP_PERSON` | viewport / one-shot | `behind_door_staff-Sheet.png` |
| 1 | D1_FA03 | 설비실 | `OBJ_FIELD_D1_HANDPRINT_FLASH` | 고정 시야 / 4단계 one-shot non-loop | `handprint-Sheet.png` |
| 2 | D2_FA01 | 치료실 | `OBJ_FIELD_D2_CURTAIN_HAND` | viewport / one-shot | 커튼 손 재사용 |
| 2 | D2_FA02 | 치료실 | `OBJ_FIELD_D2_UNDERBED_PERSON` | viewport / one-shot | 침대 밑 인물 재사용 |
| 2 | D2_FA03 | 치료실 | `OBJ_FIELD_D2_BLOODY_PORTRAIT` | 고정 시야 / 4단계 one-shot non-loop | 피 초상화 재사용 |
| 3 | D3_FA01 | 서버실 | UI overlay | timed flash / one-shot | 서버 얼굴 이미지 필요 |
| 3 | D3_FA02 | 서버실 | `OBJ_FIELD_D3_SERVER_VENT` | 고정 시야 / one-shot non-loop | 환기구 5프레임 필요 |
| 3 | D3_FA03 | 생활관 | `OBJ_FIELD_D3_CEILING_PERSON` | viewport / one-shot | 천장 인물 재사용 |
| 3 | D3_F01 | 서버실 | `OBJ_FIELD_D3_OBSERVER_RUSH` | 필수 시야 각도 돌진, 0.2초 | 이전 관측자 7프레임, 마지막 O-06 명찰 |

## 7. 현재 Unity 대조 결과 (2026-08-08)

### 이미 존재/진행 중

- D1 보고형 SO는 `D1_A01`~`D1_A28`까지 존재한다. 최신 v1.3의 기본 11개 외에 이전 작업에서 추가한 Day1 콘텐츠가 포함되어 있다.
- D2/D3 보고형 SO는 각각 최신 기본 범위인 `A01`~`A11`까지 존재한다.
- Area 프리팹은 `Area_LabCorridor`, `Area_TreatmentRoom`, `Area_UtilRoom`, `Area_MainRoom`, `Area_CCTVRoom`, `Area_ControllRoomExit`가 있다.
- D2 치료실의 `Patient1/2`, `Portriat`(실제 오브젝트 철자), Curtain2, Bed2에는 최근 대상 ID/프레젠테이션 구성 작업이 들어가 있다.
- `AnomalyAction`에는 `PlayPresentation`과 `SetAnimatorEnabled`가 추가돼 있고, `AnomalyPresentationController`가 존재한다.
- `DayDefinition.EmergencyObjectiveType`과 `FieldStoryRecordInspect`를 추가했다. Day2는 `StoryRecordInspection`으로 설정되어, D1 배전반 대신 O-06 기록 조사 완료가 긴급 목표를 끝낸다.
- D1/D2용 인물·커튼·침대·필드 공포 리소스는 상당수 `Assets/Art`에 존재한다.

### 최신 기획과 다른 현재 설정

| 항목 | 최신 기획 | 현재 확인값 | 조치 |
|---|---|---|---|
| Day2 길이 | 600초 | 300초 | 밸런싱 확정 후 `Day2Definition.durationSec` 수정 |
| Day2 랜덤 간격 | 40~80초 | 15~25초 | 테스트용인지 확인 후 수정 |
| Day2 최초 고정 | 최신표는 전부 랜덤 | 20초 D2_A01 고정 | 의도된 인물 신고 튜토리얼이면 문서에 예외로 기록, 아니면 제거 |
| Day3 일차 정의 | `Day3Definition`, 서버실 포함 | Day3 Definition 자산 미확인 | 생성 필요 |
| 서버실 | AreaId 400 / 감시·필드 공용 | Area 프리팹 없음 | `Area_ServerRoom` 제작 필요 |
| CCTV/필드 공포 데이터 | NR/FA 정의 및 runner | 전용 Definition/Runner 스크립트 미확인 | 아래 P0 선행 구현 |
| Day2 대사 | Day2 전용 전화 스크립트 | Day2 씬에 StoryDialogueController 미배정 | Day별 대사 데이터/컨트롤러 분리 |

## 8. 먼저 깔아야 할 선행 구현 (우선순위)

### P0 — Day2/Day3 콘텐츠를 넣기 전에

1. **공통 루트 프리팹화**: `CommonRoot`(카메라, CCTV 시스템, UI, 오디오, 전환, pause)와 `DayContentRoot`(영역 프리팹, day definition, 대사, 필드 이벤트)를 분리한다. Day1 수정 때 Day2를 다시 복사하는 문제를 없앤다.
2. **DayDefinition 3종 정합화**: Day1/Day2/Day3 각각의 시간·활성 영역·랜덤 풀·긴급 이벤트를 최신 DB 값으로 확정한다. 현재 Day2는 테스트값이 남아 있다.
3. **스토리 플래그/상호작용 데이터**: `FieldInteractionDefinition` 또는 동등한 SO에 `interaction_id`, day, area, target, type, 거리, 취소 가능, 1회성, 성공 flag, linked horror event를 둔다.
4. **CCTV/필드 공포 러너**: `ViewportIntrusion`, `ScreenFlash`, `SequentialTraceReveal`, `VisionAngleRush`를 데이터로 실행하고, 확률은 동일 시야 구간에서 한 번만 판정한다.
5. **애니메이션 공통 플레이어**: 스프라이트 시퀀스/Animator/좌표 tween을 `PresentationId`로 표준화한다. 반복과 마지막 프레임 고정 옵션이 필요하다.

### P1 — Day2 완성

1. D2_A01~A11 대상 ID와 정상 baseline을 Day2 프리팹에 전부 배정한다.
2. D2_A11 괴물 입 문 이미지, D2 NR/FA 대상·오버레이·SFX 키를 추가한다.
3. D2_F01 기록 조각 6장, 확대 이미지, `D2_LOG_O06_READ` 플래그를 구현한다.
4. Day2 전용 전화 대사와 튜토리얼 정책을 배정한다. Day1의 튜토리얼/상태 문자열을 공용 UI가 재사용하지 않게 한다.

### P2 — Day3 제작 착수

1. `Area_ServerRoom` CCTV/필드 프리팹과 `AreaId.ServerRoom(400)` 등록.
2. Day3 보고형 11개 대상, D3Definition, 서버 제어 상호작용을 추가.
3. D3 스프라이트 시퀀스 8종과 공포 리소스(서버 얼굴, 천장 인물, 반사 인물, 이전 관측자)를 제작/임포트.
4. D3_F01 강제 돌진과 O-06 연계를 구현.

## 9. QA 체크리스트

- 보고 정답 직후 최소 안전 간격 내에 새 이상/정전이 겹치지 않는다.
- 올려진 보고판은 눈 깜빡임·정전·최종 실패·필드 전환 전에 자동으로 내려간다.
- 정답/만료/재시작/일차 변경 후 sprite, 위치, 활성 상태, animator, camera 위치가 baseline으로 복구된다.
- 모든 미보고/현장 공포는 보고 선택지에 추가되지 않고, 1회성/쿨다운/필드 방문 제한을 지킨다.
- 필드 팝업 동안 이동·카메라·보고 UI·돌진 이벤트가 동시 실행되지 않는다.
- Day2/Day3 진입 시 Day1 상태 라벨, 튜토리얼, 전화 대사가 남지 않는다.
