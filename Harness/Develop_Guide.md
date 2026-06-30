# DEVELOP GUIDE

**4X Subculture Space Strategy — [프로젝트 가칭]**

*Unity 6 LTS DOTS · 결정적 락스텝 Co-op · PC 전용 (x64)*

**v1.0 — Opus CLI 작업용 전체 개발 가이드**

> **본 문서는 Engineering_Harness_v1_1.md와 함께 사용한다. 아키텍처 충돌 시 Harness가 우선한다.**
> **각 STEP 작업 시 Harness + 해당 STEP 섹션을 함께 컨텍스트로 제공한다.**

---

## 문서 구성

| STEP | 시스템 | 분류 | 의존 STEP |
|------|--------|------|-----------|
| STEP 1 | 프로젝트 설정 + 코어 아키텍처 | 기반 | — |
| STEP 2 | 틱 드라이버 + 명령 파이프라인 | 기반 | 1 |
| STEP 3 | 체크섬 + 리플레이 | 기반 | 2 |
| STEP 4 | 전투 — 편제 구조 (함선·모선·소대) | 전투 | 2 |
| STEP 5 | 전투 — 지휘형 + 스킬 시스템 | 전투 | 4 |
| STEP 6 | 전투 — 카메라 + 전투 HUD | 전투 | 4 |
| STEP 7 | 전투 — BattleManager + 결과 처리 | 전투 | 5, 6 |
| STEP 8 | 내정 — 행성 + 건물 + 자원 | 내정 | 2 |
| STEP 9 | 내정 — 시민 카드 + 지지도 | 내정 | 8 |
| STEP 10 | 캐릭터 — 데이터 + 성장 시스템 | 캐릭터 | 2 |
| STEP 11 | 캐릭터 — 호감도 + 스토리 트리거 | 캐릭터 | 10 |
| STEP 12 | 미니 캠페인 통합 씬 | 통합 | 7, 9, 11 |

---

## STEP 1 — 프로젝트 설정 + 코어 아키텍처

### 목표
Unity 6 LTS 프로젝트의 폴더 구조, 공통 데이터 구조, 전역 이벤트 버스를 설정한다.
이후 모든 STEP이 이 구조 위에서 작동한다.

### 폴더 구조

```
Assets/
└─ _Game/
    ├─ Scripts/
    │   ├─ Battle/          전투 시스템 전체
    │   ├─ Internal/        내정 시스템 전체
    │   ├─ Character/       캐릭터·호감도·스토리 시스템
    │   ├─ Core/            GameManager, SimTickDriver, EventBus 등
    │   ├─ Netcode/         명령 파이프라인, 체크섬, 락스텝
    │   └─ UI/              HUD, 패널, 팝업 전체
    ├─ Data/
    │   ├─ Characters/      CharacterData ScriptableObject
    │   ├─ Ships/           ShipData ScriptableObject
    │   ├─ Buildings/       BuildingData ScriptableObject
    │   └─ Skills/          SkillData ScriptableObject
    ├─ Prefabs/
    │   ├─ Battle/          함선·함재기·이펙트 프리팹
    │   └─ UI/              UI 프리팹
    └─ _Placeholder/        임시 에셋 (추후 교체 대상 명시)
```

### 공통 데이터 구조

#### BaseStats (공통 스탯 — unmanaged struct)
```csharp
// 위치: Assets/_Game/Scripts/Core/Data/BaseStats.cs
public struct BaseStats
{
    public float maxHP;
    public float attackPower;
    public float defense;
    public float speed;
    public float evasion;             // 0~1
    public float skillCooldownMult;   // 1.0 = 기본
}
```

#### CharacterBonus (캐릭터 배치 보정값 — unmanaged struct)
```csharp
// 위치: Assets/_Game/Scripts/Core/Data/CharacterBonus.cs
public enum SlotType : byte { Leader, Sub1, Sub2 }

public struct CharacterBonus
{
    public float    bonusHP;
    public float    bonusAttack;
    public float    bonusDefense;
    public float    bonusSpeed;
    public float    bonusEvasion;
    public float    bonusSkillCooldown;
    public SlotType slotType;
}
```

#### SimID (엔티티 식별자)
```csharp
// 위치: Assets/_Game/Scripts/Core/Data/SimID.cs
// HARD RULE: 명령·체크섬·세이브에서 Entity 핸들 대신 반드시 SimID 사용
public struct SimIDComponent : IComponentData
{
    public ulong Value;   // 결정적 순차 부여, 절대 재사용 없음
}
```

#### ResourceBundle (자원 묶음 — unmanaged struct)
```csharp
// 위치: Assets/_Game/Scripts/Core/Data/ResourceBundle.cs
public struct ResourceBundle
{
    public int energy;
    public int minerals;
    public int food;
    public int research;
    public int credits;
}
```

### 전역 이벤트 버스

시스템 간 직접 참조 방지. Sim→프레젠테이션 단방향 원칙.

```csharp
// 위치: Assets/_Game/Scripts/Core/EventBus.cs
// 주요 이벤트 목록
public static class GameEvents
{
    // 전투
    public static event Action<ulong> OnShipDestroyed;       // simId
    public static event Action<ulong> OnCarrierDestroyed;    // simId
    public static event Action<ulong> OnCharacterDown;       // simId (격추)

    // 내정
    public static event Action<ulong, BuildingData> OnBuildingCompleted;
    public static event Action<ResourceBundle>      OnResourceUpdated;

    // 캐릭터
    public static event Action<ulong, int>          OnAffinityChanged;   // simId, newValue
    public static event Action<StoryEventData>      OnStoryTriggered;

    // 시스템
    public static event Action<ulong>  OnBattleStart;   // battleSimId
    public static event Action<bool>   OnBattleEnd;     // playerWin
    public static event Action<bool>   OnPauseChanged;  // isPaused
}
```

### ScriptableObject 베이스 구조

모든 수치는 하드코딩 금지. Inspector에서 편집 가능해야 함.

```csharp
// 위치: Assets/_Game/Scripts/Core/Data/GameDataBase.cs
// 모든 게임 데이터 SO의 공통 베이스
public abstract class GameDataBase : ScriptableObject
{
    [Header("식별")]
    public string dataId;    // 고유 문자열 ID (세이브·참조용)
    public string displayName;
}
```

---

## STEP 2 — 틱 드라이버 + 명령 파이프라인

### 목표
락스텝의 핵심. 싱글/멀티 단일 경로로 동작하는 틱 드라이버와
모든 Sim 상태 변경의 유일한 입구인 명령 파이프라인을 구현한다.

### ITransport — 전송 계층 인터페이스

싱글(null)/멀티 분기를 전송 계층 구현체 하나로 격리.

```csharp
// 위치: Assets/_Game/Scripts/Netcode/ITransport.cs
public interface ITransport
{
    // 명령을 호스트에 전송 (싱글: 즉시 자기에게 echo)
    void SendCommand(SimCommand cmd);

    // 이번 틱 실행 가능 여부 (싱글: 항상 true)
    bool CanAdvanceTick(ulong tick);

    // 틱 N의 전 클라 명령 묶음 수신 (싱글: 로컬 버퍼에서)
    NativeList<SimCommand> GetCommandsForTick(ulong tick, Allocator allocator);
}

// 싱글플레이 구현체
public class NullTransport : ITransport
{
    private NativeList<SimCommand> _localBuffer;

    public void SendCommand(SimCommand cmd)
    {
        // 즉시 자기에게 echo — 네트워크 없음
        _localBuffer.Add(cmd);
    }

    public bool CanAdvanceTick(ulong tick) => true;   // 항상 진행

    public NativeList<SimCommand> GetCommandsForTick(ulong tick, Allocator allocator)
    {
        // 해당 틱 스탬프 명령만 필터링해서 반환
        var result = new NativeList<SimCommand>(allocator);
        for (int i = _localBuffer.Length - 1; i >= 0; i--)
        {
            if (_localBuffer[i].executionTick == tick)
            {
                result.Add(_localBuffer[i]);
                _localBuffer.RemoveAt(i);
            }
        }
        return result;
    }
}
```

