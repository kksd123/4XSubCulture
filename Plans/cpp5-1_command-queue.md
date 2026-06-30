# 플랜 5-1 — 명령 자료구조 + 큐 적재

> 이 파일 1개 + Harness(상시)만 로드하면 작업 시작 가능.
> 학습 방식: 코드는 학습자가 직접 타이핑. Claude/Claude Code는 설명·제시만(LEARNING.md 진행 방식, Harness §0-1).

---
플랜 ID: cpp5-1
상위 단계: C++ 코어 로드맵 5단계 — 명령 큐 + 소비
선행조건: 1~4단계 완료 (DLL C ABI · 정수 틱 · 고정소수점 · SimID 레지스트리)
상태: ⬜ 다음
---

## 목표 (한 줄)
외부 입력을 `SimCommand`로 **큐에 쌓기만** 하는 경로(`sim_enqueue_command`)를 만든다. (소비/적용은 5-2)

## 왜 (맥락)
Sim 상태를 바꾸는 입구는 **명령 하나뿐**이어야 한다(락스텝 핵심). 지금은 `sim_spawn` 생성 시점 외에 상태를 바꿀 길이 없다. 명령 파이프라인의 첫 절반(적재)을 깔아, 호스트가 코어 내부를 직접 건드리지 않고 명령으로만 입력하게 한다.

## 대상 파일
- `SimCore/SimCore/sim_core.h` — enum + `sim_enqueue_command` 선언 추가
- `SimCore/SimCore/sim_core.cpp` — `SimCommand` struct + `Sim.commands` 필드 + enqueue 구현

## 작업 단위 (체크리스트)
- [ ] `sim_core.h`: `enum SimCommandType { SIM_CMD_NONE=0, SIM_CMD_SET_VELX=1 }` 추가
- [ ] `sim_core.h`: `sim_enqueue_command(...)` 선언 추가
- [ ] `sim_core.cpp`: 내부 `SimCommand` struct 정의 (`extern "C"` 밖)
- [ ] `sim_core.cpp`: `Sim`에 `std::vector<SimCommand> commands;` 필드 추가
- [ ] `sim_core.cpp`: `sim_enqueue_command` 구현 (`commands.push_back`만 — 적용 없음)
- [ ] 빌드 (에디터 닫고 → 빌드 → `Assets/Plugins/SimCore.dll` 복사 → 에디터 열기)

## 추가/변경할 API 표면
```c
// sim_core.h (구현은 세션에서 직접 타이핑)
enum SimCommandType {
    SIM_CMD_NONE     = 0,
    SIM_CMD_SET_VELX = 1,
};

SIMCORE_API void sim_enqueue_command(
    Sim*     sim,
    uint64_t execution_tick,   // 이 틱에 소비 (미래 틱 스탬프)
    uint8_t  player_id,        // 정렬 키
    uint16_t sequence,         // 정렬 키
    int32_t  type,             // SimCommandType
    uint64_t target_sim_id,    // 대상 유닛 — SimID만
    int64_t  arg_raw);         // 페이로드 (예: 새 velX raw)
```

## 완료 기준 (검증)
- DLL 빌드 성공.
- C#에 `[DllImport("SimCore")] sim_enqueue_command` 선언 후 호출해도 **크래시 없음**.
- 아직 **효과는 없음**(velX 안 바뀜) — 정상. 소비(5-2) 전이므로 큐에 쌓이기만 하면 성공.

## 관련 HARD RULE
- Harness §5 — 명령은 Sim 상태 변경의 **유일한 입구**.
- Harness §5-1 — `SimCommand`는 unmanaged·blittable. (경계는 struct 대신 **스칼라 인자**로 단순화 → 마샬링 회피)
- Harness §5-2 — 대상 참조는 **SimID(ulong)만**, Entity 핸들/포인터 금지. 정렬 키 `(executionTick, playerId, sequence)`는 5-2 소비에서 사용.

## 함정
- **경계에 struct 넘기지 말 것** — 평평한 스칼라 인자로. `SimCommand` struct는 C++ 내부에만.
- **enum 폭 일치** — C `int`(4B) ↔ C# `Int32`. P/Invoke 시그니처 어긋나면 스택 깨짐.
- 이 단계는 **적재만**. `consume`/`sim_tick` 수정/정렬은 5-2 범위 — 여기서 손대지 말 것(단위 분리 유지).
