# ENGINEERING HARNESS

**4X Subculture Space Strategy — [프로젝트 가칭]**

*Unity 6 LTS DOTS · 결정적 락스텝 Co-op · PC 전용 (x64)*

**v1.1 — 본 문서는 모든 코드 작업 시 컨텍스트에 상시 포함**

> **기존 개발 가이드(STEP 1~10)와 충돌 시 본 Harness가 우선한다**

> ⚠️ **[v1.1 상태 고지 — 아키텍처 전환됨, 2026-06-29]**
> 본 문서는 **Unity DOTS + float 결정성** 시점에 작성된 헌법이다. 이후 시뮬레이션 코어를
> **엔진 독립 C++ DLL + 고정소수점(Fixed)** 으로 전환했다(엔진 교체 대비 + C ABI 경계에서
> 단일 Burst float 결정성이 사라지므로). **설계 원칙(두 시계 · Command가 유일한 입구 ·
> 스냅샷 복사본 · SimID · 매 틱 체크섬)은 그대로 유효**하며, 그것이 곧 C API 경계가 된다.
> 아래 항목만 구현 수단이 바뀌었으니 **C++ 코어 기준으로 재해석**할 것:
> - §1 "결정성 전략 = float + Burst FloatMode.Deterministic / 고정소수점 회피" → **고정소수점으로 대체**
> - §2 HARD RULE 중 Burst/`Unity.Mathematics`/`NativeCollections`/ECB 관련 → C++ + 정수연산 + EnTT(또는 배열)로 등가 적용
> - §9 셀프 체크리스트 1·2번(Burst·`Mathf`/`UnityEngine.Random`) → C++ 코어의 결정성 규칙(고정소수점·결정적 RNG)으로 환산
>
> 전환 경위·C++ 코어 진행 상황은 `LEARNING.md`("아키텍처 전환" 및 "C++ 코어 로드맵") 참조.

---

## 0. 문서 사용법 (Opus 지시사항)

본 문서는 아키텍처 헌법이다. 모든 STEP 작업 요청 시 이 문서가 함께 전달되며, 아래 규칙을 따른다.

- 코드 생성 전, 작성하려는 코드가 본 문서의 HARD RULE을 위반하지 않는지 먼저 검토한다.
- 기존 개발 가이드 문서(STEP 1~10)의 게임플레이 스펙(편제, 스탯 산식, 호감도, 내정 등)은 유효하다. 단, 아키텍처 관련 내용이 본 문서와 충돌하면 본 문서가 우선한다.
- HARD RULE을 지킬 수 없는 상황이 생기면 코드를 작성하지 말고 충돌 내용을 보고한다.
- 본 문서에 정의되지 않은 설계 결정이 필요하면, 임의로 결정하지 말고 선택지를 제시하고 질문한다.

### 0-1. CLI 협업 규칙 (필수)

**파일 직접 수정(Edit/Write)은 사용자가 명시적으로 "수정해줘"라고 요청한 경우에만 허용한다. 그 외 모든 경우는 아래 형식을 따른다.**

**[일회성 원칙] 수정 허가는 그 "수정해줘" 명령 한 동작에만 적용된다. 한 번 허가됐다고 이후까지 계속 유지되지 않는다.** 수정 지시가 포함된 그 턴의 대상 작업이 끝나면 즉시 기본값(코드 제시 전용)으로 복귀한다. 이어지는 "이어서 / 계속 / 다음 진행" 등은 수정 허가가 아니므로, 명시적 "수정해줘"가 다시 없으면 파일을 직접 건드리지 말고 아래 형식으로 코드만 제시한다.

코드를 제시할 때는 반드시 다음을 포함한 복사·붙여넣기 가능한 형태로 제공한다:

- **완성된 코드 블록** — 사용자가 그대로 복사해 붙여넣을 수 있는 단위
- **대상 위치** — 어느 파일, 어느 클래스/메서드에 들어가는 코드인지 명시
- **사용된 주요 함수·API 목록**과 각각의 역할 설명
- **코드 전체 흐름(Flow) 설명** — 호출 순서, 데이터가 어디서 와서 어디로 가는지
- **본 Harness 규칙과의 연관성** — 어떤 HARD RULE을 지키기 위한 구조인지

---

## 1. 확정 기술 결정 (변경 불가)