### SimCommand 구조

```csharp
// 위치: Assets/_Game/Scripts/Netcode/SimCommand.cs
// HARD RULE: unmanaged + blittable 유지. string·class 참조 금지.
public enum CommandType : ushort
{
    // 전투
    MoveFleet       = 100,
    AttackTarget    = 101,
    LaunchSquadron  = 102,
    RecallSquadron  = 103,
    ActivateSkill   = 104,

    // 내정
    BuildStructure  = 200,
    CancelBuild     = 201,
    SetCitizenCard  = 202,
    ProduceUnit     = 203,

    // 메타 (HARD RULE: 일시정지·속도변경도 Command 경유)
    Pause           = 900,
    Resume          = 901,
    SetGameSpeed    = 902,
    SaveGame        = 903,
}

[StructLayout(LayoutKind.Sequential)]
public struct SimCommand : IComparable<SimCommand>
{
    public ulong        executionTick;   // 이 틱에 실행
    public byte         playerId;        // 0~3, AI = 100+
    public ushort       sequence;        // 같은 틱 내 발행 순서
    public CommandType  type;
    public CommandPayload payload;

    // HARD RULE: 정렬 키 (executionTick, playerId, sequence) — 전 클라 동일 순서
    public int CompareTo(SimCommand other)
    {
        int c = executionTick.CompareTo(other.executionTick);
        if (c != 0) return c;
        c = playerId.CompareTo(other.playerId);
        if (c != 0) return c;
        return sequence.CompareTo(other.sequence);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
public struct CommandPayload
{
    [FieldOffset(0)] public ulong targetSimId;     // 대상 SimID
    [FieldOffset(8)] public ulong secondarySimId;  // 보조 대상
    [FieldOffset(16)] public float3 position;      // 이동 목표
    [FieldOffset(28)] public byte byteParam;       // 범용 byte 파라미터
}
```

### SimTickDriver

```csharp
// 위치: Assets/_Game/Scripts/Core/SimTickDriver.cs
// HARD RULE: Sim 시계는 정수 논리 틱. Time.deltaTime 사용 금지.
public class SimTickDriver : MonoBehaviour
{
    [Header("설정 (ScriptableObject 연동 예정)")]
    public int   maxTicksPerFrame = 5;          // spiral of death 방지
    public float baseTickIntervalMs = 100f;     // 1x 기준

    public ulong CurrentTick   { get; private set; }
    public bool  IsPaused      { get; private set; }
    public float GameSpeed     { get; private set; } = 1f;

    private float _accumulator;
    private float _tickIntervalMs;
    private ITransport _transport;

    // 의존성 주입 — 싱글: NullTransport, 멀티: NetworkTransport
    public void Initialize(ITransport transport)
    {
        _transport = transport;
        _tickIntervalMs = baseTickIntervalMs / GameSpeed;
    }

    private void Update()
    {
        if (IsPaused) return;

        // HARD RULE: 벽시계는 프레젠테이션 전용이지만
        // accumulator 계산은 틱 드라이버 책임 (Sim 상태 변경 안 함)
        _accumulator += Time.deltaTime * 1000f;

        int ticksThisFrame = 0;
        while (_accumulator >= _tickIntervalMs && ticksThisFrame < maxTicksPerFrame)
        {
            if (!_transport.CanAdvanceTick(CurrentTick + 1)) break;

            CurrentTick++;
            RunSimTick(CurrentTick);
            _accumulator -= _tickIntervalMs;
            ticksThisFrame++;
        }
    }

    private void RunSimTick(ulong tick)
    {
        // 1. 이번 틱 명령 수신 및 CommandBuffer에 적재
        using var cmds = _transport.GetCommandsForTick(tick, Allocator.Temp);
        CommandBuffer.EnqueueBatch(cmds);

        // 2. SystemGroup 일괄 실행 (World.Update — 순서는 Harness 4장 고정)
        SimWorld.Instance.Update();
    }

    // HARD RULE: 일시정지·속도변경은 Command 경유
    public void RequestPause()
    {
        var cmd = new SimCommand
        {
            executionTick = CurrentTick + 1,
            playerId      = LocalPlayerId,
            type          = CommandType.Pause,
        };
        _transport.SendCommand(cmd);
    }

    public void RequestSetSpeed(float speed)
    {
        var cmd = new SimCommand
        {
            executionTick    = CurrentTick + 1,
            playerId         = LocalPlayerId,
            type             = CommandType.SetGameSpeed,
            payload          = new CommandPayload { byteParam = (byte)(speed * 10) },
        };
        _transport.SendCommand(cmd);
    }

    // Command 소비 시 호출됨 (CommandConsumeGroup에서)
    public void ApplyPause(bool pause)     { IsPaused = pause; }
    public void ApplySpeed(float speed)
    {
        GameSpeed       = speed;
        _tickIntervalMs = baseTickIntervalMs / GameSpeed;
    }

    private byte LocalPlayerId => 0; // TODO: 멀티 시 플레이어 ID 주입
}
```

### CommandBuffer (전역 명령 큐)

```csharp
// 위치: Assets/_Game/Scripts/Netcode/CommandBuffer.cs
// HARD RULE: Sim 상태 변경의 유일한 입구
public static class CommandBuffer
{
    private static NativeList<SimCommand> _pending;

    public static void Initialize()
    {
        _pending = new NativeList<SimCommand>(256, Allocator.Persistent);
    }

    public static void EnqueueBatch(NativeList<SimCommand> cmds)
    {
        _pending.AddRange(cmds);
        // HARD RULE: 정렬 키 (executionTick, playerId, sequence) 정렬
        _pending.Sort();
    }

    // CommandConsumeGroup이 틱 시작 시 호출
    public static NativeArray<SimCommand> ConsumeTick(ulong tick, Allocator allocator)
    {
        var result = new NativeList<SimCommand>(allocator);
        for (int i = _pending.Length - 1; i >= 0; i--)
        {
            if (_pending[i].executionTick == tick)
            {
                result.Add(_pending[i]);
                _pending.RemoveAt(i);
            }
        }
        return result.AsArray();
    }

    public static void Dispose() { _pending.Dispose(); }
}
```

### CommandConsumeSystem

```csharp
// 위치: Assets/_Game/Scripts/Netcode/CommandConsumeSystem.cs
// HARD RULE: SystemGroup 실행 순서 1번. [UpdateInGroup] 명시 필수.
[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
[UpdateInGroup(typeof(CommandConsumeGroup))]
public partial struct CommandConsumeSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var tick = SystemAPI.GetSingleton<SimTickComponent>().CurrentTick;
        using var cmds = CommandBuffer.ConsumeTick(tick, Allocator.Temp);

        // HARD RULE: 소비 시점 유효성 검사 (발행 시점 검사 금지)
        foreach (var cmd in cmds)
        {
            if (!ValidateCommand(cmd, ref state))
            {
                UnityEngine.Debug.Log($"[CMD DROP] tick={tick} type={cmd.type} player={cmd.playerId}");
                continue;   // HARD RULE: 무효 명령은 조용히 드랍
            }
            DispatchCommand(cmd, ref state);
        }
    }

    private bool ValidateCommand(SimCommand cmd, ref SystemState state)
    {
        return cmd.type switch
        {
            CommandType.MoveFleet    => EntityExists(cmd.payload.targetSimId, ref state),
            CommandType.BuildStructure => HasEnoughResources(cmd, ref state),
            _                        => true,
        };
    }

    private void DispatchCommand(SimCommand cmd, ref SystemState state)
    {
        // 각 명령 타입별 처리 — 해당 STEP에서 구현
        switch (cmd.type)
        {
            case CommandType.Pause:        ApplyPause(true);  break;
            case CommandType.Resume:       ApplyPause(false); break;
            case CommandType.SetGameSpeed: ApplySpeed(cmd.payload.byteParam / 10f); break;
            // 나머지는 해당 STEP에서 추가
        }
    }

    private bool EntityExists(ulong simId, ref SystemState state) =>
        SimIDLookup.TryGetEntity(simId, out _);

    private bool HasEnoughResources(SimCommand cmd, ref SystemState state) =>
        true; // TODO: STEP 8에서 구현

    private void ApplyPause(bool pause) =>
        SimTickDriver.Instance.ApplyPause(pause);

    private void ApplySpeed(float speed) =>
        SimTickDriver.Instance.ApplySpeed(speed);
}
```

