using Emergence.Gameplay.Environment;
using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Player
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class PlayerMovementSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTag>();
            RequireForUpdate<EnvironmentSettings>();
        }

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            var environment = SystemAPI.GetSingleton<EnvironmentSettings>();
            foreach (var (position, motion, input, speed, form) in
                     SystemAPI.Query<RefRW<PlayerPosition>, RefRW<PlayerMotionState>,
                         RefRW<PlayerInput>, RefRO<PlayerMoveSpeed>, RefRO<PlayerForm>>().WithAll<PlayerTag>())
            {
                float2 direction = input.ValueRO.Move;
                float length = math.length(direction);
                if (length > 1) direction /= length;

                float2 velocity;
                //不同形态的速度选择
                switch (form.ValueRO.Value)
                {
                    case PlayerFormKind.Gas:
                        velocity = direction * speed.ValueRO.Gas + environment.WindVelocity.xy;
                        break;
                    case PlayerFormKind.Liquid:
                        velocity = direction * speed.ValueRO.Liquid;
                        break;
                    default:
                        float2 current = motion.ValueRO.Velocity.xy;
                        float currentLength = math.length(current);
                        if (currentLength > speed.ValueRO.Solid)
                            current *= speed.ValueRO.Solid / currentLength;
                        float2 target = direction * speed.ValueRO.Solid;
                        float2 change = target - current;
                        float changeLength = math.length(change);
                        float maxChange = speed.ValueRO.Solid / speed.ValueRO.SolidResponseTime * dt;
                        velocity = changeLength <= maxChange
                            ? target : current + change / changeLength * maxChange;
                        break;
                }
                //移动逻辑
                position.ValueRW.XY += velocity * dt;

                float height = position.ValueRO.Height;
                float vertical;
                if (form.ValueRO.Value == PlayerFormKind.Gas)
                {
                    vertical = environment.WindVelocity.z;
                    height += vertical * dt;
                }
                else
                {
                    // 自由落体
                    float previous = math.min(0, motion.ValueRO.Velocity.z);
                    height += previous * dt - 0.5f * environment.Gravity * dt * dt;
                    vertical = previous - environment.Gravity * dt;
                }
                if (height <= 0)
                {
                    height = 0;
                    vertical = 0;
                }
                position.ValueRW.Height = height;
                motion.ValueRW.Velocity = new float3(velocity, vertical);
                input.ValueRW.InteractPressed = false;
            }
        }
    }
}
