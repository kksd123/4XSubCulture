#include "pch.h"
#define SIMCORE_EXPORTS
#include "sim_core.h"
#include "fixed.h"

struct Sim
{
	uint64_t currentTick;
	Fixed posX;
	Fixed velX;
};

extern "C"
{
	SIMCORE_API Sim* sim_create(void)
	{
		Sim* sim = new Sim();
		sim->currentTick = 0;
		sim->posX = Fixed::FromInt(0);
		sim->velX = Fixed::FromRaw(Fixed::ONE / 4); //0.25 / 틱
		return sim;
	}

	SIMCORE_API void sim_destroy(Sim* sim)
	{
		delete sim;
	}

	SIMCORE_API void sim_tick(Sim* sim)
	{
		sim->currentTick++;
		sim->posX = sim->posX + sim->velX;
	}

	SIMCORE_API uint64_t sim_current_tick(const Sim* sim)
	{
		return sim->currentTick;
	}

	SIMCORE_API int64_t sim_get_posx_raw(const Sim* sim)
	{
		return sim->posX.raw;
	}
}