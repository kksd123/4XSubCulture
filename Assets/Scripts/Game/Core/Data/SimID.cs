using Unity.Entities;

namespace Game.Core.Data
{
    public struct SimIDComponent : IComponentData
    {
        public ulong Value;
    }
}