### SimIDLookup (SimID → Entity 매핑)

```csharp
// 위치: Assets/_Game/Scripts/Core/SimIDLookup.cs
// HARD RULE: NativeHashMap 순회 금지. 조회(lookup)만 허용.
public static class SimIDLookup
{
    private static NativeHashMap<ulong, Entity> _map;
    private static ulong _nextId = 1;

    public static void Initialize()
    {
        _map = new NativeHashMap<ulong, Entity>(1024, Allocator.Persistent);
    }

    // 결정적 순차 부여 — 틱 내 생성 순서 고정 필수
    public static ulong Register(Entity entity)
    {
        ulong id = _nextId++;
        _map[id] = entity;
        return id;
    }

    // 조회만 허용
    public static bool TryGetEntity(ulong simId, out Entity entity) =>
        _map.TryGetValue(simId, out entity);

    public static void Unregister(ulong simId) => _map.Remove(simId);

    public static void Dispose() => _map.Dispose();
}
```

---

## STEP 3 — 체크섬 + 리플레이

### 목표
결정성 버그를 조기 발견하는 두 가지 도구를 구현한다.
싱글 개발 중에도 항상 구동해서 멀티 이전에 버그를 잡는다.

### ChecksumSystem

```csharp
// 위치: Assets/_Game/Scripts/Netcode/ChecksumSystem.cs
// HARD RULE: [UpdateInGroup(typeof(ChecksumGroup))] — 매 틱, SystemGroup 순서 9번
[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
[UpdateInGroup(typeof(ChecksumGroup))]
public partial struct ChecksumSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var tick = SystemAPI.GetSingleton<SimTickComponent>().CurrentTick;

        // HARD RULE: EntityID 정렬 후 해시 — 순서 비결정 방지
        using var positions = CollectSortedPositions(ref state, Allocator.Temp);
        using var healths   = CollectSortedHP(ref state, Allocator.Temp);
        using var resources = CollectResources(ref state, Allocator.Temp);

        uint hash = ComputeHash(positions, healths, resources);

        ChecksumLog.Record(tick, hash);

        // 멀티: N틱마다 호스트에 전송 (STEP 네트워크에서 구현)
        if (tick % 10 == 0)
            ChecksumLog.FlushToNetwork(tick);
    }

    [BurstCompile]
    private NativeArray<float3> CollectSortedPositions(ref SystemState state, Allocator allocator)
    {
        // HARD RULE: SimID 기준 정렬 — NativeHashMap 순회 금지, NativeList 사용
        var list = new NativeList<KeyValue<ulong, float3>>(allocator);
        foreach (var (simId, pos) in
            SystemAPI.Query<RefRO<SimIDComponent>, RefRO<LocalTransform>>())
        {
            list.Add(new KeyValue<ulong, float3>(simId.ValueRO.Value, pos.ValueRO.Position));
        }
        list.Sort(new SimIDComparer());

        var result = new NativeArray<float3>(list.Length, allocator);
        for (int i = 0; i < list.Length; i++) result[i] = list[i].Value;
        return result;
    }

    [BurstCompile]
    private uint ComputeHash(
        NativeArray<float3> positions,
        NativeArray<float> healths,
        NativeArray<ResourceBundle> resources)
    {
        // xxHash 누적
        uint hash = 2166136261u;
        foreach (var p in positions)
        {
            hash ^= math.hash(p);
            hash *= 16777619u;
        }
        foreach (var h in healths)
        {
            hash ^= math.hash(h);
            hash *= 16777619u;
        }
        return hash;
    }
}
```

### ReplaySystem (명령 기록·재생)

```csharp
// 위치: Assets/_Game/Scripts/Netcode/ReplaySystem.cs
// 목적: 결정성 검증 도구 + 리플레이 기능
public class ReplaySystem : MonoBehaviour
{
    public bool IsRecording { get; private set; }
    public bool IsReplaying { get; private set; }

    // 틱 → 명령 목록 기록
    private Dictionary<ulong, List<SimCommand>> _replayData = new();

    public void StartRecording() { IsRecording = true; }

    // CommandConsumeGroup 실행 전 호출
    public void RecordTick(ulong tick, IEnumerable<SimCommand> cmds)
    {
        if (!IsRecording) return;
        _replayData[tick] = new List<SimCommand>(cmds);
    }

    // 재생: 기록된 명령을 동일 순서로 재주입
    public IEnumerator Replay()
    {
        IsReplaying = true;
        SimTickDriver.Instance.Reset();

        foreach (var (tick, cmds) in _replayData.OrderBy(kv => kv.Key))
        {
            // 해당 틱까지 진행 후 명령 주입
            yield return new WaitUntil(() =>
                SimTickDriver.Instance.CurrentTick >= tick - 1);

            var nullTransport = (NullTransport)SimTickDriver.Instance.Transport;
            foreach (var cmd in cmds)
                nullTransport.SendCommand(cmd);
        }
        IsReplaying = false;
    }

    // 결정성 검증: 재생 후 체크섬이 원본과 일치하는지 확인
    public bool ValidateDeterminism()
    {
        return ChecksumLog.CompareWithReplay();
    }

    // 저장·불러오기 (JSON — 디버그용)
    public void SaveReplay(string path) =>
        File.WriteAllText(path, JsonUtility.ToJson(new ReplayData(_replayData)));

    public void LoadReplay(string path)
    {
        var data = JsonUtility.FromJson<ReplayData>(File.ReadAllText(path));
        _replayData = data.ToDictionary();
    }
}
```

---

## STEP 4 — 전투 편제 구조

### 목표
함선 편대(Squad) / 모선(타이탄급) / 함재기 소대(Squadron) 3계층 편제를 구현한다.
스탯은 BaseStats + CharacterBonus 합산 구조로 에셋 교체 가능하게 설계한다.

### 전투 편제 3계층

```
모선 (타이탄급 독립 유닛)
  └─ 함재기 소대 (7기: 캐릭터 3슬롯 + 더미 4기)

함선 편대 (동급 6기 = 1 Squad)
  └─ 지휘형 함장 슬롯 (선택)
```

### ShipData (ScriptableObject)

```csharp
// 위치: Assets/_Game/Data/Ships/ShipData.cs
[CreateAssetMenu(menuName = "Game/Ship Data")]
public class ShipData : GameDataBase
{
    [Header("기본 스탯")]
    public BaseStats baseStats;

    [Header("편제")]
    public ShipClass shipClass;   // Frigate, Destroyer, Cruiser, Carrier(모선)
    public int       squadSize;   // 편대 크기 (일반함 = 6, 모선 = 1)

    [Header("모선 전용")]
    public bool      isCarrier;
    public int       squadronCapacity;   // 탑재 가능 소대 수
}

public enum ShipClass : byte
{
    Frigate    = 0,
    Destroyer  = 1,
    Cruiser    = 2,
    Battleship = 3,
    Carrier    = 10,   // 모선 (타이탄급)
}
```

### FleetUnitComponent (함선 편대 ECS 컴포넌트)

