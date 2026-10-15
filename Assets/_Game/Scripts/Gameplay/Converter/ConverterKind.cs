using UnityEngine;

namespace Emergence.Gameplay.Converter
{
    public enum ConverterKind
    {
        [InspectorName("固态 → 液态")] SolidToLiquid,
        [InspectorName("液态 → 气态")] LiquidToGas,
        [InspectorName("气态 → 液态")] GasToLiquid,
        [InspectorName("液态 → 固态")] LiquidToSolid
    }
}
