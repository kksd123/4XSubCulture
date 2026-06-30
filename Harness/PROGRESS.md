# 개발 진행 상황 (PROGRESS)

**4X Subculture Space Strategy** · Unity 6 LTS DOTS · 결정적 락스텝 Co-op

> 이 문서는 STEP 작업 진행 상황을 추적한다. 매 작업 시작/종료 시 갱신한다.
> 함께 참조: `Engineering_Harness_v1_1.md` (아키텍처 헌법), `Develop_Guide.md` (STEP 스펙)
>
> **주의**: 실제 구현은 Develop_Guide의 코드 샘플과 네이밍·구조가 일부 다르다(아래 "가이드 대비 구현 차이" 참조).
> 가이드는 설계 스펙이고, 본 문서가 **실제 코드 상태의 단일 기준**이다.

---

## 현재 상태

| 항목 | 값 |
|------|-----|
| 최종 갱신 | 2026-06-17 |
| 완료 STEP | STEP 1, STEP 2, STEP 3, STEP 4(코어) |
| 다음 작업 | **STEP 5 — 전투 지휘형 + 스킬 시스템** |
| Unity 버전 | 6000.3.13f1 |
| DOTS 패키지 | Entities 1.4.7 · Burst 1.8.19 · Collections 2.6.7 · Mathematics 1.3.2 · Entities.Graphics 1.4.20 ✅ 설치됨 |

---

## STEP 진행 체크리스트

| STEP | 시스템 | 상태 |
|------|--------|------|
| 1 | 프로젝트 설정 + 코어 아키텍처 | ✅ 완료 |
| 2 | 틱 드라이버 + 명령 파이프라인 | ✅ 완료 |
| 3 | 체크섬 + 리플레이 | ✅ 완료 (리플레이 결정성 검증 루프는 세이브/로드 대기) |
| 4 | 전투 — 편제 구조 (함선·모선·소대) | ✅ 완료 (코어: 3계층 컴포넌트·ShipData·스탯 합산·스폰 팩토리. 런타임 검증용 SO 에셋·스폰 호출부는 STEP 7) |
| 5 | 전투 — 지휘형 + 스킬 시스템 | ⬜ |
| 6 | 전투 — 카메라 + 전투 HUD | ⬜ |
| 7 | 전투 — BattleManager + 결과 처리 | ⬜ |
| 8 | 내정 — 행성 + 건물 + 자원 | ⬜ |
| 9 | 내정 — 시민 카드 + 지지도 | ⬜ |
| 10 | 캐릭터 — 데이터 + 성장 시스템 | ⬜ |
| 11 | 캐릭터 — 호감도 + 스토리 트리거 | ⬜ |
| 12 | 미니 캠페인 통합 씬 | ⬜ |

---

## 실제 폴더 구조 (현재)

> 가이드의 `Assets/_Game/Scripts/...` 대신 아래 구조를 사용 중. 네임스페이스는 `Game.Core`, `Game.Core.Data`, `Game.NetCode`.

```
Assets/Scripts/Game/
├─ Core/
│   ├─ Data/        BaseStats, CharacterBonus, ResourceBundle, SimID, GameDataBase
│   ├─ BCSingleton.cs        (IBCSingleton + BCSingleton<T> ECS 싱글톤 헬퍼)
│   ├─ BCMonoSingleton.cs    (MonoBehaviour 싱글톤 베이스)
│   ├─ SimClock.cs           (currentTick / isPaused / gameSpeedX10)
│   ├─ SimIdRegistry.cs      (SimID→Entity 매핑, 조회 전용)
│   ├─ SimRandom.cs          (결정적 RNG — tick+simId 시드)
│   ├─ SimSystemGroups.cs    (SimRootGroup + 10개 SystemGroup 선언)
│   ├─ SimTickDriver.cs      (정수 틱 드라이버 + 그룹 부트스트랩)
│   ├─ PausableRateManager.cs(옵션 A 일시정지 게이트)
│   └─ EventBus.cs           (GameEvents 정적 이벤트 버스)
├─ NetCode/
│   ├─ ITransport.cs         (ITransport + NullTransport)
│   ├─ SimCommand.cs         (eCommandType + SimCommand/CommandPayload)
│   ├─ SimCommandQueue.cs    (전역 명령 큐 — ECS 싱글톤)
│   ├─ CommandConsumeSystem.cs
│   ├─ ChecksumLog.cs        (RingBuffer 기반 체크섬 저장소)
│   ├─ ChecksumSystem.cs     (매 틱 SimID 정렬 해시)
│   └─ ReplayRecorder.cs     (명령 기록·재생·검증 도구)
└─ Battle/                   (STEP 4 진행 중)
    ├─ BattleEnums.cs        (eTeam, eShipClass)
    ├─ Components/           FleetUnitComponent, SquadronCarrierComponent,
    │                        StrikeSquadronComponent, StatsNeedRecalcTag
    ├─ Data/                 ShipData (SO)
    ├─ System/               StatCalculatorSystem (스탯 합산)
    └─ FleetFactory.cs       (편제 엔티티 런타임 스폰 + SimID 결정적 부여)
```