| 항목 | 결정 | 근거 |
|------|------|------|
| 엔진 | Unity 6 LTS + DOTS (Entities, Burst, Jobs, Collections) | 확정 |
| 플랫폼 | PC 전용 (Windows x64) | 크로스플레이 없음 |
| 플레이 모드 | 싱글 + Co-op 2~4인. 싱글 = 1인 락스텝 (로컬 릴레이, null transport) — 별도 싱글 경로 금지 | 단일 코드 경로로 멀티 버그 조기 발견 |
| 멀티플레이 | 결정적 락스텝, 호스트 릴레이 | 상태가 아닌 명령만 동기화 |
| 결정성 전략 | float + Burst FloatMode.Deterministic | PC x64 단일 아키텍처라 고정소수점 회피 |
| 넷코드 | 자체 구현 (Netcode for Entities 미사용) | NfE는 서버권위+예측 모델 — 락스텝과 불일치 |
| 기준 틱레이트 | 10 ticks/sec (1틱 = 100ms @ 1x) | 함재기 전투 해상도 + 보간으로 충분 |
| 게임 시간 | 1 게임일 = 10틱 (1초 @ 1x) | 내정 계산 단위 |
| 게임 속도 | 1x~5x = 틱당 실제 ms 변경 (100ms→20ms) | 틱 의미는 불변. 일시정지 = 틱 정지 |
| 데싱크 감시 | 매 틱 체크섬 — Day 1부터 구현 | 결정성 버그는 체크섬 없이 추적 불가 |
| 캐릭터 복잡 로직 | managed C# 허용 — 단, 정수 연산만 (float 금지) + Sim 상태 변경은 명령 경유 | managed float은 결정성 보장 밖. 호감도·조건은 int |

---

## 2. 결정성 규칙 — HARD RULES

> **아래 규칙 위반 = 멀티플레이 데싱크. 예외 없음.**
> Sim 상태(엔티티 위치, HP, 자원, RNG 등 게임 로직에 영향을 주는 모든 값)를 읽거나 쓰는 코드 전체에 적용된다.

### 2-1. 필수 (MUST)

| 규칙 | 상세 |
|------|------|
| Sim 코드는 전부 Burst 컴파일 | Sim 상태를 변경하는 모든 System/Job에 `[BurstCompile]` 필수. managed C# 수학 연산으로 Sim 상태 변경 금지 |
| FloatMode.Deterministic 강제 | `[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]` |
| Unity.Mathematics만 사용 | `math.sin`, `math.sqrt` 등. `Mathf.*`, `System.Math` 사용 금지 (Sim 내) |
| 결정적 RNG | `Unity.Mathematics.Random`을 틱+엔티티ID 기반 시드로 사용. `UnityEngine.Random` 절대 금지 |
| 결정적 순회 순서 | Sim 상태를 변경하는 순회는 안정된 정렬 키(EntityID 등) 기준 정렬 후 처리 |
| 병렬 Job 작성 규칙 | `IJobEntity`/`IJobChunk` 병렬 처리 시 결과가 순서 의존적이면 안 됨. 순서 의존 연산(예: 자원 합산 후 차감)은 단일 스레드 System으로 분리 |
| 구조 변경은 ECB | 엔티티 생성/삭제/컴포넌트 추가는 `EntityCommandBuffer`로 기록 후 고정된 시점(SystemGroup 끝)에 playback |
| 틱 체크섬 | 매 틱 종료 시 위치·HP·자원의 해시 기록 (xxHash 권장). 멀티에서 클라 간 비교 |
| SimID로 엔티티 참조 | 명령·체크섬·정렬·세이브는 결정적으로 부여되는 순차 `ulong SimID` 사용. DOTS Entity 핸들은 세이브/클라 간 불안정 — 직렬화 금지. SimID→Entity는 lookup 맵 (조회만 허용) |
| 캐릭터/스토리 레이어 정수 연산 | 호감도·트리거 조건·임계값 전부 `int`. managed C# 영역에서 float로 Sim 영향 값 계산 금지 |

### 2-2. 금지 (NEVER)

