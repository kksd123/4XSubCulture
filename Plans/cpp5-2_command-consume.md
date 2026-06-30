# 플랜 5-2 — 명령 소비 (consume + sim_tick 연결)

> 이 파일 1개 + Harness(상시)만 로드하면 작업 시작 가능.
> 학습 방식: 코드는 학습자가 직접 타이핑. Claude/Claude Code는 설명·제시만(LEARNING.md 진행 방식, Harness §0-1).

---
플랜 ID: cpp5-2
상위 단계: C++ 코어 로드맵 5단계 — 명령 큐 + 소비
선행조건: cpp5-1 완료 (`SimCommand` struct · `Sim.commands` 큐 · `sim_enqueue_command` 적재)
상태: ⬜ 대기
---

## 목표 (한 줄)
큐에 쌓인 명령을 **틱 시작 시점에 정렬·일괄 소비**해 대상 유닛(SimID lookup)에 적용한다. 명령 파이프라인의 나머지 절반.

## 왜 (맥락)
5-1은 적재만 했다(효과 없음). 락스텝은 **모든 클라가 같은 명령을 같은 틱에 같은 순서로** 소비해야 같은 결과가 난다. 그래서 ① 정렬 키로 순서를 못박고 ② `executionTick == 현재틱`인 것만 ③ 이동(`posX += velX`) **전에** 적용한다. 4단계의 `idToIndex` lookup(`*_by_id`)이 여기서 처음 실제로 쓰인다.

## 대상 파일
- `SimCore/SimCore/sim_core.cpp` — 내부 헬퍼 2개 추가 + `sim_tick` 수정 + `<algorithm>` include

## 작업 단위 (체크리스트)
- [ ] `sim_core.cpp`: `#include <algorithm>` 추가(`std::sort`)
- [ ] `static void apply_command(Sim*, const SimCommand&)` 추가 — SimID lookup → 타입별 적용, 무효 시 드랍
- [ ] `static void consume_commands(Sim*)` 추가 — 정렬 → 이번 틱만 소비 → 미래는 보관, 만료는 드랍(swap)
- [ ] `sim_tick` 수정: `currentTick++` **직후, 이동 루프 전**에 `consume_commands(sim)` 호출
- [ ] 빌드 → DLL 복사

## 추가/변경할 구조 (시그니처/흐름만 — 구현은 세션에서)
```cpp
// extern "C" 블록 밖, static 내부 헬퍼
static void apply_command(Sim* sim, const SimCommand& cmd);
//   idToIndex.find(targetSimId) → 없으면 return(무효 드랍)
//   type == SIM_CMD_SET_VELX → unit.velX = Fixed::FromRaw(cmd.argRaw)

static void consume_commands(Sim* sim);
//   std::sort by (executionTick, playerId, sequence)
//   for cmd: tick>now → remaining 보관 / tick<now → 드랍(만료) / tick==now → apply
//   commands.swap(remaining)   ← erase-during-iterate 회피 (4단계 swap-remove와 같은 결)

// sim_tick 내부 순서
//   sim->currentTick++;
//   consume_commands(sim);     // 소비 = 이동 전
//   for units: posX = posX + velX;
```

## 완료 기준 (검증)
- 빌드 성공.
- (단독 확인) spawn 후 `sim_enqueue_command(sim, now+0, ..., SET_VELX, simId, newVel)` 즉시 소비되면 velX 변경 반영.
- 본격 검증(미래 틱 스탬프 N틱 뒤 적용, 무효 SimID 무시)은 5-3에서.

## 관련 HARD RULE
- Harness §5-2 정렬 키 — `(executionTick, playerId, sequence)`로 전 클라 동일 순서.
- Harness §5-2 소비 시점 — 틱 시작 시 해당 틱 명령만 일괄 소비(`CommandConsumeGroup` 대응).
- Harness §5-2 검증 — **소비 시점**에 유효성 검사(대상 생존?), 발행 시점 검사 금지.
- Harness §5-2 무효 명령 — 조용히 드랍, 부분 실행 금지(전부 or 전무).
- Harness §2-2 — `unordered_map` **순회로 상태 변경 금지**(조회만). 소비는 벡터 순회, map은 lookup만.

## 함정
- **소비는 이동 전.** `consume_commands`를 move 루프 뒤에 두면 명령이 한 틱 늦게 먹는다.
- **erase 중 순회 금지** — `remaining` 새 벡터에 담아 `swap`(인덱스 밀림 방지).
- **정렬 키 유일성** — 세 키가 합쳐 유일하면 `std::sort`로 충분. 겹칠 수 있으면 `std::stable_sort`.
- **로그 자리** — DLL `printf`는 Unity 콘솔에 안 뜸. 무효 드랍 로그는 주석으로 자리만, 콜백 로깅은 7단계.
