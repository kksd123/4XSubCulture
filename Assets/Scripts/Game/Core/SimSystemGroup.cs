using Unity.Entities;

namespace Game.Core
{
    [DisableAutoCreation]
    public partial class SimRootGroup : ComponentSystemGroup { }

    /// <summary>
    /// 실행순서 : 상위가 가장 우선순위 이후 순서대로 아래 순위
    /// </summary>
    
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimRootGroup))]
    public partial class CommandConsumeGroup : ComponentSystemGroup { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimRootGroup))]
    [UpdateAfter(typeof(CommandConsumeGroup))]
    public partial class CombatGroup : ComponentSystemGroup { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimRootGroup))]
    [UpdateAfter(typeof(CombatGroup))]
    public partial class FleetMoveGroup : ComponentSystemGroup { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimRootGroup))]
    [UpdateAfter(typeof(FleetMoveGroup))]
    public partial class VisionGroup : ComponentSystemGroup { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimRootGroup))]
    [UpdateAfter(typeof(VisionGroup))]
    public partial class AISnapshotGroup : ComponentSystemGroup { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimRootGroup))]
    [UpdateAfter(typeof(AISnapshotGroup))]
    public partial class GameDataGroup : ComponentSystemGroup { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimRootGroup))]
    [UpdateAfter(typeof(GameDataGroup))]
    public partial class StoryGroup : ComponentSystemGroup { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimRootGroup))]
    [UpdateAfter(typeof(StoryGroup))]
    public partial class DiplomacyGroup : ComponentSystemGroup { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimRootGroup))]
    [UpdateAfter(typeof(DiplomacyGroup))]
    public partial class ChecksumGroup : ComponentSystemGroup { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimRootGroup))]
    [UpdateAfter(typeof(ChecksumGroup))]
    public partial class RenderSnapshotGroup : ComponentSystemGroup { }
}