| 금지 항목 | 이유 |
|-----------|------|
| `NativeHashMap`/`NativeParallelHashMap` 순회로 Sim 상태 변경 | 순회 순서 비결정적. 조회(lookup)는 허용, 순회 후 쓰기는 금지 |
| `Time.deltaTime` / `Time.time`을 Sim에서 사용 | Sim은 논리 틱만 안다. 벽시계는 프레젠테이션 전용 |
| 하드웨어 intrinsic 직접 호출 | 플랫폼 간 결과 차이 가능 |
| Sim 코드에서 managed 객체 참조 | GC + Burst 비호환 + 비결정 위험 |
| `DateTime`, `Guid.NewGuid` 등 비결정 API | 클라마다 다른 값 |
| Sim 레이어에 maxDelay/스킵/저하 로직 | 클라별 독립 지연 = 데싱크. 부하는 전체 게임속도 저하로만 흡수 |
| 프레젠테이션 코드가 Sim 상태 직접 변경 | 렌더/UI/입력은 반드시 Command 경유 |

---

## 3. 시계 모델

### 3-1. 두 개의 시계

| 시계 | 단위 | 소유 | 용도 |
|------|------|------|------|
| 시뮬레이션 시계 | 정수 논리 틱 (`ulong tick`) | `SimTickDriver` | 모든 Sim 상태 변경. 전 클라 동일 진행 |
| 프레젠테이션 시계 | 벽시계 ms (`Time.time`) | 각 클라 독립 | 보간·렌더·이펙트·UI. Sim에 영향 절대 없음 |

### 3-2. 틱 진행 규칙

```csharp
// SimTickDriver (메인스레드, MonoBehaviour 또는 SystemGroup 루트)

tickIntervalMs = 100 / gameSpeed;   // 1x=100ms, 2x=50ms, 5x=20ms

accumulator += wallDeltaMs;

while (accumulator >= tickIntervalMs) {
    if (CanAdvanceTick())            // 락스텝: 틱 N의 전 클라 명령 수신 완료?
        RunSimTick(++currentTick);   // SystemGroup 일괄 실행
    else break;                       // 명령 미수신 — 대기 (게임이 느려짐)

    accumulator -= tickIntervalMs;
}
```

- **일시정지** = `RunSimTick` 호출 정지. 프레젠테이션 시계는 계속 돌아감 (카메라·UI 조작 가능).
- `CanAdvanceTick()`이 false인 상황(느린 클라)이 지속되면 전체 게임속도를 자동 하향 — 스텔라리스 방식.
- 일시정지·게임속도 변경은 로컬 즉시 처리 금지 — `PauseCommand` / `SpeedChangeCommand`로 틱 스탬프를 박아 전 클라가 동일 틱에 적용 (싱글도 동일 경로, 로컬 릴레이라 체감 차이 없음).
- 한 프레임에 여러 틱 실행 가능(고속 모드). 단 프레임당 최대 틱 수 상한을 둬서 spiral of death 방지 (권장: 5).

### 3-3. 게임 시간 정의

| 단위 | 정의 |
|------|------|
| 1 논리 틱 | 시뮬레이션 최소 단위 (@1x = 100ms 실시간) |
| 1 게임일 | 10틱 — 내정(자원·건설·연구) 계산 단위 |
| 1 게임월 | 300틱 (30게임일) — 외교·이벤트 등 거시 단위 |

---

## 4. SystemGroup 구조

Sim은 Unity 메인스레드가 구동하며, 병렬성은 Job System 워커 풀에서 나온다. raw 스레드 생성 금지.

### 4-1. 실행 순서 (틱 내 고정)

| 순서 | SystemGroup | 실행 조건 | 담당 |
|------|-------------|-----------|------|
| 1 | `CommandConsumeGroup` | 매 틱 | 틱 N 스탬프 명령 일괄 소비 (플레이어+AI) |
| 2 | `CombatGroup` | 매 틱 | 히트 판정 → 데미지 → 격파 처리, 함재기 이동 (병렬 Job) |
| 3 | `FleetMoveGroup` | `tick % 2 == 0` | 함선 편대·모선 이동 |
| 4 | `VisionGroup` | `tick % 4 == 0` | Sim 영향 가시성 (AI가 참조하는 시야) — 결정적 |
| 5 | `AISnapshotGroup` | `tick % 10 == 0` | `aiSnapshot` NativeArray 복사 + AI Job kick |
| 6 | `GameDataGroup` | `tick % 10 == 0` (게임일) | 자원 생산·소비, 건설, 유닛 생산, 연구 |
| 7 | `StoryGroup` | `tick % 10 == 0` | 호감도 변화, 스토리 트리거 조건 체크 |
| 8 | `DiplomacyGroup` | `tick % 20 == 0` | 세력 관계, 외교 이벤트, 외교 AI |
| 9 | `ChecksumGroup` | 매 틱 | 상태 해시 기록, 멀티 시 비교 큐 적재 |
| 10 | `RenderSnapshotGroup` | 매 틱 (마지막) | `renderSnapshot` 복사본 떠냄 (prev/curr 쌍) |

