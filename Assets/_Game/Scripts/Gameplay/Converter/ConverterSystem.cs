using System;
using Emergence.Gameplay.Player;
using Unity.Entities;

namespace Emergence.Gameplay.Converter
{
    public static class ConverterSystem
    {
        public static bool TryConvert(EntityManager manager, Entity player, ConverterKind kind)
        {
            PlayerFormKind source;
            PlayerFormKind target;
            switch (kind)
            {
                case ConverterKind.SolidToLiquid:
                    source = PlayerFormKind.Solid;
                    target = PlayerFormKind.Liquid;
                    break;
                case ConverterKind.LiquidToGas:
                    source = PlayerFormKind.Liquid;
                    target = PlayerFormKind.Gas;
                    break;
                case ConverterKind.GasToLiquid:
                    source = PlayerFormKind.Gas;
                    target = PlayerFormKind.Liquid;
                    break;
                case ConverterKind.LiquidToSolid:
                    source = PlayerFormKind.Liquid;
                    target = PlayerFormKind.Solid;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (manager.HasComponent<DeadTagComponent>(player) && manager.IsComponentEnabled<DeadTagComponent>(player))
                return false;
            if (manager.GetComponentData<PlayerFormComponent>(player).Value != source) return false;
            PlayerSystem.SetForm(manager, player, target);
            return true;
        }
    }
}
