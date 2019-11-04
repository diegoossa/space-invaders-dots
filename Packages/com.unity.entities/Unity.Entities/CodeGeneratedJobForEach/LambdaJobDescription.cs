using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using AllowMultipleInvocationsAttribute = Unity.Entities.LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute;
namespace Unity.Entities.CodeGeneratedJobForEach
{
    public interface ILambdaJobDescription
    {
    }
    
    public interface ISupportForEachWithUniversalDelegate
    {
    }
    
    public struct ForEachLambdaJobDescription : ILambdaJobDescription, ISupportForEachWithUniversalDelegate
    {
    }

    public struct LambdaSingleJobDescription : ILambdaJobDescription
    {
    }

    public struct LambdaJobChunkDescription : ILambdaJobDescription
    {
    }
}

namespace Unity.Entities
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal class AllowDynamicValueAttribute : Attribute
    {
    }

    public static class LambdaJobDescriptionConstructionMethods
    {
        [AttributeUsage(AttributeTargets.Method)]
        internal class AllowMultipleInvocationsAttribute : Attribute
        {
        }

        public static TDescription WithBurst<TDescription>(this TDescription description, bool enabled = true, FloatMode floatMode = FloatMode.Default, FloatPrecision floatPrecision = FloatPrecision.Standard, bool synchronousCompilation = false) where TDescription : ILambdaJobDescription => description;
        public static TDescription WithName<TDescription>(this TDescription description, string name) where TDescription : ILambdaJobDescription => description;
        
        [AllowMultipleInvocations]
        public static TDescription WithReadOnly<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static TDescription WithWriteOnly<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static TDescription WithDeallocateOnJobCompletion<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static TDescription WithNativeDisableContainerSafetyRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static TDescription WithNativeDisableUnsafePtrRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [Obsolete("Use WithNativeDisableUnsafePtrRestriction instead", true)] //<-- remove soon, never shipped, only used in a2-dots-shooter
        public static TDescription WithNativeDisableUnsafePtrRestrictionAttribute<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static TDescription WithNativeDisableParallelForRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;

        //do not remove this obsolete method. It is not really obsolete, it never existed, but it is created to give a better error message for when you try to use .Schedule() without argument.  Without this method signature,
        //c#'s overload resolution will try to match a completely different Schedule extension method, and explain why that one doesn't work, which results in an error message that sends the user in a wrong direction.
        [Obsolete("You must provide a JobHandle argument to .Schedule()", true)]
        public static JobHandle Schedule<TDescription>(this TDescription description) where TDescription : ILambdaJobDescription => ThrowCodeGenException();
        
        public static JobHandle Schedule<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaJobDescription => ThrowCodeGenException();
        
        public static void Run<TDescription>(this TDescription description) where TDescription : ILambdaJobDescription => ThrowCodeGenException();

        static JobHandle ThrowCodeGenException() => throw new Exception("This method should have been replaced by codegen");
    }

    public static class LambdaSimpleJobDescriptionConstructionMethods
    {
        public static LambdaSingleJobDescription WithCode(this LambdaSingleJobDescription description,  [AllowDynamicValue] Action code) =>description;
    }
    
    public static class LambdaJobChunkDescriptionConstructionMethods
    {
        public delegate void JobChunkDelegate(ArchetypeChunk chunk, int chunkIndex, int queryIndexOfFirstEntityInChunk);
        public static LambdaJobChunkDescription ForEach(this LambdaJobChunkDescription description,  [AllowDynamicValue] JobChunkDelegate code) =>description;
    }
    
    public static class LambdaJobChunkDescription_SetSharedComponent
    {
        public static LambdaJobChunkDescription SetSharedComponentFilterOnQuery<T>(LambdaJobChunkDescription description, T sharedComponent, EntityQuery query) where T : struct, ISharedComponentData
        {
            query.SetSharedComponentFilter(sharedComponent);
            return description;
        }
    }
    
    public static class ForEachLambdaJobDescription_SetSharedComponent
    {
        public static ForEachLambdaJobDescription SetSharedComponentFilterOnQuery<T>(ForEachLambdaJobDescription description, T sharedComponent, EntityQuery query) where T : struct, ISharedComponentData
        {
            query.SetSharedComponentFilter(sharedComponent);
            return description;
        }
    }
    
    public static partial class LambdaForEachDescriptionConstructionMethods
    {
        static TDescription ThrowCodeGenException<TDescription>() => throw new Exception("This method should have been replaced by codegen");
    }

#if ENABLE_DOTS_COMPILER    
    public static class InternalCompilerInterface
    {
        public static JobChunkRunWithoutJobSystemDelegate BurstCompile(JobChunkRunWithoutJobSystemDelegate d) => BurstCompiler.CompileFunctionPointer(d).Invoke;
        public unsafe delegate void JobChunkRunWithoutJobSystemDelegate(ArchetypeChunkIterator* iterator, void* job);
        public static unsafe void RunJobChunk<T>(T jobData, EntityQuery query, JobChunkRunWithoutJobSystemDelegate functionPointer) where T : struct, IJobChunk
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!JobsUtility.JobDebuggerEnabled)
#endif
            {
                var myIterator = query.GetArchetypeChunkIterator();
                functionPointer(&myIterator, UnsafeUtility.AddressOf(ref jobData));
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            else
                JobChunkExtensions.Run(jobData, query );
#endif
        }
    }
#endif    
}