#pragma once
#include <stdint.h>

#ifdef  __cplusplus
extern "C" {
#endif //  __cplusplus

#ifdef SIMCORE_EXPORTS
#define SIMCORE_API __declspec(dllexport)
#else
#define SIMCORE_API __declspec(dllimport)
#endif

	//불투명 핸들 - 엔진 내부를 모르는 상태로 포인터만 들고 다님
	typedef struct Sim Sim;

	SIMCORE_API Sim* sim_create(void);
	SIMCORE_API void sim_destroy(Sim* sim);
	SIMCORE_API void sim_tick(Sim* sim);
	SIMCORE_API uint64_t sim_current_tick(const Sim* sim);
	SIMCORE_API uint64_t sim_spawn(Sim* sim, int64_t posx_raw, int64_t velx_raw);
	SIMCORE_API int32_t  sim_unit_count(const Sim* sim);
	SIMCORE_API uint64_t sim_get_unit_simid(const Sim* sim, int32_t index);
	SIMCORE_API int64_t  sim_get_unit_posx_raw(const Sim* sim, int32_t index);
	SIMCORE_API int64_t  sim_get_posx_raw_by_id(const Sim* sim, uint64_t sim_id);

#ifdef __cplusplus
}
#endif // __cplusplus