> **실행 순서는 `[UpdateInGroup]`/`[UpdateBefore]`/`[UpdateAfter]`로 명시적으로 고정한다. 순서가 암묵적이면 빌드마다 달라져 데싱크 가능.**

- 히트 판정은 이동 전 위치 기준(순서 2가 3보다 앞) — 이동하면서 맞는 비직관 제거.
- VisionGroup은 이동 결과를 다음 실행 시점에 반영 (2틱 지연 허용, 결정적이므로 문제없음).
- 분주기 체크(`tick % N`)는 SystemGroup 단위 RateManager로 구현. System 내부 `if`문 금지.

---

## 5. 명령 파이프라인 (락스텝 핵심)

Sim 상태를 바꾸는 유일한 입구. 플레이어 입력, 네트워크 수신 입력, AI 출력이 전부 여기로 들어온다.

### 5-1. Command 구조

```csharp
public struct SimCommand   // unmanaged, blittable
{
    public ulong  executionTick;   // 이 틱에 실행
    public byte   playerId;        // 0~3 (AI는 별도 대역: 100+)
    public ushort sequence;        // 같은 틱 내 발행 순서
    public CommandType type;       // enum: MoveFleet, BuildStructure, ...
    public CommandPayload payload; // union형 고정 크기 페이로드
}
```

### 5-2. 규칙

| 규칙 | 상세 |
|------|------|
| 정렬 키 | `(executionTick, playerId, sequence)` — 전 클라 동일 순서 보장 |
| 입력 지연 | 로컬 입력은 현재틱 + 3 스탬프 (@1x 약 300ms, 전략 게임 체감 미미) |
| AI 명령 | AI Job 완료 시 결과 명령에 미래 안전 틱 스탬프 (완료 시점 + 2틱 이상) |
| 소비 시점 | `CommandConsumeGroup`이 틱 시작 시 해당 틱 명령만 일괄 소비 |
| 검증 | 소비 시점에 유효성 검사 (대상 엔티티 생존? 자원 충분?) — 발행 시점 검사 금지 (틱 차이로 상태 다름) |
| 무효 명령 | 조용히 드랍 + 로그. 부분 실행 금지 (전부 실행 or 전부 드랍) |
| 대상 참조 | 명령 페이로드의 유닛·행성 참조는 `SimID(ulong)`만 사용. Entity 핸들 직렬화 금지 |
| 메타 명령 | 일시정지·속도변경·세이브 요청도 `SimCommand`로 처리 (`PauseCommand` 등) — 전 클라 동일 틱 적용 |
| 네트워크 | 호스트가 전 클라 명령 수집 → 틱별 묶음으로 전 클라 배포. 클라는 자기 명령도 호스트 echo를 기다려 소비 (자기만 먼저 실행 금지) |

---

## 6. 스냅샷 규약

> **스냅샷 = live World 밖으로 복사해 낸 순수 NativeArray. live World를 들여다보는 뷰/포인터 절대 금지. 이것이 Job 안전 시스템과 무관하게 읽을 수 있는 유일한 방법이다.**

### 6-1. aiSnapshot — AI 입력

| 항목 | 내용 |
|------|------|
| 생성 | `AISnapshotGroup`이 10틱마다 `IJobEntity`로 NativeArray에 복사 |
| 내용 | 함대 위치·전력·소속, 행성 상태, Sim 가시성 마스크. True State 기반 (락스텝이라 전 클라 보유 — 보안 의미 없음). AI 공정성(안개 너머 인지 여부)은 디자인 파라미터로 분리 |
| 포함 금지 | 함재기 개별 위치(AI는 소대 단위만), 호감도, 렌더 데이터 |
| 더블 버퍼 | NativeArray 2개 스왑. AI Job이 읽는 동안 다음 스냅샷은 다른 버퍼에 |
| 수명 | AI Job의 JobHandle 완료 전 Dispose 금지 |

### 6-2. AI 실행 모델 — Long-running Job

