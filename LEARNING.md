# 4X 프로젝트 학습 진행 노트

## 학습자 프로필
- Unity 기본: 익숙함 (MonoBehaviour, Update, GameObject)
- ECS/DOTS: 입문 중
- C# 문법: 다소 어렵게 느껴짐
- 목표: ECS 기초 + 락스텝 네트워크 원리 이해

## 진행 방식 (중요 — Claude Code도 이 방식을 따를 것)
- **개념 설명 위주로 진행한다.** 코드를 던지기 전에 "왜 이렇게 만드는가"를 먼저 설명한다.
- **코드는 학습자가 직접 타이핑한다.** Claude/Claude Code는 설명 + 코드 제시만 하고, 파일을 직접 만들거나 수정하지 않는다.
  - 예외: 학습자가 명시적으로 "수정해줘" / "파일 만들어줘"라고 요청한 경우에만 직접 작업.
  - (Engineering_Harness_v1_1.md 0-1장 "CLI 협업 규칙"과 동일한 기준)
- **실제 프로젝트 컨벤션을 그대로 따른다.** 학습용 단순 예제(Authoring/Baker 등 범용 DOTS 튜토리얼 방식)가 아니라, 이 프로젝트가 실제로 채택한 방식(런타임 스폰 팩토리, SimID 기반 참조, BCSingleton 등)으로 처음부터 배운다.
- **새 개념이 등장하면 그 자리에서 "왜 이게 필요한가"를 Harness/Develop_Guide의 HARD RULE과 연결해서 설명한다.** (예: SimID는 왜 Entity 핸들 대신 쓰는가 → Harness 2-1 HARD RULE)
- 한 번에 파일 하나, 개념 하나씩만 진행. 학습자가 "다음 단계로" 라고 말하면 진행.

## 참조 문서 (항상 함께 사용)
- `Engineering_Harness_v1_1.md` — 아키텍처 헌법, HARD RULE (충돌 시 최우선)
- `Develop_Guide.md` — STEP별 스펙 (원본 가이드, 일부는 실제 구현과 네이밍이 다름)
- `PROGRESS.md` — **실제 코드 상태의 단일 기준.** 가이드와 실제 구현이 다른 부분은 이 문서 기준.

> 주의: Develop_Guide의 코드 예시(`CommandBuffer`→실제는 `SimCommandQueue`, `SimIDLookup`→실제는 `SimIdRegistry`, static class→실제는 ECS 싱글톤+`BCSingleton<T>` 등)는 설계 의도 참고용이고, 실제 네이밍/구조는 PROGRESS.md를 따른다.

