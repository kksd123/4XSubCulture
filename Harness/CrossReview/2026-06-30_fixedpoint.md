# 교차검증 기록 — 고정소수점 결정성

---
검증일: 2026-06-30
검증 대상: 고정소수점 산술 토대 (`SimCore/SimCore/fixed.h` + 사용 맥락)
검증자 모델: GPT-5.5 (추론 : 높음)
설계자 모델: Claude Opus-4.8(추론 : 높음)
입력 문서: Engineering_Harness_v1_1.md(결정성 규칙) + fixed.h 전문 + sim_core.cpp 사용부
목적: 동일 모델 자기평가 편향을 외부 모델로 상쇄 — 고정소수점 허점/정확성/경계 위험 발굴
---

## 0. 검증 대상 스냅샷 (재현용)
> 검증 시점의 `fixed.h` 핵심. 이후 코드가 바뀌면 이 기록의 전제도 바뀜에 유의.

- `Fixed{ int64_t raw }`, `SHIFT=16`, `ONE=65536`
- `operator+/-`: 정수 가감
- `operator*`: `(a.raw * b.raw) >> 16`  ← 곱셈 후 시프트
- `operator/`: `(a.raw << 16) / b.raw`
- `ToInt()`: `raw >> 16` (음수 floor)
- 사용: 매 틱 `posX = posX + velX` (가감산만 사용 중, 곱/나눗셈은 정의됐으나 미사용)

---

## 1. 받은 지적 (Gemini 응답 원문 — 그대로 붙여넣기)

심각도: 상] · [분류: 데싱크]

[근거: [fixed.h (line 18)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:18) operator+, [sim_core.cpp (line 43)](/C:/Workspace/4XSubCulture/SimCore/SimCore/sim_core.cpp:43) posX = posX + velX. sim_spawn(sim, INT64_MAX, 1) 후 sim_tick()이면 a.raw + b.raw가 signed overflow. C++ signed overflow는 UB라 컴파일러/최적화별 결과가 비트 동일이라는 보장이 없다.]

[구체 수정 제안: “Fixed raw 연산은 signed overflow를 절대 발생시키지 않는다. 모든 +/-는 사전 범위 검사 후 trap/saturate/wrap 중 하나를 명문화한다.” 코드 제안: if ((b.raw > 0 && a.raw > INT64_MAX - b.raw) || ...) return OverflowPolicy(...); 또는 unsigned 2’s-complement wrap을 의도적으로 구현.]

[반론 여지: “게임 값 범위가 작다.” 가능. 하지만 C ABI가 FromRaw를 그대로 열어 두고 있어서 현재 타입 자체는 그 전제를 강제하지 않는다.]

[심각도: 상] · [분류: 데싱크]

[근거: [fixed.h (line 19)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:19) operator-. Fixed::FromRaw(INT64_MIN) - Fixed::FromRaw(1) 또는 0 - INT64_MIN은 signed overflow UB.]

[구체 수정 제안: operator-도 +와 같은 overflow 정책을 공유해야 한다. 헌법 문장: “감산은 a + (-b)로 암묵 변환하지 말고 INT64_MIN 부호 반전 케이스까지 별도 처리한다.”]

[반론 여지: 아직 감산 미사용. 하지만 타입 API로 열려 있고 이후 거리/방향 계산에서 바로 쓰일 가능성이 높다.]

[심각도: 상] · [분류: 데싱크]

[근거: [fixed.h (line 20)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:20) operator*: (a.raw * b.raw) >> 16. 예: a = Fixed::FromInt(100000), b = Fixed::FromInt(100000)이면 raw는 각각 6,553,600,000, 곱은 약 4.29e19로 INT64_MAX 초과. >>16 전에 이미 UB.]

[구체 수정 제안: 중간값은 최소 128비트로 고정: __int128 p = (__int128)a.raw * b.raw; 후 범위 검사. MSVC 포함이면 _mul128/boost::multiprecision::int128_t/플랫폼별 래퍼를 단일 구현으로 감싼다.]

[반론 여지: 곱셈은 아직 미사용. 하지만 정의된 연산자가 잘못된 계약을 제공하며, 이동 보간/가속도/피해 배율이 들어오는 순간 터진다.]

[심각도: 상] · [분류: 데싱크]

[근거: [fixed.h (line 21)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:21) operator/: (a.raw << 16) / b.raw. a.raw = 140737488355328이면 a.raw << 16이 INT64_MAX 범위를 넘는다. 음수 a.raw = -1도 signed negative left shift라 표준상 UB인 컴파일러 모드가 있다.]

