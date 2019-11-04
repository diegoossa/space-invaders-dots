using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public class PlayerMovementSystem : JobComponentSystem
{
    [BurstCompile]
    struct PlayerMovementSystemJob : IJobForEach<Translation, Player, PlayerInput, MovementSpeed, Boundaries>
    {
        public float deltaTime;

        public void Execute(ref Translation translation, [ReadOnly] ref Player player,
            [ReadOnly] ref PlayerInput playerInput, [ReadOnly] ref MovementSpeed movementSpeed,
            [ReadOnly] ref Boundaries boundaries)
        {
            var xTranslation = translation.Value.x;
            xTranslation += movementSpeed.Value * playerInput.Horizontal * deltaTime;
            xTranslation = math.clamp(xTranslation, boundaries.Left, boundaries.Right);
            translation.Value = new float3(xTranslation, translation.Value.y, 0);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var job = new PlayerMovementSystemJob
        {
            deltaTime = Time.deltaTime,
        };

        return job.Schedule(this, inputDependencies);
    }
}