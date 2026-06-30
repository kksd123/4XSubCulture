# 작업 플랜 인덱스 (C++ 코어 로드맵)

> **컨텍스트 최소화 장치.** 각 작업은 1-1, 1-2 단위 파일로 쪼개 둔다.
> 작업 시작 시 **Harness(상시) + 해당 플랜 1개만** 컨텍스트에 로드한다 — 전부 import 금지(그러면 최소화가 깨짐).
>
> 사용법: 작업할 때 `@Plans/<플랜파일>` 하나만 당겨온다. 예) `@Plans/cpp5-1_command-queue.md`
> 새 플랜은 `_TEMPLATE.md`를 복사해 만든다. 진행 상태의 단일 기준은 `LEARNING.md`(개념)·`Harness/PROGRESS.md`(코드).

---

## C++ 코어 로드맵 ↔ 플랜 매핑

| 단계 | 플랜 ID | 작업 단위 | 상태 | 파일 |
|------|---------|-----------|------|------|
| 1~4 | — | DLL C ABI · 정수 틱 · 고정소수점 · SimID 레지스트리 | ✅ 완료 | (LEARNING.md "완료 상세") |
| 5 | cpp5-1 | 명령 자료구조 + 큐 적재 (`sim_enqueue_command`) | ⬜ 다음 | [`cpp5-1_command-queue.md`](./cpp5-1_command-queue.md) |
| 5 | cpp5-2 | 명령 소비 (`consume_commands` + `sim_tick` 연결) | ⬜ | [`cpp5-2_command-consume.md`](./cpp5-2_command-consume.md) |
| 5 | cpp5-3 | 검증 — Unity P/Invoke로 미래 틱 스탬프 동작 확인 | ⬜ | [`cpp5-3_command-verify.md`](./cpp5-3_command-verify.md) |
| 6 | cpp6-x | 체크섬 + 스냅샷 | ⬜ | _미작성_ |
| 7 | cpp7-x | Unity 호스트 정리(DLL 호출) | ⬜ | _미작성_ |

> 6단계 이후는 5단계 진행하며 같은 템플릿으로 쪼개 추가한다.

---

## 명명 규약
- 파일: `cpp<단계>-<서브>_<슬러그>.md` (예: `cpp5-1_command-queue.md`)
- 한 파일 = **한 작업 단위**(한 세션에 끝낼 수 있는 크기). 커지면 더 쪼갠다.
- 상태 표기: ⬜ 대기 · 🔄 진행 · ✅ 완료