```csharp
// 위치: Assets/_Game/Scripts/Battle/Components/FleetUnitComponent.cs
// HARD RULE: IComponentData — unmanaged
public struct FleetUnitComponent : IComponentData
{
    public ulong    shipDataId;        // ShipData 참조 (SimID 방식)
    public int      currentSquadSize;  // 현재 편대 생존 함선 수
    public ulong    commanderSimId;    // 지휘형 함장 SimID (0 = 없음)
    public BaseStats currentStats;     // baseStats + commander 보정 합산
    public byte     teamId;            // 0 = 플레이어, 1 = 적
}
```

### SquadronCarrierComponent (모선 ECS 컴포넌트)

```csharp
// 위치: Assets/_Game/Scripts/Battle/Components/SquadronCarrierComponent.cs
public struct SquadronCarrierComponent : IComponentData
{
    public ulong    carrierDataId;
    public ulong    assignedSquadronSimId;   // 탑재 소대 SimID (0 = 없음)
    public ulong    commanderSimId;          // 모선 함장
    public BaseStats currentStats;
    public bool     isSquadronLaunched;
    public byte     teamId;
}
```

### StrikeSquadronComponent (함재기 소대 ECS 컴포넌트)

```csharp
// 위치: Assets/_Game/Scripts/Battle/Components/StrikeSquadronComponent.cs
public struct StrikeSquadronComponent : IComponentData
{
    public ulong  carrierSimId;      // 모선 SimID — 모선 격파 시 소대 소멸
    public ulong  leaderSimId;       // 대장기 캐릭터 SimID (0 = 더미)
    public ulong  sub1SimId;         // 서브기 1
    public ulong  sub2SimId;         // 서브기 2
    public BaseStats currentStats;   // baseStats(더미 7기 기준) + 캐릭터 보정 합산
    public bool   isActive;
}
```

### StatCalculatorSystem (스탯 합산 시스템)

```csharp
// 위치: Assets/_Game/Scripts/Battle/Systems/StatCalculatorSystem.cs
// 캐릭터 배치·제거 시 트리거. CommandConsumeGroup 직후 실행.
[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
[UpdateInGroup(typeof(CommandConsumeGroup))]
[UpdateAfter(typeof(CommandConsumeSystem))]
public partial struct StatCalculatorSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 함선 편대: baseStats + commander bonus
        foreach (var fleet in
            SystemAPI.Query<RefRW<FleetUnitComponent>>()
                     .WithAll<StatsNeedRecalcTag>())
        {
            var baseStats = GetShipBaseStats(fleet.ValueRO.shipDataId);
            var bonus     = GetCommanderBonus(fleet.ValueRO.commanderSimId);
            fleet.ValueRW.currentStats = ApplyBonus(baseStats, bonus);
        }

        // 함재기 소대: baseStats + 3슬롯 보정
        foreach (var squadron in
            SystemAPI.Query<RefRW<StrikeSquadronComponent>>()
                     .WithAll<StatsNeedRecalcTag>())
        {
            var s   = ref squadron.ValueRW;
            var bs  = GetSquadronBaseStats();   // 더미 7기 기준
            var b0  = GetCharacterBonus(s.leaderSimId, SlotType.Leader);
            var b1  = GetCharacterBonus(s.sub1SimId,   SlotType.Sub1);
            var b2  = GetCharacterBonus(s.sub2SimId,   SlotType.Sub2);
            s.currentStats = ApplyBonus(ApplyBonus(ApplyBonus(bs, b0), b1), b2);
        }
    }

    [BurstCompile]
    private BaseStats ApplyBonus(BaseStats bs, CharacterBonus bonus) => new BaseStats
    {
        maxHP             = bs.maxHP             + bonus.bonusHP,
        attackPower       = bs.attackPower       + bonus.bonusAttack,
        defense           = bs.defense           + bonus.bonusDefense,
        speed             = bs.speed             + bonus.bonusSpeed,
        evasion           = bs.evasion           + bonus.bonusEvasion,
        skillCooldownMult = bs.skillCooldownMult + bonus.bonusSkillCooldown,
    };
}
```

### 기본 스탯 수치표 (조정 가능)

| 스탯 | 더미 7기 기준 | 대장 +α | 서브 각 +α | 만편성 합계 |
|------|-------------|---------|-----------|------------|
| HP | 100 | +30 | +15 | 160 |
| 공격력 | 50 | +20 | +10 | 90 |
| 방어력 | 30 | +15 | +8 | 61 |
| 속도 | 100 | +10 | +5 | 120 |
| 회피 | 0.10 | +0.08 | +0.04 | 0.26 |
| 스킬쿨 배율 | 1.00x | -0.15 | -0.07 | 0.71x |

> 수치는 ScriptableObject(ShipData, CharacterData)에서 Inspector로 조정. 하드코딩 금지.

---

## STEP 5 — 전투 지휘형 + 스킬 시스템

### 목표
지휘형 함장의 패시브·자동스킬과 전투형 함재기의 액티브 스킬을 구현한다.

### SkillData (ScriptableObject)

```csharp
// 위치: Assets/_Game/Data/Skills/SkillData.cs
[CreateAssetMenu(menuName = "Game/Skill Data")]
public class SkillData : GameDataBase
{
    public SkillType   skillType;
    public TargetType  targetType;
    public float       cooldown;       // 자동스킬 발동 간격 (초)
    public float       range;          // 범위 (월드 단위)
    public float       power;          // 공격력 배율 또는 버프 수치
    public GameObject  effectPrefab;   // 발동 이펙트 (컷인 없음)
}

public enum SkillType  : byte { Passive, AutoAttack, AutoBuff, Active }
public enum TargetType : byte { EnemyArea, AllyArea, Self, SingleEnemy }
```

### CommanderComponent (지휘형 ECS 컴포넌트)

```csharp
// 위치: Assets/_Game/Scripts/Battle/Components/CommanderComponent.cs
// HARD RULE: 지휘형은 화면에 직접 표시 안 됨 — 함선 모델로 대체
public struct CommanderComponent : IComponentData
{
    public ulong  characterSimId;
    public ulong  attachedUnitSimId;   // 배치된 함선/모선 SimID
    public float  autoSkillTimer;      // 자동스킬 쿨타임 타이머 (논리 틱 단위)
    public byte   skillSlotCount;      // 보유 스킬 수 (최대 4)
    // 스킬 데이터는 BlobAsset으로 참조 (ScriptableObject 직접 참조 금지 — Burst 불가)
    public BlobAssetReference<SkillDataBlob> skillBlob;
}
```

### CommanderAutoSkillSystem

```csharp
// 위치: Assets/_Game/Scripts/Battle/Systems/CommanderAutoSkillSystem.cs
[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
[UpdateInGroup(typeof(CombatGroup))]
public partial struct CommanderAutoSkillSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (commander, transform) in
            SystemAPI.Query<RefRW<CommanderComponent>, RefRO<LocalTransform>>())
        {
            commander.ValueRW.autoSkillTimer++;   // 논리 틱 카운트

            ref var blob = ref commander.ValueRO.skillBlob.Value;
            for (int i = 0; i < blob.skills.Length; i++)
            {
                ref var skill = ref blob.skills[i];
                if (skill.skillType == SkillType.Passive) continue;

                // cooldown을 틱 단위로 환산 (cooldown_sec * 10tps)
                float cooldownTicks = skill.cooldown * 10f;
                if (commander.ValueRO.autoSkillTimer < cooldownTicks) continue;

                // 스킬 발동
                TriggerAutoSkill(ref skill, transform.ValueRO.Position, ref state, ref ecb);
                commander.ValueRW.autoSkillTimer = 0;
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    private void TriggerAutoSkill(
        ref SkillEntry skill,
        float3 origin,
        ref SystemState state,
        ref EntityCommandBuffer ecb)
    {
        switch (skill.targetType)
        {
            case TargetType.EnemyArea:
                ApplyAreaDamage(origin, skill.range, skill.power, ref state, ref ecb);
                break;
            case TargetType.AllyArea:
                ApplyAreaBuff(origin, skill.range, skill.power, ref state, ref ecb);
                break;
        }
        // 이펙트는 EventBus로 프레젠테이션에 전달 (Sim 직접 호출 금지)
        GameEvents.OnSkillTriggered?.Invoke(origin, skill.effectId);
    }
}
```

