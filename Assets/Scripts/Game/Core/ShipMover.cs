using Unity.Entities;
using Unity.Mathematics;

namespace Game.Core
{
    public class ShipMover : IComponentData
    {
        public float3 velocity; // 1틱에 움직일 양
    }
}
