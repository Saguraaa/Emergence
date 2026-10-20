using Unity.Collections;
using Unity.Entities;

namespace Emergence.Gameplay.Save
{
    public struct LevelProgressComponent : IComponentData
    {
        public FixedString512Bytes Level;
        public bool IsLoading;
        public bool ResumeAtRespawn;
    }
}