### CharacterUnitComponent (전투형 ECS 컴포넌트)

```csharp
// 위치: Assets/_Game/Scripts/Battle/Components/CharacterUnitComponent.cs
public struct CharacterUnitComponent : IComponentData
{
    public ulong    characterSimId;
    public ulong    squadronSimId;     // 소속 소대
    public SlotType slotType;
    public float    currentHP;
    public bool     isDown;            // 격추 여부
    public int      injuryTimerTicks;  // 부상 회복 남은 틱 수
    public byte     activeSkillCount;
    public BlobAssetReference<SkillDataBlob> skillBlob;
}
```

### ActiveSkillSystem (전투형 액티브 스킬)

```csharp
// 위치: Assets/_Game/Scripts/Battle/Systems/ActiveSkillSystem.cs
// HARD RULE: 스킬 발동은 Command 경유 (L5 UI 버튼 → SimCommand.ActivateSkill)
[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
[UpdateInGroup(typeof(CombatGroup))]
public partial struct ActiveSkillSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // CommandConsumeSystem이 이미 ActivateSkill 명령을 SkillActivateTag로 변환
        foreach (var (unit, tag) in
            SystemAPI.Query<RefRO<CharacterUnitComponent>, RefRO<SkillActivateTag>>())
        {
            if (unit.ValueRO.isDown) continue;   // 격추 상태면 발동 불가

            var skill = GetSkill(unit.ValueRO.skillBlob, tag.ValueRO.skillIndex);
            ExecuteActiveSkill(skill, unit.ValueRO.characterSimId, ref state);
        }
    }
}
```

---

## STEP 6 — 전투 카메라 + HUD

### 목표
스텔라리스 방식의 L1~L5 카메라 줌 시스템과 전투 HUD를 구현한다.
L5 진입 + 함재기 선택 시 액티브 스킬 UI가 등장한다.

### 카메라 줌 5단계

| 레벨 | 명칭 | 카메라 Y 기준 | 활성 UI | 플레이어 행동 |
|------|------|-------------|---------|-------------|
| L1 | 은하 뷰 | > 1000 | 항성·세력 | 함대 이동·외교 |
| L2 | 항성계 뷰 | 200~1000 | 행성·함대 아이콘 | 행성 클릭→내정 |
| L3 | 함대 뷰 | 50~200 | 함선 모델·HP바 | 포메이션·명령 |
| L4 | 함선 뷰 | 10~50 | 함선 디테일·함장 Sprite | 지휘형 확인 |
| L5 | 캐릭터 뷰 | < 10 | 함재기 모델·스킬 UI | 액티브 스킬 발동 |

### CameraController

```csharp
// 위치: Assets/_Game/Scripts/UI/CameraController.cs
// HARD RULE: 벽시계 기반 — 프레젠테이션 전용. Sim 상태 변경 금지.
public class CameraController : MonoBehaviour
{
    public enum ZoomLevel { Galaxy = 1, StarSystem = 2, Fleet = 3, Ship = 4, Character = 5 }

    [Header("줌 경계값 (Inspector 조정)")]
    public float galaxyThreshold     = 1000f;
    public float starSystemThreshold = 200f;
    public float fleetThreshold      = 50f;
    public float shipThreshold       = 10f;

    public ZoomLevel CurrentLevel { get; private set; }
    public event Action<ZoomLevel> OnZoomLevelChanged;

    private Camera _cam;

    private void Update()
    {
        float y = transform.position.y;
        var newLevel = y > galaxyThreshold     ? ZoomLevel.Galaxy
                     : y > starSystemThreshold ? ZoomLevel.StarSystem
                     : y > fleetThreshold      ? ZoomLevel.Fleet
                     : y > shipThreshold       ? ZoomLevel.Ship
                                               : ZoomLevel.Character;

        if (newLevel != CurrentLevel)
        {
            CurrentLevel = newLevel;
            OnZoomLevelChanged?.Invoke(CurrentLevel);
        }

        HandleZoomInput();
        HandlePanInput();
    }

    public void FocusOnUnit(Transform target)
    {
        // 더블클릭 시 해당 유닛으로 이동·줌인
        StartCoroutine(SmoothFocusCoroutine(target));
    }

    private void HandleZoomInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        transform.Translate(0, -scroll * zoomSpeed, 0);
    }

    [SerializeField] private float zoomSpeed = 500f;
    [SerializeField] private float panSpeed  = 200f;

    private void HandlePanInput() { /* 패닝 구현 */ }

    private IEnumerator SmoothFocusCoroutine(Transform target)
    {
        /* 보간 이동 구현 */
        yield break;
    }
}
```

### BattleHUDManager

```csharp
// 위치: Assets/_Game/Scripts/UI/BattleHUDManager.cs
public class BattleHUDManager : MonoBehaviour
{
    [Header("UI 패널 — Inspector 연결")]
    public GameObject fleetCommandPanel;    // L1~L3: 함대 명령
    public GameObject pauseButton;          // 항상
    public GameObject commanderPopup;       // L4 + 함선 클릭
    public GameObject squadronStatusPanel;  // L3 + 모선 클릭
    public GameObject skillButtonPanel;     // L5 + 함재기 선택
    public GameObject battleResultPanel;    // 전투 종료

    private CameraController _cam;

    private void Awake()
    {
        _cam = FindObjectOfType<CameraController>();
        _cam.OnZoomLevelChanged += OnZoomChanged;
    }

    private void OnZoomChanged(CameraController.ZoomLevel level)
    {
        fleetCommandPanel.SetActive(level <= CameraController.ZoomLevel.Fleet);
        // 나머지 패널은 줌 + 선택 이벤트 조합으로 제어
    }

    // 함선 클릭 시 (L4)
    public void OnShipClicked(ulong shipSimId)
    {
        if (_cam.CurrentLevel < CameraController.ZoomLevel.Ship) return;
        commanderPopup.SetActive(true);
        commanderPopup.GetComponent<CommanderPopupUI>().Populate(shipSimId);
    }

    // 함재기 선택 시 (L5)
    public void OnStrikerSelected(ulong characterSimId)
    {
        if (_cam.CurrentLevel != CameraController.ZoomLevel.Character) return;
        skillButtonPanel.SetActive(true);
        skillButtonPanel.GetComponent<SkillPanelUI>().Populate(characterSimId);
    }

    // 스킬 버튼 클릭 → Command 발행 (HARD RULE: UI가 Sim 직접 변경 금지)
    public void OnSkillButtonClicked(ulong characterSimId, byte skillIndex)
    {
        var cmd = new SimCommand
        {
            executionTick = SimTickDriver.Instance.CurrentTick + 3,
            playerId      = 0,
            type          = CommandType.ActivateSkill,
            payload       = new CommandPayload
            {
                targetSimId = characterSimId,
                byteParam   = skillIndex,
            },
        };
        SimTickDriver.Instance.Transport.SendCommand(cmd);
    }
}
```

---

## STEP 7 — BattleManager + 결과 처리

### 목표
전투 초기화, 실시간 진행, 일시정지, 종료 판정, 사후 처리를 담당하는
BattleManager를 구현한다.

### BattleManager

