#include "pch.h"
#define SIMCORE_EXPORTS
#include "sim_core.h"
#include "fixed.h"
#include <vector>
#include <unordered_map>

struct Unit
{
	uint64_t simId;
	Fixed posX;
	Fixed velX;
};

struct Sim
{
	uint64_t currentTick;
	uint64_t nextId;
	std::vector<Unit> units;
	std::unordered_map<uint64_t, size_t> idToIndex;
};

extern "C"
{
	SIMCORE_API Sim* sim_create(void)
	{
		Sim* sim = new Sim();
		sim->currentTick = 0;
		sim->nextId = 1;
		return sim;
	}

	SIMCORE_API void sim_destroy(Sim* sim)
	{
		delete sim;
	}

	SIMCORE_API void sim_tick(Sim* sim)
	{
		sim->currentTick++;
		for (int i = 0; i < sim->units.size(); i++)
		{
			sim->units[i].posX = sim->units[i].posX + sim->units[i].velX;
		}
	}

	SIMCORE_API uint64_t sim_current_tick(const Sim* sim)
	{
		return sim->currentTick;
	}

	SIMCORE_API uint64_t sim_spawn(Sim* sim, int64_t posx_raw, int64_t velx_raw)
	{
		Unit u;
		u.simId = sim->nextId++;
		u.posX = Fixed::FromRaw(posx_raw);
		u.velX = Fixed::FromRaw(velx_raw);

		size_t idx = sim->units.size();
		sim->units.push_back(u);
		sim->idToIndex[u.simId] = idx;
		return u.simId;
	}

	SIMCORE_API int32_t sim_unit_count(const Sim* sim)
	{
		return (int32_t)sim->units.size();
	}

	SIMCORE_API uint64_t sim_get_unit_simid(const Sim* sim, int32_t index)
	{
		if (index < 0 || index >= (int32_t)sim->units.size())
			return 0;

		return sim->units[index].simId;
	}

	SIMCORE_API int64_t sim_get_unit_posx_raw(const Sim* sim, int32_t index)
	{
		if (index < 0 || index >= (int32_t)sim->units.size())
			return 0;

		return sim->units[index].posX.raw;
	}

	SIMCORE_API int64_t sim_get_posx_raw_by_id(const Sim* sim, uint64_t sim_id)
	{
		auto it = sim->idToIndex.find(sim_id);
		if (it == sim->idToIndex.end())
			return 0;

		return sim->units[it->second].posX.raw;
	}
}