[구체 수정 제안: 나눗셈도 128비트 numerator 사용: (__int128)a.raw << SHIFT. 헌법 문장: “signed 값에 대한 left shift 금지. 스케일 확장은 128비트 정수 곱셈 또는 명시적 checked shift로만 수행한다.”]

[반론 여지: MSVC x64에서 기대대로 보일 수 있다. 하지만 요구 조건은 MSVC x64가 아니라 컴파일러/플랫폼 무관 비트 동일성이다.]

[심각도: 상] · [분류: 데싱크]

[근거: [fixed.h (line 21)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:21) operator/가 b.raw == 0을 검사하지 않는다. Fixed::FromRaw(123) / Fixed::FromRaw(0)은 정수 divide-by-zero로 런타임 trap/시그널/예외 동작이 플랫폼별로 달라질 수 있다.]

[구체 수정 제안: 0 분모 정책을 필수 규칙으로 박아야 한다. 예: “0 분모는 deterministic fatal error code를 기록하고 tick을 중단한다” 또는 “API 경계에서 명령을 거부한다”. 조용한 임의값 반환 금지.]

[반론 여지: 게임 로직에서 0으로 나누지 않게 하면 된다. 하지만 락스텝 명령/맵 데이터/외부 raw 주입이 있는 구조에서는 ‘못 들어온다’가 타입 불변식이 아니다.]

[심각도: 중] · [분류: 데싱크]

[근거: [fixed.h (line 14)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:14), [fixed.h (line 20)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:20) 음수 signed right shift. Fixed::FromRaw(-1).ToInt()와 Fixed::FromRaw(-1) * Fixed::FromRaw(1)은 raw >> 16 또는 음수 결과 >> 16에 의존한다. C++ 표준/언어 버전/컴파일러별로 arithmetic shift 보장이 흔들리는 영역이다.]

[구체 수정 제안: 음수 스케일 다운은 직접 정의한다. 예: FloorDivPow2(raw, 16) 또는 TruncDivPow2(raw, 16) 함수를 만들고 / (1LL << SHIFT) 기반의 명시 정책을 사용한다.]

[반론 여지: 대부분의 현대 타깃은 arithmetic shift다. 그렇더라도 “대부분”은 락스텝 결정성 계약이 아니다.]

[심각도: 중] · [분류: 정확성]

[근거: [fixed.h (line 20)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:20) 곱셈의 음수 반올림이 편향된다. a.raw = -1, b.raw = 1이면 실제 값은 -1 / 2^32라 16.16으로는 0에 가까운 값인데 arithmetic shift 환경에서는 결과 raw가 -1이 된다. 즉 극소 음수 곱이 1 LSB 음수로 과대 보존된다.]

[구체 수정 제안: 곱셈 rounding convention을 명문화한다. 예: round toward zero, floor, nearest-even 중 하나. 코드도 그 정책을 함수명으로 드러내라: MulTrunc, MulFloor, MulNearest.]

[반론 여지: floor가 의도라면 -1은 맞다. 하지만 현재 나눗셈은 C++ / 때문에 0 방향 절삭이라 곱셈과 정책이 섞인다.]

[심각도: 중] · [분류: 정확성]

[근거: [fixed.h (line 21)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:21) 나눗셈은 C++ signed integer division이라 0 방향 절삭이다. 반면 음수 곱셈/ToInt는 arithmetic shift 환경에서 floor 성향이다. 예: Fixed::FromRaw(-1) / Fixed::FromRaw(2 * 65536)은 half-LSB 음수를 0으로 잃을 수 있고, 곱셈의 유사 케이스는 -1로 남을 수 있다.]

[구체 수정 제안: “모든 스케일 축소 연산은 동일 rounding 모드”라는 규칙 필요. 특히 곱셈과 나눗셈이 같은 방향으로 편향되어야 물리/위치 누적이 예측 가능하다.]

[반론 여지: 서로 다른 연산에 서로 다른 수학적 관례를 둘 수 있다. 하지만 그 경우도 이름과 문서가 있어야 하며 현재는 암묵적이다.]

[심각도: 중] · [분류: 정확성]

[근거: [fixed.h (line 14)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:14) ToInt(): raw = -1은 값으로는 -0.000015...인데 arithmetic shift면 -1을 반환한다. 좌표를 타일/셀 인덱스로 바꾸면 원점 바로 왼쪽의 극소 오차가 셀 -1로 튄다.]