```csharp
// 위치: Assets/_Game/Scripts/Battle/BattleManager.cs
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    public bool IsBattleActive { get; private set; }

    [Header("전투 참여 편제 — Inspector 또는 코드에서 주입")]
    public List<ulong> playerFleetSimIds;
    public List<ulong> enemyFleetSimIds;

    private void Awake() { Instance = this; }

    public void StartBattle()
    {
        IsBattleActive = true;

        // 모든 모선 소대 출격 명령 (Command 경유)
        foreach (var carrierId in GetCarrierSimIds())
            SendLaunchCommand(carrierId);

        GameEvents.OnBattleStart?.Invoke(0);
    }

    // 매 틱 ChecksumGroup 이후 호출 (BattleResultCheckSystem에서)
    public void OnCheckBattleEnd(bool playerAllDestroyed, bool enemyAllDestroyed)
    {
        if (!playerAllDestroyed && !enemyAllDestroyed) return;

        bool playerWin = !playerAllDestroyed && enemyAllDestroyed;
        EndBattle(playerWin);
    }

    private void EndBattle(bool playerWin)
    {
        IsBattleActive = false;

        // 부상 처리
        ProcessInjuries();

        // 손실 계산
        var result = CalculateBattleResult(playerWin);

        // 스토리 이벤트 체크 (STEP 11에서 구현)
        StoryEventTrigger.CheckPostBattle(result);

        GameEvents.OnBattleEnd?.Invoke(playerWin);
    }

    private void ProcessInjuries()
    {
        // 격추된 전투형 캐릭터 → isDown = true, injuryTimerTicks 설정
        // Command 경유 (SetInjuryCommand)
    }

    private BattleResult CalculateBattleResult(bool playerWin)
    {
        return new BattleResult
        {
            playerWin          = playerWin,
            playerLostShips    = CountDestroyedShips(teamId: 0),
            enemyLostShips     = CountDestroyedShips(teamId: 1),
            injuredCharacters  = GetInjuredCharacters(),
        };
    }

    private void SendLaunchCommand(ulong carrierId)
    {
        var cmd = new SimCommand
        {
            executionTick = SimTickDriver.Instance.CurrentTick + 1,
            playerId      = 0,
            type          = CommandType.LaunchSquadron,
            payload       = new CommandPayload { targetSimId = carrierId },
        };
        SimTickDriver.Instance.Transport.SendCommand(cmd);
    }
}
```

### RenderSnapshotSystem (프레젠테이션 입력 생성)

```csharp
// 위치: Assets/_Game/Scripts/Battle/Systems/RenderSnapshotSystem.cs
// HARD RULE: [UpdateInGroup(typeof(RenderSnapshotGroup))] — 매 틱 마지막(순서 10번)
[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
[UpdateInGroup(typeof(RenderSnapshotGroup))]
public partial struct RenderSnapshotSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var tick = SystemAPI.GetSingleton<SimTickComponent>().CurrentTick;

        // prev ← curr 스왑
        RenderSnapshotBuffer.Swap();
        var curr = RenderSnapshotBuffer.GetWriteBuffer();
        curr.Clear();

        // live World에서 렌더링에 필요한 데이터만 복사
        // HARD RULE: FoW 미적용 — 프레젠테이션 단계에서 클라별 적용
        foreach (var (simId, transform, health, unitType) in
            SystemAPI.Query<
                RefRO<SimIDComponent>,
                RefRO<LocalTransform>,
                RefRO<HealthComponent>,
                RefRO<UnitTypeComponent>>())
        {
            curr.Add(new RenderEntry
            {
                simId    = simId.ValueRO.Value,
                position = transform.ValueRO.Position,
                rotation = transform.ValueRO.Rotation,
                hpRatio  = health.ValueRO.Current / health.ValueRO.Max,
                unitType = unitType.ValueRO.Value,
                tick     = tick,
            });
        }
    }
}

// 보간 (프레젠테이션 — 벽시계 기반)
public class RenderInterpolator : MonoBehaviour
{
    // HARD RULE: 벽시계 기반 — 프레젠테이션 전용
    private void LateUpdate()
    {
        float alpha = (Time.time - SimTickDriver.Instance.LastTickWallTime)
                    / SimTickDriver.Instance.CurrentTickIntervalSec;
        alpha = Mathf.Clamp01(alpha);

        var prev = RenderSnapshotBuffer.GetPrevBuffer();
        var curr = RenderSnapshotBuffer.GetCurrBuffer();

        foreach (var entry in curr)
        {
            if (!TryGetVisual(entry.simId, out var visual)) continue;

            // FoW 필터 (클라 자신의 시야 마스크)
            if (!FogOfWarManager.IsVisible(entry.position))
            {
                ApplyLastKnownPos(visual, entry.simId);
                continue;
            }

            var prevEntry = prev.Find(e => e.simId == entry.simId);
            visual.transform.position = Vector3.Lerp(prevEntry.position, entry.position, alpha);

            // 함재기 급선회 보정
            float angle = Quaternion.Angle(prevEntry.rotation, entry.rotation);
            visual.transform.rotation = angle > 45f
                ? Quaternion.LookRotation(entry.position - prevEntry.position)  // 속도 기반
                : Quaternion.Slerp(prevEntry.rotation, entry.rotation, alpha);
        }
    }
}
```

---

## STEP 8 — 내정 행성 + 건물 + 자원

### 목표
행성 건물 슬롯(행성 크기별 가변), 건설 큐, 자원 생산·소비 시스템을 구현한다.
스텔라리스 대비 간소화 — 매 게임일 3~5개의 의미 있는 결정에 집중한다.

### 내정 범위 (포함/제외)

| 포함 | 제외 (추후 검토) |
|------|----------------|
| 행성 건물 건설 (슬롯 기반) | 테라포밍 |
| 유닛 생산 (함선·모선) | 복잡한 무역 노선 |
| 항성 특수건물 | 연방·공동체 시스템 |
| 자원 관리 (5종) | 외계 종족 개별 관리 |
| 지지도 시스템 | |
| 시민 카드 (정책 슬롯) | |

### PlanetData (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "Game/Planet Data")]
public class PlanetData : GameDataBase
{
    public PlanetSize    planetSize;
    public ResourceBundle baseResourceOutput;   // 기본 자원 생산량 (건물 없는 상태)
    public int           specialSlotCount;      // 항성 특수건물 슬롯
}

public enum PlanetSize : byte
{
    Small  = 4,
    Medium = 6,
    Large  = 8,
    Huge   = 10,
}
```

### PlanetComponent (ECS 컴포넌트)

```csharp
// 위치: Assets/_Game/Scripts/Internal/Components/PlanetComponent.cs
public struct PlanetComponent : IComponentData
{
    public ulong      planetDataId;
    public int        totalSlots;         // 행성 크기 기반
    public int        usedSlots;
    public ulong      assignedCharSimId;  // 내정형 캐릭터 SimID (0 = 없음)
    public int        approvalRating;     // 지지도 0~100 (HARD RULE: int)
    public ResourceBundle currentOutput;  // 이번 게임일 순 생산량
}
```

### BuildingData (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "Game/Building Data")]
public class BuildingData : GameDataBase
{
    public BuildingType   buildingType;
    public ResourceBundle resourceOutput;
    public ResourceBundle upkeepCost;
    public ResourceBundle buildCost;
    public int            buildTurns;       // 건설 소요 게임일
    public string[]       synergyTags;      // 시너지 체크용
}

public enum BuildingType : byte
{ Production, Military, Research, Special }
```

### BuildingSlotComponent

```csharp
public struct BuildingSlotComponent : IComponentData
{
    public ulong  planetSimId;
    public ulong  buildingDataId;    // 0 = 빈 슬롯
    public int    constructionDays;  // 건설 남은 게임일 (0 = 완공)
    public bool   isUnlocked;
}
```

### ResourceSystem (GameDataGroup — 게임일마다)