> **프로젝트 위치 (중요 — 혼동 금지)**: 학습 코드는 주 작업 폴더 `C:\Workspace\4XSubCulture\Assets\Scripts\Game\`에 있다(이 LEARNING.md도 여기). `C:\Workspace\Unity_4SProject\4SProject`는 STEP 1~4가 **완성된 참조 프로젝트**(읽기 교재용)이며 학습 코드가 아니다. Harness/Develop_Guide/PROGRESS 문서는 참조 프로젝트의 `Harness/` 폴더에 있다. → "다음 단계 코드"는 참조 프로젝트를 보고 설명하되, 작성·확인은 항상 학습 프로젝트(`4XSubCulture`)에서 한다.

---

## 학습 로드맵 (개념 단위 — 엔진 무관)
- [x] 1단계: ECS 개념 (Entity / Component / System 이란?)
- [x] 2단계: Unity DOTS 실습 — 틱 루프·SimID·BCSingleton·SystemGroup 수동 등록까지 개념 습득(코드는 Unity로 작성). **여기서 아키텍처 전환 결정 ↓**
- [ ] 3단계: 결정적(Deterministic) 시뮬레이션이란? → **고정소수점**으로 C++ 코어에서 구현
- [ ] 4단계: 락스텝 네트워크 원리
- [ ] 5단계: ECS + 락스텝 합치기

> 위 로드맵은 *개념* 기준이라 그대로 유효. 단 **구현 수단**이 Unity DOTS → **엔진 독립 C++ 코어**로 바뀌었다(아래).

---

## 🔀 아키텍처 전환 (2026-06-29) — Unity DOTS → 엔진 독립 C++ 코어

### 결정
Sim 코어를 **Unity DOTS 안**이 아니라 **Unity 밖 독립 C++ DLL**로 만든다. Unity/Unreal은 그 DLL을 부르는 "뷰+입력 호스트"일 뿐.

### 이유
- **언리얼로 교체될 여지**(프로토타이핑). Unity DOTS/Burst 코드는 언리얼로 0% 이식 — 갈아타면 Sim을 통째로 재작성해야 함.
- Sim 코어를 엔진 독립 모듈로 빼면 Unity·Unreal이 **같은 DLL**을 공유 → 엔진 교체에 강함. (처음에 상상했던 "C++ 시뮬레이터 import" 구조)

### 두 개의 독립 축
- **결정성을 어떻게?** → 경계를 넘으므로 단일 Burst 바이너리(float) 보장이 사라짐 → **고정소수점**으로 간다.
- **빠른 코드 어디에?** → 엔진 밖 **C++ DLL** (Unity·Unreal 둘 다 C ABI로 호출).
- ※ "C++ = 고정소수점"이 아님. C++은 고정소수점/재사용/엔진독립을 담는 그릇일 뿐, 둘은 별개 축.

### 유지 vs 바뀜
- **유지(개념·설계)**: Harness 분리 원칙 — 두 시계, 명령(Command)이 유일한 입구, 스냅샷 복사본, SimID, 체크섬. 이게 **그대로 C API 경계**가 됨.
- **바뀜(구현)**: `ISystem`/`SimRootGroup`/`NativeCollections`/`Burst` → C++ + EnTT(or 단순 배열) + 고정소수점.
- Unity DOTS 학습(1·2단계)은 **폐기 아님** — 개념이 그대로 C++ 코어로 전이.

### 프로젝트 위치 (C++ 코어)
- VS 솔루션: `C:\Workspace\4XSubCulture\SimCore\` (Empty Project → 구성형식 동적 라이브러리(.dll), x64)
- 소스: `SimCore\sim_core.h`, `SimCore\sim_core.cpp`
- 빌드 산출: `SimCore\build\bin\x64\Debug\SimCore.dll`
- Unity 로드용: `Assets\Plugins\SimCore.dll`로 복사 (P/Invoke 호출)
- ⚠️ 에디터가 DLL을 잠금 → **에디터 닫고 → 빌드 → 복사 → 에디터 열기** 루틴 필수.

---

## C++ 코어 로드맵 + 진행

| # | 단계 | Unity 때 대응 | 상태 |
|---|------|------------|------|
| 1 | DLL 골격 + C ABI 경계 (`extern "C"`, 42) | [Heartbeat] 사슬 | ✅ |
| 2 | Sim 핸들(opaque) + 정수 틱 | SimClock + 틱 루프 | ✅ |
| 3 | **고정소수점 타입** — 결정성 토대 | (신규) | ✅ |
| 4 | 엔티티 데이터 + SimID 레지스트리 | 스폰 + SimIdRegistry | ✅ |
| 5 | 명령 큐 + 소비 (`sim_enqueue_command`) | 명령 파이프라인 | ⬜ ← 다음 |
| 6 | 체크섬 + 스냅샷 | ChecksumSystem + renderSnapshot | ⬜ |
| 7 | Unity 호스트 정리(DLL 호출) + 나중에 Unreal | SimTickDriver | ⬜ |

### 완료 상세
- **1 ✅** Empty Project→DLL. `extern "C" __declspec(dllexport)`로 맹글링 끈 C ABI. Unity `[DllImport("SimCore")]` P/Invoke로 `sim_hello()`=42 확인. (이름 맹글링 끄는 게 엔진 무관 호출의 핵심)
- **2 ✅** 불투명 핸들: C++ `struct Sim{uint64_t currentTick;}`를 `Sim*`로만 노출, C#은 `IntPtr` 보관. `sim_create`(new)→`sim_tick`(++)→`sim_current_tick`(읽기)→`sim_destroy`(delete). C# 호스트가 accumulator로 코어 구동 → `[SimCore] tick=1,2,3…`. 시계가 엔진 밖 C++로 이동.
- **3 ✅** 고정소수점 `Fixed`(int64 + 16비트 소수, `fixed.h`). `+`/`-`는 정수 가감, `*`는 `>>16`, `/`는 `<<16`. 정수 연산이라 컴파일러·플랫폼 무관 비트 동일 = 결정성. `posX += velX`(0.25/틱) 누적 확인. 함정: 0.1 부정확(양자화 1/65536), 큰 곱셈 오버플로(나중 128비트), 음수 ToInt=floor.
- **4 ✅** 엔티티 컬렉션: `Sim`에 `std::vector<Unit>` + `nextId` + `unordered_map<simId,index>`(조회 전용). `sim_spawn`(결정적 SimID 부여+레지스트리 등록), `sim_tick`이 **인덱스 순회**로 전 유닛 이동(map 순회 금지). 게터(`sim_unit_count`/`sim_get_unit_*`)+레지스트리 lookup(`sim_get_posx_raw_by_id`). 유닛 3개 결정적 이동 확인. 함정: despawn 넣으면 인덱스 밀려 map 깨짐 → swap-remove+갱신으로.

### 네이밍 규약 (확정)
- **내보내는 C ABI 심볼 = snake_case + `sim_` 접두사**(`sim_create` 등). C 라이브러리 관례 + "네이티브 경계" 시각 신호. C#/엔진 코드는 PascalCase. 둘이 달라야 *네이티브 호출 vs 관리 호출*이 이름만으로 구분됨.

### 다음: 5단계 — 명령 큐 + 소비
- 외부 입력(이동·스킬 등)을 `SimCommand`로 큐에 넣고, 틱 소비 시점에 **SimID로 대상 유닛을 찾아** 적용. 락스텝의 유일한 Sim 입구. `sim_enqueue_command` + 틱 정렬·소비. 레지스트리 lookup(`*_by_id`)이 여기서 쓰임. (Harness 5 명령 파이프라인)

---

## 1단계 완료: ECS 개념

### 핵심 요약
| 개념 | 역할 |
|------|------|
| Entity | 고유 ID만 있는 껍데기 (주민등록번호). 실제로는 `{int Index, int Version}` |
| Component | 순수 데이터 (struct, 로직 없음) |
| System | 로직 담당, Component를 읽고 처리 |

- MonoBehaviour와 차이: 데이터와 로직이 분리됨.
- Component는 메모리에 연속 배치되어 CPU 캐시 효율이 높음 — class(참조 타입)는 힙에 흩어지지만 struct(값 타입)는 한 자리에 나란히 깔림.
- ECS Component(`IComponentData` 구현체)는 **무조건 struct**여야 함 (Unity 강제 규칙) + Burst가 managed 객체를 다룰 수 없기 때문(Harness HARD RULE: "Sim 코드는 전부 Burst 컴파일", "Sim 코드에서 managed 객체 참조 금지").

---

## 2단계 (완료·전환됨): 실제 프로젝트 방식 DOTS 실습 — Unity로 개념 습득

> ⚠️ 이 절은 **Unity DOTS로 개념을 익힌 기록**이다. 이후 아키텍처가 C++ 코어로 전환되어, 아래 "다음에 배울 것"의 Unity 구현(SimRootGroup 구동·Query 등)은 **위 "C++ 코어 로드맵"으로 대체**됨. 개념은 유효, 구현 수단만 바뀜.

### 설치 완료
- `com.unity.entities` 패키지 설치됨

### 방향 결정
- Authoring/Baker(씬 GameObject → 베이킹) 방식 **사용 안 함**.
- 실제 프로젝트는 베이킹 대신 **런타임 스폰 팩토리** 채택 (PROGRESS.md STEP4 "스폰 경로" 참고) — 수동 World 부트스트랩, 런타임 `SimIdRegistry`와 정합되기 때문.
- 그래서 학습도 "Component 설계 → SimID/Registry 개념 → World/EntityManager 런타임 스폰 → System" 순서로 실제 구조를 따라간다.

### 작성 완료한 파일 (개념 설명 끝, 직접 타이핑 완료 또는 진행중)

**`Core/Data/BaseStats.cs`**
```csharp
namespace Game.Core.Data
{
    public struct BaseStats
    {
        public float maxHP;
        public float attackPower;
        public float defense;
        public float speed;
        public float evasion;
        public float skillCooldownMult;
    }
}
```
- 일반 데이터 struct (ECS Component 아님, `IComponentData` 안 붙음). 다른 Component 안에 끼워 넣는 용도.
- namespace는 문법적으로 필수는 아니지만, 프로젝트 영역(`Game.Core`, `Game.Core.Data`, `Game.NetCode`, `Game.Battle` 등) 구분을 위해 컨벤션상 사용.

**`Core/Data/SimID.cs`**
```csharp
using Unity.Entities;