---

## STEP 1 — 완료 내역

### 생성 파일 (Core/Data + Core)

| 파일 | 위치 | 내용 |
|------|------|------|
| `BaseStats.cs` | Core/Data | 공통 스탯 unmanaged struct |
| `CharacterBonus.cs` | Core/Data | 배치 보정값 struct + `eSlotType{kLeader,kSub1,kSub2}` enum |
| `SimID.cs` | Core/Data | `SimIDComponent`(`ulong Value`) — Entity 참조 대체 |
| `ResourceBundle.cs` | Core/Data | 자원 5종 struct(전부 int) + 연산자 |
| `GameDataBase.cs` | Core/Data | 모든 SO의 공통 베이스(`dataId`,`displayName`) |
| `EventBus.cs` | Core | `GameEvents` 정적 이벤트 버스 + `Raise*` 헬퍼 (Sim→프레젠 단방향) |
| `SimSystemGroups.cs` | Core | `SimRootGroup` + SystemGroup 11개 선언 (Harness 4장 실행 순서 고정) |

### 적용된 코드 컨벤션 (g_client 기반)
- 네임스페이스: `Game.Core`, `Game.Core.Data`, `Game.NetCode`
- enum 타입 `e*` 접두사 / 값 `k*` 접두사 — **확정 적용** (`eSlotType`, `eCommandType`)
- 멤버 private 필드 `m_`/`mc_` 접두사 (`m_transport`, `mc_localPlayerId` 등 STEP 2부터 적용)
- DOTS struct/컴포넌트 public 데이터 필드는 직렬화/Burst 위해 유지
- Allman 중괄호, 필드는 클래스 하단

### 가이드와의 차이
- `GameEvents`의 `OnBuildingCompleted`/`OnStoryTriggered`는 아직 미존재 SO 대신 `ScriptableObject`로 임시 시그니처. 해당 STEP(8/11)에서 `BuildingData`/`StoryEventData`로 교체 예정.

---

## STEP 2 — 완료 내역

락스텝 핵심: 싱글/멀티 단일 경로 틱 드라이버 + 명령 파이프라인.

### 생성 파일

| 파일 | 위치 | 내용 |
|------|------|------|
| `ITransport.cs` | NetCode | 전송 계층 인터페이스 + `NullTransport`(싱글 echo, `IDisposable`) |
| `SimCommand.cs` | NetCode | `eCommandType`(k접두사) + blittable `SimCommand`(정렬키 CompareTo) + `CommandPayload`(Explicit, 32B) |
| `SimCommandQueue.cs` | NetCode | 전역 명령 큐 — ECS 싱글톤. `EnqueueBatch`(정렬), `ConsumeTick`(해당 틱 묶음 소비) |
| `CommandConsumeSystem.cs` | NetCode | SystemGroup 1번. Burst Deterministic. 검증→드랍/디스패치, Pause/Resume/Speed 처리 |
| `SimClock.cs` | Core | 틱 상태 ECS 싱글톤 (`currentTick`,`isPaused`,`gameSpeedX10`) |
| `SimIdRegistry.cs` | Core | SimID→Entity 매핑 ECS 싱글톤 (Register/TryGetEntity/UnRegister — 조회만) |
| `SimTickDriver.cs` | Core | 정수 논리 틱 드라이버 (`BCMonoSingleton`), accumulator·속도·메타명령 발행 |
| `BCSingleton.cs` | Core | `IBCSingleton` + `BCSingleton<T>` (Ensure/TryGet/Get/Set) ECS 싱글톤 헬퍼 |
| `BCMonoSingleton.cs` | Core | MonoBehaviour 싱글톤 베이스 |

