# 플랜 5-3 — 검증 (미래 틱 스탬프 동작 확인)

> 이 파일 1개 + Harness(상시)만 로드하면 작업 시작 가능.
> 학습 방식: 코드는 학습자가 직접 타이핑. Claude/Claude Code는 설명·제시만(LEARNING.md 진행 방식, Harness §0-1).

---
플랜 ID: cpp5-3
상위 단계: C++ 코어 로드맵 5단계 — 명령 큐 + 소비
선행조건: cpp5-2 완료 (`consume_commands` + `sim_tick` 연결)
상태: ⬜ 대기
---

## 목표 (한 줄)
명령 파이프라인이 **락스텝 의미대로** 동작하는지 Unity 호스트에서 확인한다 — ① 미래 틱 스탬프는 그 틱에 정확히 적용 ② 무효 SimID는 조용히 무시.

## 왜 (맥락)
5-1·5-2는 코드만 깔았다. "즉시 적용이 아니라 **스탬프한 틱에** 적용된다"는 게 락스텝 입력 지연(Harness §5-2)의 핵심이라, 이걸 눈으로 확인해야 5단계가 닫힌다. 코드 추가보다 **관찰 시나리오**가 본체.

## 대상 파일
- `Assets/Scripts/Game/Core/SimCoreTest.cs` — `sim_enqueue_command` P/Invoke 선언 + 시나리오 호출/로그 (테스트 호스트, Sim 아님)

## 작업 단위 (체크리스트)
- [ ] `SimCoreTest.cs`: `[DllImport("SimCore")] sim_enqueue_command` 선언 추가 (인자 폭 정확히)
- [ ] enum 값 미러: C#에 `const int SIM_CMD_SET_VELX = 1;` (또는 enum)
- [ ] 시나리오 A: spawn(velX=1.0) → 틱 진행 중 `enqueue(now+3, SET_VELX, simId, 5.0)` → posX 증가율이 **정확히 3틱 뒤** 1→5로 바뀌는지 로그 확인
- [ ] 시나리오 B: `enqueue(now+1, SET_VELX, simId=99(없음), ...)` → 크래시 없이 **무시**되는지 확인
- [ ] (선택) 시나리오 C: 같은 틱에 여러 명령 → sequence 순서대로 적용되는지

## 추가/변경할 API 표면 (C# 측, 시그니처만)
```csharp
[DllImport("SimCore")]
private static extern void sim_enqueue_command(
    IntPtr sim, ulong executionTick, byte playerId, ushort sequence,
    int type, ulong targetSimId, long argRaw);

const int SIM_CMD_SET_VELX = 1;
// 현재 틱 = sim_current_tick(m_sim) 로 읽어 now+3 스탬프
// argRaw = 5 * ONE  (ONE = 65536) → velX = 5.0
```

## 완료 기준 (검증)
- 시나리오 A: 스탬프한 틱 **이전**엔 1씩, **이후**엔 5씩 증가 → 미래 틱 스탬프 = 즉시 적용 아님(락스텝 입력 지연) 확인.
- 시나리오 B: 없는 SimID 명령이 크래시/오작동 없이 드랍 → 소비 시점 검증 확인.
- 로그로 위 두 가지가 재현되면 5단계 종료. LEARNING.md "C++ 코어 로드맵" 5단계 ✅ 처리 + 완료 상세 기록.

## 관련 HARD RULE
- Harness §5-2 입력 지연 — 로컬 입력은 현재틱+N 스탬프(전략 게임 체감 미미). 시나리오 A가 이걸 실증.
- Harness §5-2 무효 명령 — 조용히 드랍. 시나리오 B가 이걸 실증.
- Harness §3-1 두 시계 — 호스트(`SimCoreTest`)는 벽시계로 cadence만, 명령의 틱 스탬프는 정수 틱(`sim_current_tick`) 기준.

## 함정
- **틱 스탬프 기준** — `now`는 `sim_current_tick`(정수)로 읽어 `+3`. `Time.deltaTime`로 계산 금지(두 시계 혼용).
- **인자 폭/순서** — C# `[DllImport]` 시그니처가 C ABI와 1:1(byte/ushort/int/ulong/long). 어긋나면 스택 깨짐.
- **관찰만, 로직 추가 금지** — Sim 동작 변경은 5-2에서 끝. 여기서 코어(`sim_core.*`)를 또 손대면 단위 분리가 깨짐.
- (알려진 범위 밖) 이 테스트 호스트엔 아직 **락스텝 게이트(`CanAdvanceTick`)가 없다** — 단일 플레이 확인용. 멀티 게이트는 로드맵 후반(네트워크).