[구체 수정 제안: ToIntFloor, ToIntTrunc, ToIntRound로 분리하고 ToInt() 같은 모호한 API는 금지. 헌법 문장: “정수 변환은 호출자가 rounding 목적을 이름으로 선택한다.”]

[반론 여지: 타일 좌표에는 floor가 필요할 수 있다. 그렇다면 이름이 ToIntFloor여야 한다.]

[심각도: 중] · [분류: 데싱크]

[근거: [fixed.h (line 14)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:14) ToInt()는 shift 후 int32_t 캐스팅한다. raw = 1LL << 62이면 raw >> 16 = 70368744177664, int32_t 범위 밖이다. signed narrowing 결과는 구현 정의.]

[구체 수정 제안: ToIntChecked()는 범위 밖이면 deterministic error. 또는 반환 타입을 int64_t로 바꾸고 int32 필요 지점에서 별도 checked cast.]

[반론 여지: ToInt가 시뮬레이션 상태에 아직 안 쓰인다. 그러나 경계/AI/타일링에 쓰이기 쉬운 API라 지금 계약을 닫아야 한다.]

[심각도: 중] · [분류: 정확성]

[근거: [fixed.h (line 15)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:15) ToFloat(): raw를 먼저 float로 변환한다. raw = 16777217이면 float mantissa 한계 때문에 16777216으로 반올림되어 1 raw LSB가 사라진다. raw = 9007199254740993 같은 큰 값은 훨씬 거칠게 양자화된다.]

[구체 수정 제안: ToFloat는 렌더/로그 전용으로만 허용하고 Sim 상태 계산 입력으로 사용 금지. 가능하면 이름을 ToFloatForDebugRenderOnly 수준으로 노골화하거나 core 타입에서 제거하고 경계 어댑터에 둔다.]

[반론 여지: 현재 C++ core 안에서는 ToFloat 미사용. 하지만 API가 타입에 붙어 있으면 향후 “잠깐 계산”에 쓰일 유혹이 크다.]

[심각도: 상] · [분류: 설계]

[근거: [sim_core.h (line 21)](/C:/Workspace/4XSubCulture/SimCore/SimCore/sim_core.h:21), [sim_core.cpp (line 56)](/C:/Workspace/4XSubCulture/SimCore/SimCore/sim_core.cpp:56) sim_spawn(Sim*, int64_t posx_raw, int64_t velx_raw)가 외부 raw를 무검증으로 FromRaw 주입한다. 예: posx_raw = INT64_MAX, velx_raw = 1은 다음 tick에서 즉시 UB. velx_raw = INT64_MAX도 한 tick만에 상태 불능.]

[구체 수정 제안: C ABI 입력은 sim_spawn_checked처럼 성공/실패를 반환하고 raw 허용 범위를 제한한다. 헌법 문장: “외부 raw는 신뢰하지 않는다. core 진입 시 Fixed 도메인 범위와 future operation headroom을 검증한다.”]

[반론 여지: 테스트 코드만 호출한다. 하지만 ABI는 엔진/네트워크/툴이 만지는 public boundary다.]

[심각도: 중] · [분류: 설계]