### Harness HARD RULE 준수 확인
- ✅ Sim 시계 = 정수 논리 틱. `Time.deltaTime`은 accumulator 계산 전용(Sim 상태 변경 X)
- ✅ 일시정지·속도변경도 Command 경유 (`RequestPause/Resume/SetSpeed` → `SendMeta`)
- ✅ `SimCommand` unmanaged + blittable, 정렬키 `(executionTick, playerId, sequence)`
- ✅ 무효 명령 조용히 드랍 (소비 시점 검증)
- ✅ SystemGroup 실행 순서 `[UpdateInGroup]`/`[UpdateAfter]`로 명시 (`SimSystemGroups.cs`)
- ✅ `CommandConsumeSystem`에 `[BurstCompile(FloatMode.Deterministic)]`

### 가이드 대비 구현 차이 (중요)
- **상태를 정적 클래스가 아닌 ECS 싱글톤 컴포넌트로 보관**: 가이드의 static `CommandBuffer`/`SimIDLookup` 대신
  `SimCommandQueue`/`SimIdRegistry`를 `IComponentData`로 구현하고 `BCSingleton<T>` 헬퍼로 관리.
  → World 단위 수명 관리·세이브 직렬화·테스트가 쉬워짐.
- **SystemGroup 수동 부트스트랩**: 모든 그룹이 `[DisableAutoCreation]`. `SimTickDriver.Initialize`에서
  `SimRootGroup`을 만들고 그룹/시스템을 `AddSystemToUpdateList` 후 `SortSystems`로 수동 등록.
  → 틱 진행이 `m_simRoot.Update()` 한 번으로 결정적으로 묶임.
- **명령 enum**: `CommandType` → `eCommandType`, 값에 `k` 접두사 (`kPause` 등).
- **이름**: `CommandBuffer`→`SimCommandQueue`, `SimIDLookup`→`SimIdRegistry`.

---

## STEP 3 — 완료 내역

목표: 결정성 버그 조기 발견 도구(체크섬 + 리플레이) 구현. 싱글 개발 중에도 항상 구동.

### 생성/구현 파일
| 파일 | 위치 | 내용 |
|------|------|------|
| `ChecksumLog.cs` | NetCode | RingBuffer 기반 체크섬 저장소(ECS 싱글톤). `TickChecksum{tick,hash}`, `Record`/`TryGet`(최근 256틱 보관) |
| `SimRandom.cs` | Core | 결정적 RNG ECS 싱글톤. `ForEntity(baseSeed,tick,simId)` → `Unity.Mathematics.Random` (틱+엔티티 시드) |
| `ChecksumSystem.cs` | NetCode | 매 틱 SimID 정렬 후 FNV-1a 해시 → `ChecksumLog.Record`. `[UpdateInGroup(ChecksumGroup)]` + Burst Deterministic. `ChecksumEntry`(SimID 정렬 키) 포함 |
| `ReplayRecorder.cs` | NetCode | 명령 기록(`SortedDictionary<tick,List<SimCommand>>`) + transport 재주입(`InjectAll`) + 체크섬 캡처/비교(`CaptureChecksums`/`CompareChecksums`) + JSON 저장/불러오기. `ReplayCommandDto`/`ReplayFileDto` 직렬화 |
| `PausableRateManager.cs` | Core | 옵션 A 일시정지 게이트. `isPaused` 시 부착 그룹 스킵(원샷 `IRateManager`) |

### 배선 (SimTickDriver)
- `ChecksumLog` 싱글톤 할당(링버퍼 256틱) + OnDestroy 해제.
- Harness 4장 전 SystemGroup(`CommandConsume`~`RenderSnapshot`)을 `SimRootGroup`에 등록 → `[UpdateAfter]` 체인 무결, 실행 순서 결정적.
- `ChecksumSystem`을 `ChecksumGroup`에 등록.
- 매 틱 소비 직전 `ReplayRecorder.RecordTick` 훅.
- 게임플레이 그룹(`CombatGroup`~`DiplomacyGroup`)에 `PausableRateManager` 부착.

### 일시정지 모델 = 옵션 A (확정)
- 틱·`CommandConsumeGroup`·`ChecksumGroup`·`RenderSnapshotGroup`은 **항상 실행** → `kResume` 명령이 정지 중에도 소비되어 교착 없음.
- `isPaused`이면 게임플레이 그룹만 스킵. pause/resume은 소비된 그 틱에 즉시 반영.
- 일시정지 중에도 **틱 번호는 증가**(게임플레이만 동결). 전 클라 동일 진행이라 결정적.

