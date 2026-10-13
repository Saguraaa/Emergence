using System;
using Emergence.Gameplay.Environment;
using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Player
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class PlayerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTagComponent>();
            RequireForUpdate<EnvironmentComponent>();
        }

        // 统一切换入口：调用方不得分别修改启用位，避免一帧执行多个形态的运动。
        public static void SetForm(EntityManager manager, Entity player, PlayerFormKind form)
        {
            if (form != PlayerFormKind.Solid && form != PlayerFormKind.Liquid && form != PlayerFormKind.Gas)
                throw new ArgumentOutOfRangeException(nameof(form));
            manager.SetComponentEnabled<PlayerSolidComponent>(player, form == PlayerFormKind.Solid);
            manager.SetComponentEnabled<PlayerLiquidComponent>(player, form == PlayerFormKind.Liquid);
            manager.SetComponentEnabled<PlayerGasComponent>(player, form == PlayerFormKind.Gas);
        }

        public static PlayerFormKind GetForm(EntityManager manager, Entity player)
        {
            bool solid = manager.IsComponentEnabled<PlayerSolidComponent>(player);
            bool liquid = manager.IsComponentEnabled<PlayerLiquidComponent>(player);
            bool gas = manager.IsComponentEnabled<PlayerGasComponent>(player);
            if ((solid ? 1 : 0) + (liquid ? 1 : 0) + (gas ? 1 : 0) != 1)
                throw new InvalidOperationException("Player must have exactly one enabled form component.");
            return solid ? PlayerFormKind.Solid : liquid ? PlayerFormKind.Liquid : PlayerFormKind.Gas;
        }

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            var environment = SystemAPI.GetSingleton<EnvironmentComponent>();
            // 普通查询跳过禁用形态，各分支只读取对应形态参数。
            foreach (var (position, motion, input, solid) in
                     SystemAPI.Query<RefRW<PlayerPositionComponent>, RefRW<PlayerMotionComponent>,
                         RefRW<PlayerInputComponent>, RefRO<PlayerSolidComponent>>().WithAll<PlayerTagComponent>())
            {
                float magnitude = solid.ValueRO.MoveVelocityMagnitude;
                float2 current = motion.ValueRO.Velocity.xy;
                float length = math.length(current);
                if (length > magnitude) current *= magnitude / length;
                float2 target = Direction(input.ValueRO.Move) * magnitude;
                float2 change = target - current;
                float changeLength = math.length(change);
                float maxChange = magnitude / solid.ValueRO.ResponseTime * dt;
                float2 velocity = changeLength <= maxChange ? target : current + change / changeLength * maxChange;
                Integrate(ref position.ValueRW, ref motion.ValueRW, velocity, false, environment, dt);
                input.ValueRW.InteractPressed = false;
            }

            foreach (var (position, motion, input, liquid) in
                     SystemAPI.Query<RefRW<PlayerPositionComponent>, RefRW<PlayerMotionComponent>,
                         RefRW<PlayerInputComponent>, RefRO<PlayerLiquidComponent>>().WithAll<PlayerTagComponent>())
            {
                float2 velocity = Direction(input.ValueRO.Move) * liquid.ValueRO.MoveVelocityMagnitude;
                Integrate(ref position.ValueRW, ref motion.ValueRW, velocity, false, environment, dt);
                input.ValueRW.InteractPressed = false;
            }

            foreach (var (position, motion, input, gas) in
                     SystemAPI.Query<RefRW<PlayerPositionComponent>, RefRW<PlayerMotionComponent>,
                         RefRW<PlayerInputComponent>, RefRO<PlayerGasComponent>>().WithAll<PlayerTagComponent>())
            {
                float2 velocity = Direction(input.ValueRO.Move) * gas.ValueRO.MoveVelocityMagnitude + environment.WindVelocity.xy;
                Integrate(ref position.ValueRW, ref motion.ValueRW, velocity, true, environment, dt);
                input.ValueRW.InteractPressed = false;
            }
        }

        private static float2 Direction(float2 input)
        {
            float length = math.length(input);
            return length > 1 ? input / length : input;
        }

        private static void Integrate(ref PlayerPositionComponent position, ref PlayerMotionComponent motion,
            float2 velocity, bool gas, EnvironmentComponent environment, float dt)
        {
            position.XY += velocity * dt;
            float vertical;
            if (gas)
            {
                vertical = environment.WindVelocity.z;
                position.Height += vertical * dt;
            }
            else
            {
                // 从气态切出时取消向上漂移，已有下落运动继续继承。
                float previous = math.min(0, motion.Velocity.z);
                position.Height += previous * dt - 0.5f * environment.Gravity * dt * dt;
                vertical = previous - environment.Gravity * dt;
            }
            if (position.Height <= 0)
            {
                position.Height = 0;
                vertical = 0;
            }
            motion.Velocity = new float3(velocity, vertical);
        }
    }
}
