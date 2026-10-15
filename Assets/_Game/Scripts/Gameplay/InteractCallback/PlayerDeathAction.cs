using Unity.Entities;
using UnityEngine;
using Emergence.Gameplay.Player;

namespace Emergence.Gameplay.InteractCallback
{
    // 独立的 UnityEvent 入口，不缓存玩家状态或承担逐帧控制。
    public sealed class PlayerDeathAction : MonoBehaviour
    {
        public void Die()
        {
            var manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var query = manager.CreateEntityQuery(typeof(PlayerTagComponent));
            PlayerSystem.Die(manager, query.GetSingletonEntity());
        }
    }
}
