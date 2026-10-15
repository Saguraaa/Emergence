using Emergence.Gameplay.Converter;
using Emergence.Gameplay.Player;
using Unity.Entities;
using UnityEngine;

namespace Emergence.Gameplay.InteractCallback.Converter
{
    // 独立的 UnityEvent 入口；转换条件和状态变更由 System 处理。
    public sealed class ConverterAction : MonoBehaviour
    {
        [SerializeField] private ConverterKind conversion;

        public void Convert()
        {
            var manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var query = manager.CreateEntityQuery(typeof(PlayerTagComponent));
            ConverterSystem.TryConvert(manager, query.GetSingletonEntity(), conversion);
        }
    }
}