### Harness HARD RULE 준수 확인
- ✅ `ChecksumSystem` Burst Deterministic + EntityID(SimID) 정렬 후 해시 (순회 비결정 제거)
- ✅ RNG는 `Unity.Mathematics.Random`(틱+엔티티 시드), `UnityEngine.Random` 미사용
- ✅ 리플레이는 Sim 직접 변경 없이 transport(Command) 경유 재주입
- ✅ 일시정지는 System 내부 `if`가 아닌 RateManager로 게이트

### 잔여 (차단/연기)
- [ ] **리플레이 결정성 검증 루프(리셋→재생→재비교) 완성** — 틱 0 상태 리셋이 필요하며, 이는 Harness 8-2 세이브/로드와 코드 공유로 구현 예정(현재 기록·영속화·재주입·해시비교 골격까지 동작).
- [ ] 멀티용 `ChecksumLog.FlushToNetwork` (10틱마다) — 네트워크 STEP에서.

---

## STEP 4 — 완료 (코어) · 편제 구조

목표: 함선 편대(Squad) / 모선(타이탄급) / 함재기 소대(Squadron) 3계층 편제. 스탯은 BaseStats + CharacterBonus 합산.

### 생성/구현 파일
| 파일 | 위치 | 내용 |
|------|------|------|
| `BattleEnums.cs` | Battle | `eTeam{kPlayer,kEnemy,kNeutral}`, `eShipClass{kFrigate,kDestroyer,kCruiser,kBattleShip,kCarrier=10}` |
| `FleetUnitComponent.cs` | Battle/Components | 함선 편대 컴포넌트. `shipDataId`,`currentSquadSize`,`commanderSimId`,`baseStats`,`currentStats`,`teamId` |
| `SquadronCarrierComponent.cs` | Battle/Components | 모선 컴포넌트. `carrierDataId`,`assignedSquadronSimId`,`commanderSimId`,`baseStats`,`currentStats`,`isSquadronLaunched`,`teamId` |
| `StrikeSquadronComponent.cs` | Battle/Components | 함재기 소대. `carrierSimId`,`leaderSimId`,`sub1SimId`,`sub2SimId`,`baseStats`,`currentStats`,`isActive` |
| `StatsNeedRecalcTag.cs` | Battle/Components | 스탯 재계산 트리거 태그 |
| `CharacterBonusComponent.cs` | Battle/Components | 배치된 캐릭터 엔티티의 배치 보정값(`CharacterBonus`) 운반. STEP 10에서 `CharacterData.deployBonus`로 채워질 예정. **[확정] STEP 10에서 캐릭터 런타임 구조 확정 시 그쪽으로 흡수/이전 (임시 브리지)** |
| `ShipData.cs` | Battle/Data | ShipData SO (`baseStats`,`shipClass`,`squadSize`,`isCarrier`,`squadronCapacity`) |
| `StatCalculatorSystem.cs` | Battle/System | 편대·모선·소대 스탯 합산. `[UpdateInGroup(CommandConsumeGroup)]` + `[UpdateAfter(CommandConsumeSystem)]`, Burst Deterministic. 처리 후 `StatsNeedRecalcTag` 제거(ECB). `ResolveBonus`는 SimID→`SimIdRegistry`→`CharacterBonusComponent` 조회(lookup만) |
| `FleetFactory.cs` | Battle | 편제 엔티티 런타임 스폰 팩토리(managed, 메인스레드/초기화 시점). `SpawnFleet`/`SpawnCarrier`/`SpawnSquadron`/`Spawn`(isCarrier 분기)/`AttachSquadron`. 각 엔티티에 `SimIDComponent`+`LocalTransform`+편제 컴포넌트+`StatsNeedRecalcTag` 부착. SimID는 `SimIdRegistry.Register`로 결정적 부여(nextId write-back). `shipDataId`는 `dataId` FNV-1a 해시 |

### 배선 (SimTickDriver)
- `StatCalculatorSystem`을 `CommandConsumeGroup`에 등록(`SimTickDriver.cs:63`, `CommandConsumeSystem` 직후).

### 스폰 경로 (확정: 런타임 스폰 팩토리)
- 베이킹(SubScene/Baker) 대신 **`FleetFactory` 런타임 스폰** 채택 — 수동 World 부트스트랩·런타임 `SimIdRegistry`와 정합. 전 클라가 동일 순서로 호출 시 SimID 일치(락스텝 정합).
- 스폰 흐름: `CreateEntity` → `SimIdRegistry.Register`(SimID) → `SimIDComponent`/`LocalTransform`/편제 컴포넌트/`StatsNeedRecalcTag` 부착 → 다음 틱 `StatCalculatorSystem`이 `currentStats` 계산. 체크섬(`SimIDComponent`+`LocalTransform`)에도 자동 포함.
- 경계: **초기 전투 편제 셋업**은 팩토리 직접 호출(틱 루프 밖). **게임 중 유닛 생산**은 별도로 Command+ECB 경유(STEP 8 `kProduceUnit`).