```csharp
// 10틱마다 (AISnapshotGroup)
if (aiJobHandle.IsCompleted) {
    aiJobHandle.Complete();

    CollectAICommands(aiResultBuffer);   // 결과 → Command Buffer (미래 틱 스탬프)

    CopySnapshot(world, aiSnapshot[next]);

    aiJobHandle = new AIThinkJob {
        snapshot = aiSnapshot[next],
        result   = aiResultBuffer
    }.Schedule();                        // 여러 프레임에 걸쳐 실행, 즉시 Complete 금지
}

// 미완료 시: 이번 주기는 skip — AI 판단이 늦어질 뿐 Sim은 결정적으로 진행
```

> AI 판단 지연은 결정성을 깨지 않는다 — "AI Job 미완료 시 skip"이라는 조건 자체가 모든 클라에서 동일하게 평가되도록, AI Job 완료 여부가 아니라 **"결과 명령이 Command Buffer에 도착했는지"**로 분기한다. 즉 AI 명령도 일반 명령과 동일하게 호스트 수집·배포를 거친다 (호스트만 AI Job 실행 권장 — 가장 단순).

### 6-3. renderSnapshot — 프레젠테이션 입력

| 항목 | 내용 |
|------|------|
| 생성 | `RenderSnapshotGroup`이 매 틱 마지막에 복사 (prev/curr 쌍 유지) |
| 내용 | 엔티티ID, 위치, 회전, HP비율, 소속, 유닛타입, 틱 번호 |
| FoW | 여기서는 적용하지 않음 — 프레젠테이션 단계에서 클라별 적용 (7장) |
| 소비 | 보간 Job이 읽음. live World 접근 금지 |

---

## 7. 프레젠테이션 규칙

이 영역만 벽시계 기반이며, 클라마다 달라도 되고, 우아한 저하(maxDelay·스킵·LOD)가 허용된다.

### 7-1. 보간

```csharp
float alpha = (Time.time - lastTickWallTime) / currentTickIntervalSec;
alpha = math.clamp(alpha, 0f, 1f);
pos = math.lerp(prev.pos, curr.pos, alpha);
rot = math.slerp(prev.rot, curr.rot, alpha);

// 함재기 급선회(각도차 45도 초과): 속도 벡터 기반 예측 보간으로 전환
```

- 보간 계산은 병렬 Job. Transform/UI 적용은 메인스레드 (Unity 제약).
- Entities Graphics 사용 시 보간 결과를 `LocalToWorld`에 직접 기록 가능.

### 7-2. FoW (프레젠테이션 필터)

| 규칙 | 상세 |
|------|------|
| 적용 위치 | 보간 Job 직전, 클라 자신의 시야 마스크로 필터 |
| Co-op 시야 | 아군 시야 공유 — 팀 단위 마스크 하나로 단순화 |
| 미가시 유닛 | `lastKnownPos` + `lastSeenTick` 유지 → 유령 표시 (반투명). 한 번도 안 본 유닛은 비표시 |
| Sim 가시성과 분리 | AI가 참조하는 가시성은 `VisionGroup`(Sim, 결정적). 렌더 FoW는 별개 — 혼용 금지 |

### 7-3. 허용되는 저하

- 이펙트·사운드 이벤트 드랍 (우선순위: 격파 > 스킬 > 히트), 원거리 유닛 보간 주기 하향, UI 갱신 스로틀.
- 단, Sim 상태 표시(자원 수치, HP바 값 자체)는 드랍 금지 — 표시가 늦는 건 허용, 틀린 값 표시는 금지.

---

## 8. 넷코드 — 자체 락스텝

### 8-0. 싱글플레이 = 1인 락스텝

| 항목 | 내용 |
|------|------|
| 전송 계층 | null transport (로컬 릴레이) — 명령이 네트워크 없이 즉시 자기에게 echo |
| 진행 조건 | `CanAdvanceTick()` 항상 true (자기 명령만 기다리면 됨) |
| AI | 로컬이 곧 호스트 — AI Job 로컬 실행 |
| 금지 | 싱글 전용 분기 경로 생성 금지. 차이는 전송 계층 구현체 하나로 격리 |
| 효과 | 싱글 플레이 중에도 명령 파이프라인·체크섬·리플레이가 전부 구동 — 멀티 버그 조기 발견 |

### 8-1. 토폴로지

