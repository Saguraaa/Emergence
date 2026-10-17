using System;
using Emergence.Gameplay.Environment;
using Emergence.Gameplay.NPC;
using Emergence.Gameplay.Physics;
using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Player
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class PlayerSystem : SystemBase
    {
        private PhysicsSystem physics;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTagComponent>();
            RequireForUpdate<EnvironmentComponent>();
        }

        protected override void OnStartRunning()
        {
            // 校验
            var resolvedPhysics = World.GetExistingSystemManaged<PhysicsSystem>();
            if (resolvedPhysics == null) throw new InvalidOperationException("PhysicsSystem must be initialized before player movement.");

            // 初始化
            physics = resolvedPhysics;
        }

        public static void Die(EntityManager manager, Entity player) =>
            manager.SetComponentEnabled<DeadTagComponent>(player, true);

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
            bool sideView = environment.MovementMode == MapMovementMode.SideView;
            // 先读取共用状态，再按形态读取对应参数；每个玩家每步只积分一次。
            foreach (var (position, motion, input, form, entity) in
                     SystemAPI.Query<RefRW<PlayerPositionComponent>, RefRW<PlayerMotionComponent>,
                         RefRW<PlayerInputComponent>, RefRO<PlayerFormComponent>>()
                         .WithAll<PlayerTagComponent>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<DeadTagComponent>(entity) && SystemAPI.IsComponentEnabled<DeadTagComponent>(entity)) continue;
                float2 direction = sideView
                    ? new float2(math.clamp(input.ValueRO.Move.x, -1, 1), 0)
                    : Direction(input.ValueRO.Move);
                float2 velocity;
                switch (form.ValueRO.Value)
                {
                    // 固体移动逻辑
                    case PlayerFormKind.Solid:
                        var solid = SystemAPI.GetComponent<PlayerSolidComponent>(entity);
                        float magnitude = solid.MoveVelocityMagnitude;
                        float2 current = motion.ValueRO.Velocity.xy;
                        if (sideView) current.y = 0;
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
                float2 displacement = velocity * dt;
                float height = position.ValueRO.Height;
                float heightDisplacement;
                float verticalVelocity;
                if (sideView)
                {
                    // 侧视模式中 Collider 随 XY.y 下落，独立 Height 不再参与移动。
                    height = 0;
                    heightDisplacement = 0;
                    verticalVelocity = 0;
                    if (form.ValueRO.Value != PlayerFormKind.Gas)
                    {
                        float previous = math.min(0, motion.ValueRO.Velocity.y);
                        displacement.y = previous * dt - .5f * environment.Gravity * dt * dt;
                        velocity.y = previous - environment.Gravity * dt;
                    }
                }
                else
                {
                    var desiredPosition = position.ValueRO;
                    var desiredMotion = motion.ValueRO;
                    Integrate(ref desiredPosition, ref desiredMotion, velocity,
                        form.ValueRO.Value == PlayerFormKind.Gas, environment, dt);
                    heightDisplacement = desiredPosition.Height - height;
                    verticalVelocity = desiredMotion.Velocity.z;
                }
                var movement = physics.Move(entity, position.ValueRO.XY, displacement, velocity, (int)form.ValueRO.Value,
                    height, heightDisplacement, verticalVelocity, environment.MaximumHeight);
                position.ValueRW.XY += movement.Displacement;
                position.ValueRW.Height = movement.Height;
                motion.ValueRW.Velocity = new float3(movement.Velocity, movement.VerticalVelocity);
            }
            ApplyDamageFields(dt);
        }

        public void ApplyDamageFields(float deltaTime)
        {
            foreach (var (resistance, position) in
                     SystemAPI.Query<RefRW<NPCResistanceComponent>, RefRO<NPCPositionComponent>>().WithAll<NPCTagComponent>())
            {
                float reduction = 0;
                foreach (var (field, center) in
                         SystemAPI.Query<RefRO<DamageFieldComponent>, RefRO<PlayerPositionComponent>>()
                             .WithAll<PlayerTagComponent>().WithDisabled<DeadTagComponent>())
                {
                    // 伤害场属于玩家，按地面位置衰减；显示按键和表现高度不参与玩法判断。
                    float distance = math.distance(position.ValueRO.XY, center.ValueRO.XY);
                    reduction += field.ValueRO.Strength * math.saturate(1 - distance / field.ValueRO.Radius) * deltaTime;
                }
                resistance.ValueRW.Value = math.max(0, resistance.ValueRO.Value - reduction);
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
