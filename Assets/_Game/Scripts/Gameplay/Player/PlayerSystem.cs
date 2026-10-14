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

        // 仅在切换入口校验枚举值；读取形态直接读取 Component。
        public static void SetForm(EntityManager manager, Entity player, PlayerFormKind form)
        {
            if (!Enum.IsDefined(typeof(PlayerFormKind), form))
                throw new ArgumentOutOfRangeException(nameof(form));
            manager.SetComponentData(player, new PlayerFormComponent { Value = form });
        }

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            var environment = SystemAPI.GetSingleton<EnvironmentComponent>();
            // 先读取共用状态，再按形态读取对应参数；每个玩家每步只积分一次。
            foreach (var (position, motion, input, form, entity) in
                     SystemAPI.Query<RefRW<PlayerPositionComponent>, RefRW<PlayerMotionComponent>,
                         RefRW<PlayerInputComponent>, RefRO<PlayerFormComponent>>()
                         .WithAll<PlayerTagComponent>().WithEntityAccess())
            {
                float2 direction = Direction(input.ValueRO.Move);
                float2 velocity;
                switch (form.ValueRO.Value)
                {
                    // 固体移动逻辑
                    case PlayerFormKind.Solid:
                        var solid = SystemAPI.GetComponent<PlayerSolidComponent>(entity);
                        float magnitude = solid.MoveVelocityMagnitude;
                        float2 current = motion.ValueRO.Velocity.xy;
                        float length = math.length(current);
                        if (length > magnitude) current *= magnitude / length;
                        float2 target = direction * magnitude;
                        float2 change = target - current;
                        float changeLength = math.length(change);
                        float maxChange = magnitude / solid.ResponseTime * dt;
                        velocity = changeLength <= maxChange ? target : current + change / changeLength * maxChange;
                        break;
                    // 液体移动逻辑
                    case PlayerFormKind.Liquid:
                        var liquid = SystemAPI.GetComponent<PlayerLiquidComponent>(entity);
                        velocity = direction * liquid.MoveVelocityMagnitude;
                        break;
                    // 气体移动逻辑
                    case PlayerFormKind.Gas:
                        var gas = SystemAPI.GetComponent<PlayerGasComponent>(entity);
                        velocity = direction * gas.MoveVelocityMagnitude + environment.WindVelocity.xy;
                        break;
                    default:
                        throw new InvalidOperationException("PlayerFormComponent contains an unsupported form.");
                }
                Integrate(ref position.ValueRW, ref motion.ValueRW, velocity,
                    form.ValueRO.Value == PlayerFormKind.Gas, environment, dt);
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
            float verticalVelocity;
            if (gas)
            {
                verticalVelocity = environment.WindVelocity.z;
                position.Height += verticalVelocity * dt;
            }
            else
            {
                // 从气态切出时取消向上漂移，已有下落运动继续继承。
                float previous = math.min(0, motion.Velocity.z);
                position.Height += previous * dt - 0.5f * environment.Gravity * dt * dt;
                verticalVelocity = previous - environment.Gravity * dt;
            }
            if (position.Height <= 0)
            {
                position.Height = 0;
                verticalVelocity = 0;
            }
            motion.Velocity = new float3(velocity, verticalVelocity);
        }
    }
}
