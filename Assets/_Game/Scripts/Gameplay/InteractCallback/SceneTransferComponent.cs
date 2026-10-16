using Unity.Collections;
using Unity.Entities;

namespace Emergence.Gameplay.InteractCallback
{
    // 请求只记录目的地；玩家实体及其状态始终留在同一个 World 中。
    public struct SceneTransferComponent : IComponentData
    {
        public FixedString512Bytes DestinationScene;
        public FixedString64Bytes DestinationPoint;
    }
}