namespace Game.Core.Data
{
    public struct SimIDComponent : IComponentData
    {
        public ulong Value;
    }
}
```
- 진짜 ECS Component (`IComponentData` 붙음). 모든 게임 유닛 엔티티에 부착해서 "이 엔티티의 SimID는 몇 번"을 표시.
- **왜 Entity 핸들을 직접 안 쓰는가**: `Entity{Index, Version}`는 세이브/로드, 멀티플레이 클라이언트 간 안정적이지 않음. 결정적으로 순차 부여되는 `ulong` SimID를 따로 두고, 명령·체크섬·세이브는 전부 SimID로만 참조한다 (Harness HARD RULE 2-1: "SimID로 엔티티 참조... DOTS Entity 핸들은 세이브/클라 간 불안정 — 직렬화 금지").

**`Core/SimIdRegistry.cs`** (struct 본체까지 작성, World 등록은 다음 단계)
```csharp
using Unity.Collections;
using Unity.Entities;

namespace Game.Core
{
    public struct SimIdRegistry : IComponentData
    {
        public NativeHashMap<ulong, Entity> map;
        public ulong nextId;

        public ulong Register(Entity entity)
        {
            ulong id = nextId;
            nextId++;
            map[id] = entity;
            return id;
        }

        public bool TryGetEntity(ulong simId, out Entity entity)
        {
            return map.TryGetValue(simId, out entity);
        }