```csharp
// HARD RULE: [UpdateInGroup(typeof(GameDataGroup))] — tick % 10 == 0
// HARD RULE: maxSkip 0 — 절대 스킵 불가 (자원 일관성)
[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
[UpdateInGroup(typeof(GameDataGroup))]
public partial struct ResourceSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 1. 건설 진행
        foreach (var slot in SystemAPI.Query<RefRW<BuildingSlotComponent>>())
        {
            if (slot.ValueRO.constructionDays <= 0) continue;
            slot.ValueRW.constructionDays--;
            if (slot.ValueRW.constructionDays == 0)
                ecb.AddComponent<BuildingCompletedTag>(/* entity */);
        }

        // 2. 자원 생산·소비 계산
        var globalResources = SystemAPI.GetSingletonRW<GlobalResourceComponent>();
        foreach (var planet in SystemAPI.Query<RefRW<PlanetComponent>>())
        {
            var output = CalculatePlanetOutput(planet.ValueRO, ref state);
            planet.ValueRW.currentOutput = output;
            globalResources.ValueRW.Add(output);
        }

        // 3. 유지비 차감
        foreach (var fleet in SystemAPI.Query<RefRO<FleetUnitComponent>>())
            globalResources.ValueRW.SubtractUpkeep(fleet.ValueRO);

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        // 프레젠테이션 갱신 알림
        GameEvents.OnResourceUpdated?.Invoke(globalResources.ValueRO.bundle);
    }
}
```

---

## STEP 9 — 시민 카드 + 지지도

### 목표
행성마다 정책 슬롯에 카드를 장착해 방향을 결정하는 시스템과
지지도 임계값별 효과·위기 시스템을 구현한다.

### CitizenCardData (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "Game/Citizen Card Data")]
public class CitizenCardData : GameDataBase
{
    [Header("자원 보정 (배율, 1.0 = 기본)")]
    public float productionMult;
    public float militaryMult;
    public float researchMult;
    public float foodMult;

    [Header("지지도 영향 (매 게임일)")]
    public int approvalDelta;   // HARD RULE: int
}
```

### PolicySlotComponent

```csharp
public struct PolicySlotComponent : IComponentData
{
    public ulong planetSimId;
    public ulong equippedCardDataId;   // 0 = 빈 슬롯
}
```

### 지지도 임계값

| 범위 | 효과 |
|------|------|
| 90~100 | 생산 +10%, 지원병 이벤트 발생 가능 |
| 70~89 | 정상 |
| 50~69 | 생산 -5% |
| 30~49 | 반란 이벤트 발생 가능 |
| 0~29 | 반란 확정 → 행성 통제권 상실 위험 |

### ApprovalSystem

```csharp
// HARD RULE: [UpdateInGroup(typeof(GameDataGroup))] — 게임일마다
// HARD RULE: 지지도는 int 연산만
[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
[UpdateInGroup(typeof(GameDataGroup))]
[UpdateAfter(typeof(ResourceSystem))]
public partial struct ApprovalSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var planet in SystemAPI.Query<RefRW<PlanetComponent>>())
        {
            int delta = CalculateApprovalDelta(planet.ValueRO, ref state);
            planet.ValueRW.approvalRating =
                math.clamp(planet.ValueRO.approvalRating + delta, 0, 100);

            CheckApprovalThreshold(planet.ValueRO);
        }
    }

    private int CalculateApprovalDelta(PlanetComponent planet, ref SystemState state)
    {
        int delta = 0;
        // 장착된 시민 카드 보정
        // 식량 상황 보정
        // 전쟁 중 패널티 등
        return delta;
    }
}
```

---

## STEP 10 — 캐릭터 데이터 + 성장 시스템

### 목표
캐릭터 3타입 분류, 성장(훈련·강화), 타입 전환 시스템을 구현한다.
HARD RULE: 모든 수치는 int. float 연산으로 Sim 상태 변경 금지.

### CharacterData (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "Game/Character Data")]
public class CharacterData : GameDataBase
{
    [Header("기본 정보")]
    public Sprite         faceSprite;      // UI 얼굴 Sprite (함장 팝업용)
    public CharacterType  characterType;

    [Header("스탯")]
    public BaseStats      baseStats;
    public CharacterBonus deployBonus;     // 배치 시 유닛에 적용되는 보정값

    [Header("스킬")]
    public SkillData[]    activeSkills;    // 전투형
    public SkillData[]    passiveSkills;   // 지휘형

    [Header("성장 — HARD RULE: int만")]
    public int            combatScore;     // 전투형 숙련도 0~100
    public int            commandScore;    // 지휘형 숙련도 0~100
    public int            internalScore;   // 내정형 숙련도 0~100
}

public enum CharacterType : byte { Combat, Commander, Internal }
```

### CharacterRuntimeComponent (ECS — 런타임 상태)

```csharp
public struct CharacterRuntimeComponent : IComponentData
{
    public ulong         characterDataId;
    public CharacterType currentType;        // 현재 타입 (전환 가능)
    public int           affinityLevel;      // 호감도 0~100 (HARD RULE: int)

    // 성장 수치 — HARD RULE: int
    public int           combatScore;
    public int           commandScore;
    public int           internalScore;

    public bool          isDown;             // 격추 상태
    public int           injuryTimerTicks;   // 부상 회복 남은 틱

    public DeployPosition currentDeploy;     // 현재 배치 위치
}

public enum DeployPosition : byte { None, Combat, Carrier, Internal }
```

### 성장 시스템

```csharp
// HARD RULE: int 연산만. float 금지.
[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
[UpdateInGroup(typeof(StoryGroup))]
public partial struct CharacterGrowthSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var ch in SystemAPI.Query<RefRW<CharacterRuntimeComponent>>())
        {
            // 배치 위치에 따라 해당 숙련도 증가
            switch (ch.ValueRO.currentDeploy)
            {
                case DeployPosition.Combat:
                    ch.ValueRW.combatScore   = math.min(ch.ValueRO.combatScore   + 1, 100);
                    break;
                case DeployPosition.Carrier:
                    ch.ValueRW.commandScore  = math.min(ch.ValueRO.commandScore  + 1, 100);
                    break;
                case DeployPosition.Internal:
                    ch.ValueRW.internalScore = math.min(ch.ValueRO.internalScore + 1, 100);
                    break;
            }

            // 타입 전환 조건 체크
            CheckTypeTransition(ref ch.ValueRW);
        }
    }

    private void CheckTypeTransition(ref CharacterRuntimeComponent ch)
    {
        // 예: combatScore >= 70이고 commandScore >= 40이면 지휘→전투 전환 가능
        // 조건 충족 시 TypeTransitionAvailableTag 추가 (Command 경유로 실제 전환)
    }
}
```

### 타입 전환 조건표

| 전환 | 조건 | 결과 |
|------|------|------|
| 내정→지휘 | internalScore ≥ 50, commandScore ≥ 30 | CommanderType 해금 |
| 지휘→전투 | commandScore ≥ 60, combatScore ≥ 40 | CombatType 해금 |
| 전투→내정 | combatScore ≥ 50, internalScore ≥ 30 | InternalType 해금 |
| 만능형 해금 | 세 가지 Score 모두 ≥ 80 | 전 타입 배치 가능 |

---

## STEP 11 — 호감도 + 스토리 트리거

### 목표
배치 위치 + 호감도 수치, 두 조건 동시 충족 시 스토리 이벤트를 트리거하는
시스템을 구현한다. HARD RULE: 호감도는 int. float 금지.

### StoryEventData (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "Game/Story Event Data")]
public class StoryEventData : GameDataBase
{
    [Header("트리거 조건 — HARD RULE: int")]
    public DeployPosition requiredDeploy;
    public int            requiredAffinity;        // 최소 호감도

    [Header("특수 트리거")]
    public bool           triggerOnShipDestroyed;  // 함선 격파 생존
    public bool           triggerOnCharacterDown;  // 격추 시

    [Header("대사·보상")]
    public string[]       dialogueLines;
    public int            rewardAffinity;           // HARD RULE: int
    public SkillData      rewardSkillUnlock;        // null 가능

