using Unity.Collections;
using Unity.Entities;

namespace Game.Core
{
    public struct SimIdRegistry : IComponentData, IBCSingleton
    {
        public NativeHashMap<ulong, Entity> map;
        public ulong nextId;

        // 순차부여 - 모든 클라이언트가 같은 순서로 호출해야 같은 ID로 나옴
        public ulong Register(Entity entity)
        {
            ulong id = nextId;
            nextId++;
            map[id] = entity;
            return id;
        }

        // 조회 - Foreach 사용 금지
        public bool TryGetEntity(ulong simId, out Entity entity)
        {
            return map.TryGetValue(simId, out entity);
        }

        public void UnRegister(ulong simId)
        {
            map.Remove(simId);
        }
    }
}