        public void UnRegister(ulong simId)
        {
            map.Remove(simId);
        }
    }
}
```
- SimID ↔ Entity 변환표. **조회(lookup)만 허용, 순회(foreach) 금지** (Harness HARD RULE: `NativeHashMap` 순회로 Sim 상태 변경 금지 — 조회는 허용).
- `NativeHashMap`을 쓰는 이유: managed `Dictionary`는 Burst가 못 다룸. `NativeHashMap`은 unmanaged 메모리를 직접 할당/해제하는 컬렉션 (`Allocator.Persistent` 사용 시 직접 `Dispose` 필요).
- 이 컴포넌트를 World에 **딱 하나만** 존재하는 "ECS 싱글톤"으로 만들 것 — 다음 단계에서 다룸.
- struct인데 메서드를 가짐: "필드는 데이터만"이라는 규칙이고, 그 데이터를 다루는 헬퍼 메서드는 struct 안에 둬도 무방 (Burst 입장에서도 인라인되는 일반 코드).

### 완료한 것 (1~4번, 끝까지 동작 확인됨)

**1번 ✅ ECS 싱글톤 + `BCSingleton<T>`**
- `SimIdRegistry`에 `, IBCSingleton` 마커 추가 (싱글톤 자격 표식).
- `Core/BCSingleton.cs` 작성 — `IBCSingleton`(빈 마커 인터페이스) + `BCSingleton<T>` static 헬퍼(`Ensure`/`TryGet`/`Get`/`Set`).
  - `Ensure` = "없으면 만들고 있으면 가져온다"(EntityQuery로 중복 검사 → World에 딱 하나 보장).
  - 핵심 함정 ①: 싱글톤 **값 필드**(예 `nextId`)는 `Get`이 복사본을 줌 → 바꾸면 `Set`/`SetSingleton`으로 write-back 필수. 단 `NativeHashMap.map` *내용*은 포인터라 자동 반영.
  - 핵심 함정 ②: `map` 소유권은 **호출자(SimTickDriver)** — `Allocator.Persistent`로 만들고 `OnDestroy`에서 직접 `Dispose`. BCSingleton은 Dispose 안 해줌(컴포넌트마다 네이티브 컬렉션 유무가 달라서).
- `mc_mapInitCapacity=1024` 이유: 동시 유닛 수 추정 + 재할당 회피, 2의 거듭제곱(빠른 해시 인덱싱). 상한 아님. **결정성과 무관**(조회 전용이라 순회 순서가 로직에 안 들어감).

**2번 ✅ World / `EntityManager.CreateEntity`**
- `Core/BCMonoSingleton.cs` 작성 — MonoBehaviour 싱글톤 베이스(CRTP `where T : BCMonoSingleton<T>`, `Awake`에서 `Instance` 등록·중복 자기파괴, `OnDestroy`에서 정리).
- `Core/SimClock.cs` 작성 — 순수 값 싱글톤(`currentTick`/`isPaused`/`gameSpeedX10`). 네이티브 컬렉션 없음 → Dispose 불필요(`SimIdRegistry`와의 대조 예제).
- `Core/SimTickDriver.cs` **최소 버전** 작성 — `World.DefaultGameObjectInjectionWorld` 잡고 `BCSingleton<T>.Ensure`로 SimClock·SimIdRegistry 생성, `OnDestroy`에서 `map.Dispose()`.
  - 개선분 2곳 추가(원본엔 없음): `Initialize`의 `m_world==null` 가드, `OnDestroy`의 `m_world.IsCreated` 가드(World가 먼저 파괴되는 teardown 순서 방어).
  - `CreateEntity(typeof(T))`=빈 엔티티 생성 → `SetComponentData`=초기값 주입, 2단계로 나뉨.

**3번 ✅ System (`ISystem`/`OnCreate`/`OnUpdate`)**
- `Core/HeartbeatSystem.cs` 작성 — **학습용 임시 시스템**(원본엔 없음, 나중에 삭제 가능). SimClock의 `currentTick`을 매 틱 읽어 로그.
- 배운 것: `partial struct : ISystem`(struct·partial 이유=Burst+소스제너레이터), `OnCreate`=System판 `Start`(딱 1번), `OnUpdate`=System판 `Update`(매 틱), `RequireForUpdate<T>`=싱글톤 준비될 때까지 OnUpdate 막는 자물쇠.
- 실전 에러 체험: `RequireForUpdate` 없이 돌리면 `GetSingleton... there are none` 예외 → OnCreate에 자물쇠 달아 해결.
- `[BurstCompile]` 붙이면 `Debug.Log`(managed) 금지됨을 직접 확인(HARD RULE "Sim 코드는 Burst, managed 금지").
- **읽기 교재(원본, 타이핑 X)**: `ChecksumSystem`(SystemAPI.Query<RefRO,RefRO>, GetSingletonRW().ValueRW), `CommanderAutoSkillSystem`(Query<RefRW>.WithEntityAccess, `.ValueRW`로 참조 직접 수정 → write-back 불필요).

**4번 ✅ 틱 루프 (정수 틱 카운팅)**
- `SimTickDriver.Update`에 accumulator 기반 정수 틱 루프 추가:
  `m_accumulator += Time.deltaTime*1000` → `intervalMs = m_baseTickIntervalMs / (clock.gameSpeedX10/10f)` → 통이 차면(`m_accumulator >= intervalMs`) `clock.currentTick += 1` 후 `SetComponentData` write-back, `m_accumulator -= intervalMs`.
- `m_maxTicksPerFrame`(=5)로 한 프레임당 최대 틱 수 상한 — spiral of death 방지(Harness 3-2).
- 배운 것: 벽시계(`Time.deltaTime`)는 **accumulator 계산에만** 쓰고 Sim 상태(`currentTick`)는 정수로만 증가 (HARD RULE 2-2 "Sim에서 Time.* 금지" / 3-1 두 시계). `gameSpeedX10`로 속도 조절 시 바뀌는 건 틱당 ms뿐, 틱 의미는 불변(HARD RULE 1).
- 값 싱글톤이라 `currentTick++` 뒤 `SetComponentData` write-back 필수(함정 ①).
- **아직 없는 것**: 이 루프는 **시계만 올림**. `SimRootGroup.Update()`로 시스템 그룹을 구동하지는 않음. `CanAdvanceTick` 락스텝 관문/transport/명령 소비도 아직 없음.

### 현재 상태
- 씬에 `SimTickDriver` 붙인 GameObject → Play하면 `[Heartbeat] tick = 0,1,2,3…`으로 증가(약 100ms마다 +1, 1x 기준). **틱 루프(정수 카운팅)까지 사슬 연결 확인됨.**
- 단, `HeartBeatSystem`은 아직 **기본 World 자동 등록**으로 돎(드라이버가 직접 구동하는 게 아님). 구조: 드라이버 `Update`가 `currentTick`을 올리고, 자동 등록된 HeartBeat가 매 프레임 그 값을 읽어 로그.

### 다음에 배울 것 (여기부터 이어서)
1. **틱 루프 — 그룹 구동 부분** (정수 틱 카운팅은 위 4번에서 완료). 남은 것: `SimRootGroup.Update()`로 매 틱 시스템 묶음을 구동 + `CanAdvanceTick` 락스텝 관문 연결. 지금은 시계만 올라가고 시스템 그룹은 안 돎 → 이걸 붙이면서 `HeartBeatSystem`을 자동 등록에서 수동 그룹으로 이전.
2. **시스템 수동 등록 + 그룹 순서** — `SimSystemGroups`(`[DisableAutoCreation]`)와 `AddGroup`/`AddSystem`/`SetPausable` 배선. 왜 자동 등록 대신 드라이버가 순서를 못 박는가(결정성).
3. **`SystemAPI.Query` + `RefRW`/`RefRO`** 본격 — 유닛 스폰(STEP4 FleetFactory) 후 여러 엔티티 훑기.
4. 이후 결정적 시뮬레이션 → 락스텝(로드맵 3~5단계).

> 미해결 메모: `HeartbeatSystem`은 현재 자동 등록(기본 World 시뮬그룹)으로 돎. 1·2번 진행하며 SimRootGroup 수동 등록 체계로 옮길지/지울지 결정.

---

## 다음 세션(또는 Claude Code) 시작 멘트 예시
> "LEARNING.md 읽고, 'C++ 코어 로드맵' 3단계(고정소수점)부터 같은 방식으로 이어서 진행해줘. 코드는 내가 직접 타이핑할 거고, 작업은 C++ 코어(`4XSubCulture\SimCore`)에서 한다. 설명하고 제시만 해줘."
>
> (구버전 멘트: 'Unity DOTS 다음에 배울 것 1번 틱 루프…' — 아키텍처 전환으로 폐기.)