    [Header("연계")]
    public StoryEventData nextEvent;               // null = 독립 이벤트
}
```

### 트리거 조건표

| 배치 조건 | 호감도 조건 | 트리거 이벤트 |
|---------|-----------|------------|
| 전장(함재기) 배치 | ≥ 30 | 전투 스토리 해금 |
| 모선 함장 배치 | ≥ 50 | 지휘 스토리 해금 |
| 행성 내정 배치 | ≥ 20 | 내정 스토리 해금 |
| 격추 발생 | ≥ 40 | 부상 이벤트 + 특수 대사 |
| 모선 격파 생존 | ≥ 60 | 탈출 이벤트 + 관계 분기 |

### AffinitySystem

```csharp
// HARD RULE: [UpdateInGroup(typeof(StoryGroup))] — tick % 10 == 0
// HARD RULE: int 연산만
[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
[UpdateInGroup(typeof(StoryGroup))]
public partial struct AffinitySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var ch in SystemAPI.Query<RefRW<CharacterRuntimeComponent>>())
        {
            // 배치 중이면 소량 호감도 증가
            if (ch.ValueRO.currentDeploy != DeployPosition.None)
                ch.ValueRW.affinityLevel =
                    math.min(ch.ValueRO.affinityLevel + 1, 100);

            // 임계값 체크
            CheckAffinityThresholds(ref ch.ValueRW, ref state);
        }
    }

    private void CheckAffinityThresholds(
        ref CharacterRuntimeComponent ch, ref SystemState state)
    {
        // StoryEventData 목록 순회하여 조건 충족 체크
        // 충족 시 StoryEventTrigger 큐에 적재
    }
}
```

### StoryEventTrigger

```csharp
// 위치: Assets/_Game/Scripts/Character/StoryEventTrigger.cs
// Sim → 프레젠테이션 단방향 (EventBus 경유)
public static class StoryEventTrigger
{
    private static Queue<StoryEventData> _pendingEvents = new();

    // Sim 레이어에서 호출 (StoryGroup, 전투 결과 처리)
    public static void Enqueue(StoryEventData eventData)
    {
        _pendingEvents.Enqueue(eventData);
    }

    // 전투 종료 후 체크
    public static void CheckPostBattle(BattleResult result)
    {
        // 격추·함선 격파 조건 체크 후 Enqueue
    }

    // 프레젠테이션 레이어에서 소비 (매 프레임)
    public static void FlushToPresentation()
    {
        while (_pendingEvents.Count > 0)
            GameEvents.OnStoryTriggered?.Invoke(_pendingEvents.Dequeue());
    }
}
```

---

## STEP 12 — 미니 캠페인 통합 씬

### 목표
STEP 1~11의 모든 시스템을 연결한 플레이어블 Vertical Slice 데모를 구현한다.
포트폴리오·피칭용 — 전투·내정·캐릭터가 연결된 완성된 흐름을 보여준다.

### 캠페인 씬 구성

| 씬 | 내용 |
|----|------|
| IntroScene | 세계관 소개, 캐릭터 초기 배치 선택 |
| InternalScene_1 | 행성 1개 내정 — 건물 건설, 시민 카드, 유닛 생산 |
| BattleScene_1 | 소규모 전투 — 함재기 소대 + 함선 편대 |
| EventScene_1 | 전투 결과 기반 스토리 이벤트 |
| InternalScene_2 | 손실 반영 재건 결정 |
| BattleScene_2 | 보스급 전투 — 모선 포함, 지휘형 자동스킬 연출 |
| ResultScene | 캠페인 결과, 성장한 캐릭터 표시 |

### 씬 전환 규칙

| 조건 | 다음 씬 |
|------|---------|
| 전투 승리 | EventScene → InternalScene 순 |
| 전투 패배 | ResultScene (패배 루트) |
| 내정 턴 종료 | BattleScene 또는 EventScene |
| 이벤트 선택지 A | 분기 씬 A |
| 이벤트 선택지 B | 분기 씬 B |

### CampaignManager

```csharp
// 위치: Assets/_Game/Scripts/Core/CampaignManager.cs
public class CampaignManager : MonoBehaviour
{
    public static CampaignManager Instance { get; private set; }

    // 씬 전환 — Command 경유 불필요 (씬 전환은 Sim 상태 변경 아님)
    public void TransitionTo(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        // 틱 드라이버 일시정지 (Command 경유)
        SimTickDriver.Instance.RequestPause();

        // 전환 이펙트
        yield return FadeOut();

        // 씬 로드
        yield return SceneManager.LoadSceneAsync(sceneName);

        yield return FadeIn();

        // 재개
        SimTickDriver.Instance.RequestResume();
    }

    // 전투 결과 수신 후 다음 씬 결정
    private void OnBattleEnd(bool playerWin)
    {
        TransitionTo(playerWin ? "EventScene_1" : "ResultScene");
    }
}
```

---

## 공통 SystemGroup 선언

모든 커스텀 SystemGroup은 여기서 선언한다.
HARD RULE: 실행 순서는 [UpdateInGroup]/[UpdateBefore]/[UpdateAfter]로 명시 고정.

```csharp
// 위치: Assets/_Game/Scripts/Core/SimSystemGroups.cs

// ── Sim 루트 그룹 (SimTickDriver가 수동 업데이트) ──
public class SimRootGroup         : ComponentSystemGroup { }

// ── 순서 1: 명령 소비 ──
[UpdateInGroup(typeof(SimRootGroup))]
public class CommandConsumeGroup  : ComponentSystemGroup { }

// ── 순서 2: 전투 ──
[UpdateInGroup(typeof(SimRootGroup))]
[UpdateAfter(typeof(CommandConsumeGroup))]
public class CombatGroup          : ComponentSystemGroup { }

// ── 순서 3: 함선 이동 (tick % 2) ──
[UpdateInGroup(typeof(SimRootGroup))]
[UpdateAfter(typeof(CombatGroup))]
public class FleetMoveGroup       : ComponentSystemGroup { }

// ── 순서 4: 가시성 (tick % 4) ──
[UpdateInGroup(typeof(SimRootGroup))]
[UpdateAfter(typeof(FleetMoveGroup))]
public class VisionGroup          : ComponentSystemGroup { }

// ── 순서 5: AI 스냅샷 (tick % 10) ──
[UpdateInGroup(typeof(SimRootGroup))]
[UpdateAfter(typeof(VisionGroup))]
public class AISnapshotGroup      : ComponentSystemGroup { }

// ── 순서 6: 게임 데이터 (tick % 10, 게임일) ──
[UpdateInGroup(typeof(SimRootGroup))]
[UpdateAfter(typeof(AISnapshotGroup))]
public class GameDataGroup        : ComponentSystemGroup { }

// ── 순서 7: 스토리 (tick % 10) ──
[UpdateInGroup(typeof(SimRootGroup))]
[UpdateAfter(typeof(GameDataGroup))]
public class StoryGroup           : ComponentSystemGroup { }

// ── 순서 8: 외교 (tick % 20) ──
[UpdateInGroup(typeof(SimRootGroup))]
[UpdateAfter(typeof(StoryGroup))]
public class DiplomacyGroup       : ComponentSystemGroup { }

// ── 순서 9: 체크섬 ──
[UpdateInGroup(typeof(SimRootGroup))]
[UpdateAfter(typeof(DiplomacyGroup))]
public class ChecksumGroup        : ComponentSystemGroup { }

// ── 순서 10: 렌더 스냅샷 (마지막) ──
[UpdateInGroup(typeof(SimRootGroup))]
[UpdateAfter(typeof(ChecksumGroup))]
public class RenderSnapshotGroup  : ComponentSystemGroup { }
```

---

## 미정 (TBD)

- 코드 스타일·네이밍 컨벤션 (Harness 10장 참고)
- 세이브 직렬화 포맷 상세
- AI 공정성 파라미터 (안개 너머 인지 여부)
- 네트워크 전송 계층 상세 (멀티 4단계)
- 외교 시스템 상세 (DiplomacyGroup)
- 항성 특수건물 상세