### 수정 내역 (2026-06-17)
- ✅ `FleetUnitComponent`를 managed `class` → unmanaged `struct`로 변경 (HARD RULE: Sim 컴포넌트는 unmanaged. `RefRW<>` 접근 가능하도록).
- ✅ `SquadronCarrierComponent` / `StrikeSquadronComponent`에 `: IComponentData` + `using Unity.Entities;` 추가 (쿼리 컴파일 가능하도록).
- ✅ `StatCalculatorSystem.ResolveBonus` 실제 구현 — `CharacterBonusComponent` 신설 + `ComponentLookup`으로 SimID→엔티티→보정값 조회. `simId==0`/미등록/레지스트리 미생성(`map.IsCreated`)/컴포넌트 없음 → `default`. HARD RULE 준수(NativeHashMap 조회만).
- ✅ `ShipData.cs` `[Header(...)]` 한글 인코딩 깨짐 → UTF-8 재작성.

### 잔여 (교차 STEP / 에디터)
- [ ] 실제 `ShipData` SO 에셋 제작(`Create/Game/Ship_Data`) + 스탯 수치표 입력 — 에디터 작업.
- [ ] 스폰 호출부(전투 초기 편제 셋업) — STEP 7 `BattleManager.StartBattle`에서 `FleetFactory` 호출 + 런타임 검증.
- [ ] `StrikeSquadronComponent.baseStats`("더미 7기 기준") 소스 — 현재 `SpawnSquadron` 인자로 주입. 전용 SO/수치표 필요 시 추가.
- [ ] `BattleEnums.cs`(`eTeam`/`eShipClass`)가 전역 네임스페이스 — `Game.Battle`로 이동 권장(컴파일엔 무관).
- [ ] `CharacterBonusComponent`를 실제로 채우는 배치 흐름 — STEP 10(캐릭터 데이터/성장)에서 `CharacterData.deployBonus` 기반으로 연결 예정. (현재는 컴포넌트가 부착돼 있으면 조회되지만, 부착·기록 경로는 미구현)
  - **[확정] STEP 10 작업 시: 캐릭터 런타임 구조가 확정되면 `CharacterBonusComponent`를 그쪽으로 흡수/이전하고, `StatCalculatorSystem.ResolveBonus`의 조회 대상을 교체한다 (현재는 임시 브리지).**

---

## 미해결 / 확인 필요 (TODO)

- [x] ~~**일시정지 게이트 미연결**~~ — 옵션 A(`PausableRateManager`)로 해결. 게임플레이 그룹만 게이트, resume 교착 없음.
- [x] ~~**SystemGroup 배선 미완**~~ — `CommandConsume`~`RenderSnapshot` 전 그룹을 `SimRootGroup`에 등록 완료.
- [ ] **분주기(RateManager) 미적용** — Harness 4장의 `tick % N` 실행 조건(FleetMoveGroup·GameDataGroup 등)은 아직 RateManager로 구현되지 않음. 현재 `PausableRateManager`는 일시정지 전용. 각 STEP에서 분주기 RateManager 추가 필요(일시정지 게이트와 합성 주의).
- [x] ~~DOTS 패키지 설치 확인~~ — Entities/Burst/Collections/Mathematics/Entities.Graphics 설치 완료.
- [x] ~~enum 네이밍 확정~~ — g_client식 `e*`/`k*` 채택 확정.

---

## 커밋 이력 매핑 (참고)

| 커밋 | STEP |
|------|------|
| `Core #1` | STEP 1 |
| `틱 드라이버, 명령 파이프라인 작업` | STEP 2 |
| `meta 파일, Random 함수` | STEP 3 (SimRandom) |
| `최근 N틱 저장하는 RingBuffer 기반 체크섬` | STEP 3 (ChecksumLog) |
| `체크섬 + 리플레이 기반 작업` | STEP 3 (ChecksumSystem/ReplayRecorder) |
| `스탯 시스템 작업 및 편대 작업` | STEP 4 (편제 컴포넌트·ShipData·StatCalculatorSystem) |
