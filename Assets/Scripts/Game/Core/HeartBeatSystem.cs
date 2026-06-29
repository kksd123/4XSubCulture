using Unity.Entities;
using UnityEngine;

namespace Game.Core
{
    [DisableAutoCreation]
    public partial struct HeartBeatSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimClock>();
        }

        public void OnUpdate(ref SystemState state)
        {
            ulong tick = SystemAPI.GetSingleton<SimClock>().currentTick;
            Debug.Log($"[Heartbeat] tick = {tick}");
        }
    }
}