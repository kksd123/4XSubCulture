using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Game.Core.Data;
using Unity.Transforms;
using Unity.Mathematics;

namespace Game.Core
{
    public class SimTickDriver : BCMonoSingleton<SimTickDriver>
    {
        private void Start()
        {
            if (m_initialized == false)
                Initialize();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (!m_initialized) return;
            if (m_world == null || !m_world.IsCreated) return;

            var em = m_world.EntityManager;

            var reg = em.GetComponentData<SimIdRegistry>(m_registryEntity);
            if (reg.map.IsCreated)
                reg.map.Dispose();
        }

        private void Update()
        {
            if (!m_initialized)
                return;

            var em = m_world.EntityManager;

            m_accumulator += Time.deltaTime * 1000f;

            int ticksThisFrame = 0;

            float intervalMs = 0f;

            while (ticksThisFrame < m_maxTicksPerFrame)
            {
                var clock = em.GetComponentData<SimClock>(m_clockEntity);

                intervalMs = m_baseTickIntervalMs / (clock.gameSpeedX10 / 10f);

                if (m_accumulator < intervalMs)
                    break;

                clock.currentTick += 1;
                em.SetComponentData(m_clockEntity, clock);

                m_simRoot.Update();

                m_accumulator -= intervalMs;
                ticksThisFrame++;
            }
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

            m_simRoot = m_world.GetOrCreateSystemManaged<SimRootGroup>();

            AddGroup<CommandConsumeGroup>();
            AddGroup<CombatGroup>();
            AddGroup<FleetMoveGroup>();
            AddGroup<VisionGroup>();
            AddGroup<AISnapshotGroup>();
            AddGroup<GameDataGroup>();
            AddGroup<StoryGroup>();
            AddGroup<DiplomacyGroup>();
            AddGroup<ChecksumGroup>();
            AddGroup<RenderSnapshotGroup>();

            AddSystem<HeartBeatSystem, RenderSnapshotGroup>();

            m_simRoot.SortSystems();

            SpawnShips();

            m_initialized = true;
        }

        private void AddGroup<TGroup>() where TGroup : ComponentSystemGroup
        {
            var group = m_world.GetOrCreateSystemManaged<TGroup>();
            m_simRoot.AddSystemToUpdateList(group);
        }

        private void AddSystem<TSystem, TGroup>()
            where TSystem : unmanaged, ISystem
            where TGroup : ComponentSystemGroup
        {
            var group = m_world.GetOrCreateSystemManaged<TGroup>();
            group.AddSystemToUpdateList(m_world.GetOrCreateSystem<TSystem>());
        }

        private void SpawnShips()
        {
            var em = m_world.EntityManager;

            var reg = em.GetComponentData<SimIdRegistry>(m_registryEntity);

            for(int i = 0; i < 3; i++)
            {
                var e = em.CreateEntity();

                ulong simId = reg.Register(e);
                em.AddComponentData(e, new SimIDComponent { Value = simId});

                em.AddComponentData(e, LocalTransform.FromPosition(new float3(i * 2f, 0f, 0f)));
                em.AddComponentData(e, new ShipMover { velocity = new float3(1f, 0f, 0f)});
            }

            em.SetComponentData(m_registryEntity, reg);
        }

        private World m_world;
        private Entity m_clockEntity;
        private Entity m_registryEntity;
        private bool m_initialized = false;
        private float m_accumulator;
        private SimRootGroup m_simRoot;

        [SerializeField]
        private float m_baseTickIntervalMs = 100f;
        [SerializeField]
        private int m_maxTicksPerFrame = 5;

        private const int mc_mapInitCapacity = 1024; // 일단 예상 가능한 최대 동시 유닛 수 
    }
}
