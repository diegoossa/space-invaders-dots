using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public class EnemyMovementSystem : JobComponentSystem
{
    private static float movementDirection = 1f;
    private static bool isAdvancing = false;
    private static float timeAdvancing = 5f;

    struct EnemyMovementSystemJob : IJobForEach<Translation, Enemy, MovementSpeed, Boundaries>
    {
        public float deltaTime;

        public void Execute(ref Translation translation, [ReadOnly] ref Enemy enemy, [ReadOnly] ref MovementSpeed movementSpeed, [ReadOnly] ref Boundaries boundaries)
        {
            var xTranslation = translation.Value.x;
            var yTranslation = translation.Value.y;
            
            if (!isAdvancing)
            {
                if (translation.Value.x > boundaries.Right)
                {
                    xTranslation = boundaries.Right;
                    movementDirection = -1;
                    isAdvancing = true;
                }
                else if (translation.Value.x < boundaries.Left)
                {
                    xTranslation = boundaries.Left;
                    movementDirection = 1;
                    isAdvancing = true;
                }
                xTranslation += movementSpeed.Value * movementDirection * deltaTime;
            }
            else
            {
                yTranslation -= movementSpeed.Value * deltaTime;
                timeAdvancing -= deltaTime;
                if (timeAdvancing < 0)
                {
                    timeAdvancing = 5f;
                    isAdvancing = false;
                }
            }
            
            translation.Value = new float3(xTranslation, yTranslation, 0);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var job = new EnemyMovementSystemJob
        {
            deltaTime = Time.deltaTime,
        };

        return job.Schedule(this, inputDependencies);
    }
}