| 항목 | 결정 |
|------|------|
| 구조 | 호스트 릴레이 — 호스트가 전 클라 명령 수집 후 틱별 묶음 배포 |
| 동기화 대상 | 명령만. 상태 동기화 없음 (은하 전체 상태는 대역폭 불가) |
| AI 실행 | 호스트만 AI Job 실행, 결과 명령을 일반 명령과 동일 경로로 배포 |
| 진행 조건 | 틱 N의 전원 명령 묶음 수신 완료 시에만 틱 N 실행 |
| 속도 제어 | 가장 느린 클라 기준 자동 속도 하향 (스텔라리스 방식) |

### 8-2. 데싱크 감시·복구

| 항목 | 내용 |
|------|------|
| 체크섬 | 매 틱 `ChecksumGroup`이 (위치, HP, 자원, RNG state) 해시 — EntityID 정렬 후 xxHash |
| 비교 | N틱마다(권장 10) 클라가 호스트로 체크섬 전송, 호스트가 비교 |
| 불일치 시 | 게임 일시정지 → 호스트 풀 세이브 직렬화 전송 → 전 클라 재로드 → 재개 |
| 세이브 규칙 | 틱 경계에서만 저장 (틱 중간 금지). 포함: `currentTick` + 전체 Sim 상태(SimID 기준) + RNG state + 미소비 명령 버퍼. 하나라도 빠지면 로드 후 결정성 붕괴 |
| 중도 참가 | 동일 경로 재사용 — 풀 세이브 전송 + 로드. 세이브 시스템과 코드 공유 설계 |
| 디버그 | 싱글 모드에서도 체크섬 기록 유지 — 리플레이 검증으로 결정성 버그 조기 발견 |

### 8-3. 구현 순서 권장

- **1단계 (지금)**: 체크섬 + 명령 파이프라인 + 틱 드라이버 — 싱글에서도 전부 구동
- **2단계**: 리플레이 (명령 기록·재생) — 결정성 검증 도구이자 기능
- **3단계**: 로컬 2클라 락스텝 (같은 PC 프로세스 2개) — 네트워크 없이 동기화 검증
- **4단계**: 실제 네트워크 전송 계층 (Unity Transport 권장)

---

## 9. Opus 셀프 체크리스트 (코드 제출 전 매번)

| # | 체크 항목 |
|---|-----------|
| 1 | Sim 상태를 바꾸는 코드에 `[BurstCompile(FloatMode.Deterministic)]`이 있는가 |
| 2 | Sim에서 `Time.*`, `Mathf.*`, `UnityEngine.Random`, `DateTime`을 쓰지 않았는가 |
| 3 | Sim 상태 변경이 Command 경유인가 (렌더/UI/입력이 직접 변경하지 않는가) |
| 4 | `NativeHashMap` 순회로 상태를 변경하지 않았는가 |
| 5 | 병렬 Job 결과가 실행 순서에 의존하지 않는가 |
| 6 | System 실행 순서가 `[UpdateBefore/After]`로 명시됐는가 |
| 7 | raw 스레드(`new Thread`)를 생성하지 않았는가 |
| 8 | 스냅샷이 live World 복사본인가 (뷰/포인터가 아닌가) |
| 9 | JobHandle 수명 관리가 됐는가 (Complete 전 Dispose 없음) |
| 10 | Transform/GameObject 접근이 메인스레드인가 |
| 11 | 수치가 하드코딩 없이 설정 데이터(BlobAsset/ScriptableObject)에서 오는가 |
| 12 | 벽시계 의존 코드가 프레젠테이션 영역에만 있는가 |
| 13 | 명령·체크섬·세이브에서 Entity 핸들 대신 `SimID`를 썼는가 |
| 14 | 일시정지·속도변경이 Command 경유인가 |
| 15 | 캐릭터/스토리 레이어에 float 연산이 없는가 (정수만) |
| 16 | 싱글 전용 분기를 만들지 않았는가 (전송 계층만 교체) |
| 17 | 코드를 파일에 직접 쓰지 않고 설명과 함께 제시했는가 (사용자가 직접 수정 요청한 경우 제외) |

---

## 10. TBD (추후 본 문서에 추가 예정)

- 코드 스타일·네이밍 컨벤션 — 사용자가 직접 작성하여 별도 전달
- 세이브 직렬화 포맷 상세 (8-2 재동기화·중도참가와 공유)
- AI 공정성 파라미터 (안개 너머 인지 여부) — 디자인 결정 대기
- 네트워크 전송 계층 상세 (4단계 진입 시)
