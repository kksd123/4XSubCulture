using Unity.Entities;

namespace Game.Core
{
    public struct SimClock : IComponentData, IBCSingleton
    {
        public ulong currentTick;
        public bool isPaused;
        public int gameSpeedX10;
    }
}