[근거: [sim_core.h (line 21)](/C:/Workspace/4XSubCulture/SimCore/SimCore/sim_core.h:21), [Assets SimCoreTest.cs (line 63)](/C:/Workspace/4XSubCulture/Assets/Scripts/Game/Core/SimCoreTest.cs:63) C# 쪽이 const long ONE = 65536을 하드코딩한다. fixed.h의 SHIFT = 16과 ABI 경계의 스케일 계약이 중복되어 있다.]

[구체 수정 제안: C ABI에 sim_fixed_shift()/sim_fixed_one()/sim_fixed_version()을 노출하거나 public C header에 상수 계약을 둔다. 규칙: “엔진 바인딩은 fixed scale을 복제 정의하지 않는다.”]

[반론 여지: SHIFT는 안 바꿀 것이다. 그러면 더더욱 ABI 상수로 고정 선언해야 한다.]

[심각도: 중] · [분류: 데싱크]

[근거: [Assets SimCoreTest.cs (line 31)](/C:/Workspace/4XSubCulture/Assets/Scripts/Game/Core/SimCoreTest.cs:31) tick driver가 Time.deltaTime float 누적으로 sim_tick 호출 횟수를 결정한다. Fixed 내부 산술은 같아도 각 클라이언트가 다른 횟수로 tick하면 상태가 갈라진다.]

[구체 수정 제안: 락스텝 모드에서는 렌더 delta float가 아니라 네트워크/정수 tick clock으로 core를 구동한다. ToFloat/Unity float 값은 표시 전용 단방향이어야 한다.]

[반론 여지: 이 파일은 테스트 MonoBehaviour이고 core 산술 자체는 아니다. 맞다. 하지만 C ABI 경계에서 float가 Sim 진행으로 영향을 주는 실제 예시라 위험 패턴으로 금지해야 한다.]

[심각도: 하] · [분류: 설계]

[근거: [fixed.h (line 6)](/C:/Workspace/4XSubCulture/SimCore/SimCore/fixed.h:6) int64_t raw가 public이다. 아무 코드나 fixed.raw = invalid로 불변식을 우회할 수 있다. FromRaw도 public이라 동일.]

[구체 수정 제안: raw 직접 쓰기는 serialization/ABI 전용 함수로 제한하고, 내부 연산용 생성자는 checked 경로를 기본으로 둔다. 최소한 규칙: “raw 직접 대입은 C ABI decode와 deterministic replay load에서만 허용.”]

[반론 여지: 작은 POD라 public raw가 편하다. 편의성은 인정되지만 overflow headroom/도메인 검증을 포기하는 비용이 있다.]

고정소수점 산술 규칙(헌법 조항)
모든 Fixed 연산은 UB 금지: signed overflow, signed negative shift, division by zero, out-of-range cast를 발생시키지 않는다.
곱셈/나눗셈 중간값 폭은 최소 128비트로 고정하고, 최종 raw 범위 초과 시 deterministic overflow 정책을 따른다.
rounding convention은 하나로 문서화한다: floor, toward zero, nearest 중 선택. 변환 API 이름에 정책을 포함한다.
0 분모 정책은 명시한다: deterministic fatal, command reject, 또는 지정 sentinel 중 하나. 암묵적 CPU trap 금지.
C ABI로 들어오는 raw는 검증한다. scale, 허용 범위, future tick headroom을 만족하지 않으면 실패를 반환한다.
float/double은 core 상태 계산에 역류 금지. 렌더/로그/디버그 출력 전용.
외부 바인딩은 65536을 하드코딩하지 않는다. ABI가 SHIFT/ONE/version을 단일 출처로 제공한다.
ToInt 같은 모호한 변환명 금지. ToIntFloor, ToIntTruncChecked처럼 목적과 실패 정책을 드러낸다.
치명적 결함 TOP 3
[fixed.h:18-21] signed overflow/shift/divide-by-zero UB가 연산자 4개 전반에 있어 플랫폼 무관 비트 동일성을 깨뜨린다.
[fixed.h:20-21] 곱셈/나눗셈 중간값이 64비트라 정상 게임 범위처럼 보이는 값도 쉽게 폭발한다.
[sim_core.h:21], [sim_core.cpp:56-57] C ABI가 raw를 무검증 주입해 한 tick 뒤 UB를 만들 수 있다.


---

## 2. 내 판단 — 채택 / 기각

| # | 지적 요약 | 심각도 | 분류(데싱크/정확성/설계) | 판단(채택/기각) | 사유 | 후속 조치 |
|---|---|---|---|---|---|---|
| 1 |  |  |  |  |  |  |
| 2 |  |  |  |  |  |  |
| 3 |  |  |  |  |  |  |

---

## 3. 기대 답안 대비 3분할 (검증의 검증)
> 앞서 세운 기대 답안(1-A 곱셈 오버플로 / 1-C 음수 floor=결정성≠정확성 / 3-A float 경계노출 / 2-A 헌법에 고정소수점 규칙 부재)과 대조.

- **놓침 (기대했으나 Gemini가 못 잡음)**:
  -
- **새로 찾음 (기대 답안에 없던 유효 지적)**:
  -
- **틀린 지적 (Gemini가 잘못 짚음 — 내가 반박)**:
  -

---

## 4. 결론 / 헌법·코드 반영 항목
- [ ] Harness §2에 "고정소수점 산술 규칙" 신설 여부:
- [ ] `fixed.h` 수정 TODO:
- [ ] 포트폴리오 §2 교차검증 문구를 A안(구체 1건+이 기록 링크)으로 승급:
