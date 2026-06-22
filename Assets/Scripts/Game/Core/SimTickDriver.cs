using UnityEngine;
using Unity.Collections;
using Unity.Entities;

namespace Game.Core
{
    public class SimTickDriver : BCMonoSingleton<SimTickDriver>
    {
        private void Start()
        {
            if (m_initialized == false)
                Initialize();
        }

        private void OnDestroy()
        {
            base.OnDestroy();

            if (!m_initialized) return;
            if (m_world == null || !m_world.IsCreated) return;

            var em = m_world.EntityManager;

            var reg = em.GetComponentData<SimIdRegistry>(m_registryEntity);
            if (reg.map.IsCreated)
                reg.map.Dispose();
        }

        public void Initialize()
        {
            if (m_initialized)
                return;

            m_world = World.DefaultGameObjectInjectionWorld;
            if(m_world == null)
            {
                Debug.LogError("[SimTickDriver] Default World가 없습니다. ECS 부트스트랩 확인하세요.");
            }

            var em = m_world.EntityManager;

            m_clockEntity = BCSingleton<SimClock>.Ensure(em, 
                new SimClock { currentTick = 0, isPaused = false, gameSpeedX10 = 10 });

            m_registryEntity = BCSingleton<SimIdRegistry>.Ensure(em, 
                new SimIdRegistry { map = new NativeHashMap<ulong, Entity>(mc_mapInitCapacity, Allocator.Persistent), nextId = 1 });

            m_initialized = true;
        }

        private World m_world;
        private Entity m_clockEntity;
        private Entity m_registryEntity;
        private bool m_initialized = false;

        private const int mc_mapInitCapacity = 1024; // 일단 예상 가능한 최대 동시 유닛 수 
    }
}
