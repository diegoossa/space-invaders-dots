using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.Jobs;
using CustomAttributeNamedArgument = Mono.Cecil.CustomAttributeNamedArgument;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Unity.Entities.CodeGen
{
    static class EntitiesForEachJobCreator
    {
        public static (TypeDefinition newJobStruct, MethodDefinition scheduleTimeInitializeMethod3, MethodDefinition
            runWithoutJobSystemMethod, FieldDefinition runWithoutJobSystemDelegateFieldBurst, FieldDefinition
            runWithoutJobSystemDelegateFieldNoBurst) CreateNewJobStruct(MethodDefinition methodToAnalyze,
                LambdaJobDescriptionConstruction lambdaJobDescriptionConstruction,
                MethodDefinition displayClassExecuteMethod, bool isCapturingLambda, bool allowReferenceTypes,
                LambdaJobsPostProcessor.ExecutionMode executionMode, bool usesBurst)
        {
            var moduleDefinition = methodToAnalyze.Module;

            var typeAttributes = TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed |
                                 TypeAttributes.AnsiClass | TypeAttributes.SequentialLayout |
                                 TypeAttributes.NestedPrivate;

            if (methodToAnalyze.DeclaringType.NestedTypes.Any(t => t.Name == lambdaJobDescriptionConstruction.Name))
            {
                UserError.DC0003(lambdaJobDescriptionConstruction.Name, methodToAnalyze, lambdaJobDescriptionConstruction.ScheduleOrRunInvocationInstruction).Throw();
            }

            var newJobStruct = new TypeDefinition(methodToAnalyze.DeclaringType.Namespace,lambdaJobDescriptionConstruction.Name, typeAttributes,moduleDefinition.ImportReference(typeof(ValueType)))
            {
                DeclaringType = methodToAnalyze.DeclaringType
            };
            methodToAnalyze.DeclaringType.NestedTypes.Add(newJobStruct);

            Type InterfaceTypeFor(LambdaJobDescriptionKind lambdaJobDescriptionKind)
            {
                switch (lambdaJobDescriptionKind)
                {
                    case LambdaJobDescriptionKind.Entities:
                    case LambdaJobDescriptionKind.Chunk:
                        return typeof(IJobChunk);
                    case LambdaJobDescriptionKind.Job:
                        return typeof(IJob);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            newJobStruct.Interfaces.Add(new InterfaceImplementation(moduleDefinition.ImportReference(InterfaceTypeFor(lambdaJobDescriptionConstruction.Kind))));

            var displayClassExecuteMethodAndItsLocalMethods = CecilHelpers.FindUsedInstanceMethodsOnSameType(displayClassExecuteMethod).Prepend(displayClassExecuteMethod).ToList();

            var allUsedParametersOfEntitiesForEachInvocations = displayClassExecuteMethodAndItsLocalMethods.SelectMany(
                    m =>
                        m.Body.Instructions.Where(IsChunkEntitiesForEachInvocation).Select(i =>
                            (m,
                                LambdaJobsPostProcessor.AnalyzeForEachInvocationInstruction(m, i)
                                    .displayClassExecuteMethod)))
                .SelectMany(m_and_dem => m_and_dem.displayClassExecuteMethod.Parameters.Select(p => (m_and_dem.m, p)))
                .ToArray();

            ElementProviderInformations elementProviderInformations = null;
            switch (lambdaJobDescriptionConstruction.Kind)
            {
                case LambdaJobDescriptionKind.Entities:
                    elementProviderInformations = ElementProviderInformations.For(displayClassExecuteMethod.Parameters.Select(p => (displayClassExecuteMethod, p)).ToArray(), newJobStruct);
                    break;
                case LambdaJobDescriptionKind.Job:
                    break;
                case LambdaJobDescriptionKind.Chunk:
                    elementProviderInformations = ElementProviderInformations.For(allUsedParametersOfEntitiesForEachInvocations, newJobStruct);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var (clonedMethods, readFromDisplayClassMethod, displayClassFieldToJobStructField) = DuplicateDisplayClassExecuteAndItsLocalMethods(methodToAnalyze, isCapturingLambda, newJobStruct, displayClassExecuteMethodAndItsLocalMethods,elementProviderInformations,allowReferenceTypes);

            foreach (var (methodName, attributeType) in ConstructionMethodsThatCorrespondToFieldAttributes)
            {
                foreach (var constructionMethod in
                    lambdaJobDescriptionConstruction.InvokedConstructionMethods.Where(m =>
                        m.MethodName == methodName))
                {
                    if (constructionMethod.Arguments.Single() is FieldDefinition fieldDefinition)
                    {
                        var correspondingJobField = displayClassFieldToJobStructField[fieldDefinition.FullName].newField;
                        correspondingJobField.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(attributeType.GetConstructor(Array.Empty<Type>()))));
                        continue;
                    }

                    UserError.DC0012(methodToAnalyze, constructionMethod).Throw();
                }
            }
            
            var clonedDisplayClassExecuteMethod = clonedMethods.First();

            MethodDefinition runWithoutJobSystemMethod = null;
            FieldDefinition runWithoutJobSystemDelegateFieldBurst = null;
            FieldDefinition runWithoutJobSystemDelegateFieldNoBurst = null;
            if (executionMode == LambdaJobsPostProcessor.ExecutionMode.Run && lambdaJobDescriptionConstruction.Kind != LambdaJobDescriptionKind.Job)
            {
                runWithoutJobSystemMethod = CreateRunWithoutJobSystemMethod(newJobStruct);
                runWithoutJobSystemDelegateFieldNoBurst = new FieldDefinition("s_RunWithoutJobSystemDelegateFieldNoBurst", FieldAttributes.Static,moduleDefinition.ImportReference(typeof(InternalCompilerInterface.JobChunkRunWithoutJobSystemDelegate)));
                newJobStruct.Fields.Add(runWithoutJobSystemDelegateFieldNoBurst);

                if (usesBurst)
                {
                    runWithoutJobSystemDelegateFieldBurst = new FieldDefinition("s_RunWithoutJobSystemDelegateFieldBurst", FieldAttributes.Static,moduleDefinition.ImportReference(typeof(InternalCompilerInterface.JobChunkRunWithoutJobSystemDelegate)));
                    newJobStruct.Fields.Add(runWithoutJobSystemDelegateFieldBurst);
                }
            }
            
            ApplyBurstAttributeIfRequired(newJobStruct, runWithoutJobSystemMethod, lambdaJobDescriptionConstruction);
            
            switch (lambdaJobDescriptionConstruction.Kind)
            {
                case LambdaJobDescriptionKind.Entities:
                {
                    MakeExecuteMethod_Entities(elementProviderInformations, clonedDisplayClassExecuteMethod, newJobStruct);
                    var scheduleTimeInitializeMethod = MakeScheduleTimeInitializeMethod(readFromDisplayClassMethod, moduleDefinition, elementProviderInformations);
                    newJobStruct.Methods.Add(scheduleTimeInitializeMethod);
                    return (newJobStruct, scheduleTimeInitializeMethod, runWithoutJobSystemMethod, runWithoutJobSystemDelegateFieldBurst,runWithoutJobSystemDelegateFieldNoBurst);
                }
                case LambdaJobDescriptionKind.Job:
                    newJobStruct.Methods.Add(MakeExecuteMethod_Job(clonedDisplayClassExecuteMethod));
                    var scheduleTimeInitializeMethod2 = MakeScheduleTimeInitializeMethod(readFromDisplayClassMethod, moduleDefinition, null);
                    newJobStruct.Methods.Add(scheduleTimeInitializeMethod2);
                    return (newJobStruct, scheduleTimeInitializeMethod2, runWithoutJobSystemMethod, runWithoutJobSystemDelegateFieldBurst,runWithoutJobSystemDelegateFieldNoBurst);

                case LambdaJobDescriptionKind.Chunk:
                {
                    MakeExecuteMethod_Chunk(clonedDisplayClassExecuteMethod,elementProviderInformations, newJobStruct);
                    var scheduleTimeInitializeMethod3 = MakeScheduleTimeInitializeMethod(readFromDisplayClassMethod, moduleDefinition, elementProviderInformations);
                    newJobStruct.Methods.Add(scheduleTimeInitializeMethod3);
                    return (newJobStruct, scheduleTimeInitializeMethod3, runWithoutJobSystemMethod, runWithoutJobSystemDelegateFieldBurst,runWithoutJobSystemDelegateFieldNoBurst);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            
        }

        private static MethodDefinition CreateRunWithoutJobSystemMethod(TypeDefinition newJobStruct)
        {
            var moduleDefinition = newJobStruct.Module;
            var result =
                new MethodDefinition("RunWithoutJobSystem", MethodAttributes.Public | MethodAttributes.Static, moduleDefinition.TypeSystem.Void)
                {
                    HasThis = false,
                    Parameters =
                    {
                        new ParameterDefinition("archetypeChunkIterator", ParameterAttributes.None,new PointerType(moduleDefinition.ImportReference(typeof(ArchetypeChunkIterator)))),
                        new ParameterDefinition("jobData", ParameterAttributes.None,new PointerType(moduleDefinition.TypeSystem.Void)),
                    },
                };
            newJobStruct.Methods.Add(result);

            var ilProcessor = result.Body.GetILProcessor();

            ilProcessor.Emit(OpCodes.Ldarg_1);
            ilProcessor.Emit(OpCodes.Call,moduleDefinition.ImportReference(typeof(UnsafeUtilityEx).GetMethod(nameof(UnsafeUtilityEx.AsRef), BindingFlags.Public | BindingFlags.Static)).MakeGenericInstanceMethod(newJobStruct));

            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Call,moduleDefinition.ImportReference(typeof(JobChunkExtensions).GetMethod(nameof(JobChunkExtensions.RunWithoutJobs), BindingFlags.Public | BindingFlags.Static)).MakeGenericInstanceMethod(newJobStruct));
            ilProcessor.Emit(OpCodes.Ret);
            return result;
        }

        private static (MethodDefinition[] clonedMethods, MethodDefinition readFromDisplayClassMethod, Dictionary<string, (FieldDefinition oldField, FieldDefinition newField)> displayClassFieldToJobStructField)
            DuplicateDisplayClassExecuteAndItsLocalMethods(MethodDefinition methodToAnalyze, bool isCapturingLambda,
                TypeDefinition targetType, List<MethodDefinition> displayClassExecuteMethodAndItsLocalMethods,
                ElementProviderInformations providerInformations, bool allowReferenceTypes)
        {
            VerifyClosureFunctionDoesNotWriteToCapturedVariable(displayClassExecuteMethodAndItsLocalMethods);

            MethodDefinition readFromDisplayClassMethod = null;
            Dictionary<string, (FieldDefinition oldField, FieldDefinition newField)> displayClassFieldToJobStructField =
                new Dictionary<string, (FieldDefinition oldField, FieldDefinition newField)>();
            if (isCapturingLambda)
            {
                readFromDisplayClassMethod = InjectReadFromDisplayClassMethod(targetType,
                    out displayClassFieldToJobStructField, displayClassExecuteMethodAndItsLocalMethods);
            }

            var clonedMethods = CecilHelpers.CloneClosureExecuteMethodAndItsLocalFunctions(displayClassFieldToJobStructField,displayClassExecuteMethodAndItsLocalMethods, targetType, "OriginalLambdaBody");

            ApplyPostProcessingOnJobCode(clonedMethods, providerInformations);

            foreach (var kvp in displayClassFieldToJobStructField.Values)
            {
                var field = kvp.oldField;
                var typeDefinition = field.FieldType.Resolve();
                if (typeDefinition.TypeReferenceEquals(methodToAnalyze.DeclaringType))
                {
                    var thisLoadingInstruction = displayClassExecuteMethodAndItsLocalMethods
                        .SelectMany(m => m.Body.Instructions).FirstOrDefault(i =>
                            i.Operand is FieldReference fr && fr.FieldType.TypeReferenceEquals(typeDefinition));
                    if (thisLoadingInstruction != null)
                    {
                        var next = thisLoadingInstruction.Next;
                        if (next.Operand is FieldReference fr) UserError.DC0001(methodToAnalyze, next, fr).Throw();
                    }
                }

                if (typeDefinition.IsDelegate())
                    continue;
                
                if (!typeDefinition.IsValueType && !allowReferenceTypes)
                {
                    var illegalInvocation = displayClassExecuteMethodAndItsLocalMethods.SelectMany(m => m.Body.Instructions)
                        .FirstOrDefault(i =>
                            i.Operand is MethodReference mr &&
                            mr.DeclaringType.TypeReferenceEquals(methodToAnalyze.DeclaringType) && mr.HasThis);
                    if (illegalInvocation != null)
                    {
                        var mr = (MethodReference) illegalInvocation.Operand;

                        UserError.DC0002(methodToAnalyze, illegalInvocation, mr).Throw();
                    }
                    
                    UserError.DC0004(methodToAnalyze, clonedMethods.First().Body.Instructions.First(), field).Throw();
                }
            }

            return (clonedMethods, readFromDisplayClassMethod, displayClassFieldToJobStructField);
        }

        
        static bool IsChunkEntitiesForEachInvocation(Instruction instruction)
        {
            if (!(instruction.Operand is MethodReference mr)) 
                return false;
            return mr.Name == nameof(LambdaJobChunkDescriptionConstructionMethods.ForEach) && mr.DeclaringType.Name == nameof(LambdaForEachDescriptionConstructionMethods);
        }

        private static void ApplyPostProcessingOnJobCode(MethodDefinition[] methodUsedByLambdaJobs,
            ElementProviderInformations elementProviderInformations)
        {
            var forEachInvocations = new List<(MethodDefinition, Instruction)>();
            var methodDefinition = methodUsedByLambdaJobs.First();
            forEachInvocations.AddRange(methodDefinition.Body.Instructions.Where(IsChunkEntitiesForEachInvocation).Select(i => (methodDefinition, i)));

            foreach (var methodUsedByLambdaJob in methodUsedByLambdaJobs)
            {
                var methodBody = methodUsedByLambdaJob.Body;

                var displayClassVariable = methodBody.Variables.SingleOrDefault(v => v.VariableType.Name.Contains("DisplayClass"));
                if (displayClassVariable != null)
                {
                    TypeDefinition displayClass = displayClassVariable.VariableType.Resolve();
                    bool allDelegatesAreGuaranteedNotToOutliveMethod =
                        displayClass.IsValueType ||
                        CecilHelpers.AllDelegatesAreGuaranteedNotToOutliveMethodFor(methodUsedByLambdaJob);

                    if (!displayClass.IsValueType && allDelegatesAreGuaranteedNotToOutliveMethod)
                    {
                        CecilHelpers.PatchMethodThatUsedDisplayClassToTreatItAsAStruct(methodBody,
                            displayClassVariable, displayClass);
                        CecilHelpers.PatchDisplayClassToBeAStruct(displayClass);
                    }
                }
            }

            int counter = 1;
            foreach (var (methodUsedByLambdaJob, instruction) in forEachInvocations)
            {
                var methodBody = methodUsedByLambdaJob.Body;
                var (ldFtn, newObj) = FindClosureCreatingInstructions(methodBody, instruction);
                var displayClassExecuteMethod = ((MethodReference) ldFtn.Operand).Resolve();

                var newType = new TypeDefinition("", "InlineEntitiesForEachInvocation" + counter++, TypeAttributes.NestedPublic | TypeAttributes.SequentialLayout,methodUsedByLambdaJob.Module.ImportReference(typeof(ValueType)))
                {
                    DeclaringType = methodUsedByLambdaJob.DeclaringType
                };
                methodUsedByLambdaJob.DeclaringType.NestedTypes.Add(newType);
                
                var result = DuplicateDisplayClassExecuteAndItsLocalMethods(methodUsedByLambdaJob, true, newType, CecilHelpers.FindUsedInstanceMethodsOnSameType(displayClassExecuteMethod).Prepend(displayClassExecuteMethod).ToList(), elementProviderInformations, false);

                var iterateEntitiesMethod = CreateIterateEntitiesMethod(elementProviderInformations, result.clonedMethods.First(), newType);
                
                var variable = new VariableDefinition(newType);
                methodBody.Variables.Add(variable);

                ldFtn.Previous.MakeNOP();
                ldFtn.MakeNOP();
                newObj.OpCode = OpCodes.Ldnull;
                newObj.Operand = null;

                var displayClassVariable = methodBody.Variables.SingleOrDefault(v => v.VariableType.Name.Contains("DisplayClass"));
                if (displayClassVariable == null) 
                    continue;
                var ilProcessor = methodBody.GetILProcessor();
                       
                ilProcessor.InsertAfter(instruction, new List<Instruction>
                {
                    //no need to drop the delegate from the stack, because we just removed the function that placed it on the stack in the first place.
                    //do not drop the description from the stack, as the original method returns it, and we want to maintain stack behaviour.
                  
                    //call our new method
                    Instruction.Create(OpCodes.Ldloca, variable),
                    Instruction.Create(OpCodes.Initobj, newType),

                    Instruction.Create(OpCodes.Ldloca, variable),
                    Instruction.Create(OpCodes.Ldloca, displayClassVariable),
                    Instruction.Create(OpCodes.Call, result.readFromDisplayClassMethod),
                    
                    
                    Instruction.Create(OpCodes.Ldloca, variable),
                    Instruction.Create(OpCodes.Ldarga,methodBody.Method.Parameters.First(p=>p.ParameterType.Name == nameof(ArchetypeChunk))),
                    Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Ldfld,elementProviderInformations._runtimesField),
                    Instruction.Create(OpCodes.Call, iterateEntitiesMethod),
                });
                
#if ENABLE_DOTS_COMPILER_CHUNKS
                var chunkEntitiesInvocation = LambdaJobDescriptionConstruction.FindInstructionThatPushedArg(methodBody.Method, 0, instruction);
                if (chunkEntitiesInvocation.Operand is MethodReference mr && mr.Name == "get_"+nameof(ArchetypeChunk.Entities) && mr.DeclaringType.Name == nameof(ArchetypeChunk))
                    CecilHelpers.EraseMethodInvocationFromInstructions(ilProcessor, chunkEntitiesInvocation);
#endif
                
                CecilHelpers.EraseMethodInvocationFromInstructions(ilProcessor, instruction);
            }
        }


        private static MethodDefinition MakeScheduleTimeInitializeMethod(MethodDefinition readFromDisplayClassMethod, ModuleDefinition moduleDefinition, ElementProviderInformations elementProviderInformations)
        {
            var scheduleTimeInitializeMethod =
                new MethodDefinition("ScheduleTimeInitialize", MethodAttributes.Public, moduleDefinition.TypeSystem.Void)
                {
                    HasThis = true,
                    Parameters =
                    {
                        new ParameterDefinition("jobComponentSystem", ParameterAttributes.None,
                            moduleDefinition.ImportReference(typeof(JobComponentSystem))),
                    },
                };

            if (readFromDisplayClassMethod != null)
                scheduleTimeInitializeMethod.Parameters.Add(readFromDisplayClassMethod.Parameters.Last());

            elementProviderInformations?.EmitInvocationToScheduleTimeInitializeIntoJobChunkScheduleTimeInitialize(scheduleTimeInitializeMethod);
            
            var scheduleIL = scheduleTimeInitializeMethod.Body.GetILProcessor();

            if (readFromDisplayClassMethod != null)
            {
                scheduleIL.Emit(OpCodes.Ldarg_0);
                scheduleIL.Emit(OpCodes.Ldarg_2);
                scheduleIL.Emit(OpCodes.Call, readFromDisplayClassMethod);
            }

            scheduleIL.Emit(OpCodes.Ret);
            return scheduleTimeInitializeMethod;
        }

        private static MethodDefinition MakeExecuteMethod_Job(MethodDefinition originalLambdaFunction)
        {
            var executeMethod = CreateMethodImplementingInterfaceMethod(originalLambdaFunction.Module, typeof(IJob).GetMethod(nameof(IJob.Execute)));

            var executeIL = executeMethod.Body.GetILProcessor();
            executeIL.Emit(OpCodes.Ldarg_0);
            executeIL.Emit(OpCodes.Call, originalLambdaFunction);
            executeIL.Emit(OpCodes.Ret);
            return executeMethod;
        }
        
        
        private static MethodDefinition MakeExecuteMethod_Chunk(MethodDefinition originalLambdaFunction,
            ElementProviderInformations elementProviderInformations, TypeDefinition newJobStruct)
        {
            var executeMethod = CreateMethodImplementingInterfaceMethod(originalLambdaFunction.Module, typeof(IJobChunk).GetMethod(nameof(IJobChunk.Execute)));
            newJobStruct.Methods.Add(executeMethod);
            elementProviderInformations.EmitInvocationToPrepareToRunOnEntitiesInIntoJobChunkExecute(executeMethod);
            
            var executeIL = executeMethod.Body.GetILProcessor();

            executeIL.Emit(OpCodes.Ldarg_0);
            executeIL.Emit(OpCodes.Ldarg_1);
            executeIL.Emit(OpCodes.Ldarg_2);
            executeIL.Emit(OpCodes.Ldarg_3);
            executeIL.Emit(OpCodes.Call, originalLambdaFunction);
            executeIL.Emit(OpCodes.Ret);
            return executeMethod;
        }

        private static void MakeExecuteMethod_Entities(ElementProviderInformations providerInformations,MethodDefinition userExecuteDefinition, TypeDefinition newJobStruct)
        {
            var moduleDefinition = userExecuteDefinition.Module;
            
            var executeMethod = CreateMethodImplementingInterfaceMethod(moduleDefinition,typeof(IJobChunk).GetMethod(nameof(IJobChunk.Execute)));
            
            newJobStruct.Methods.Add(executeMethod);

            providerInformations.EmitInvocationToPrepareToRunOnEntitiesInIntoJobChunkExecute(executeMethod);
            
            var ilProcessor = executeMethod.Body.GetILProcessor();
            
            var iterateOnEntitiesMethod = CreateIterateEntitiesMethod(providerInformations, userExecuteDefinition, newJobStruct);

            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Ldarga,1);
            
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Ldfld,providerInformations._runtimesField);
            
            ilProcessor.Emit(OpCodes.Call, iterateOnEntitiesMethod);
       
            ilProcessor.Emit(OpCodes.Ret);
        }

        static MethodDefinition CreateIterateEntitiesMethod(ElementProviderInformations elementProviderInformations, MethodDefinition clonedUserExecuteDefinition, TypeDefinition targetType)
        {
            var moduleDefinition = targetType.Module;
            var iterateEntitiesMethod = new MethodDefinition("IterateEntities", MethodAttributes.Public,moduleDefinition.TypeSystem.Void)
            {
                Parameters =
                {
                    new ParameterDefinition("chunk", ParameterAttributes.None, new ByReferenceType(moduleDefinition.ImportReference(typeof(ArchetypeChunk)))),
                    new ParameterDefinition("runtimes", ParameterAttributes.None, new ByReferenceType(elementProviderInformations.RuntimesType)),
                }
            };
            
            targetType.Methods.Add(iterateEntitiesMethod);

            var ilProcessor = iterateEntitiesMethod.Body.GetILProcessor();

            var loopTerminator = new VariableDefinition(moduleDefinition.TypeSystem.Int32);
            iterateEntitiesMethod.Body.Variables.Add(loopTerminator);
            ilProcessor.Emit(OpCodes.Ldarg_1);
            ilProcessor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetMethod("get_"+nameof(ArchetypeChunk.Count))));
            ilProcessor.Emit(OpCodes.Stloc, loopTerminator);

            var loopCounter = new VariableDefinition(moduleDefinition.TypeSystem.Int32);
            iterateEntitiesMethod.Body.Variables.Add(loopCounter);
            ilProcessor.Emit(OpCodes.Ldc_I4_0);
            ilProcessor.Emit(OpCodes.Stloc, loopCounter);

            var beginLoopInstruction = Instruction.Create(OpCodes.Ldloc, loopCounter);
            ilProcessor.Append(beginLoopInstruction);
            ilProcessor.Emit(OpCodes.Ldloc, loopTerminator);
            ilProcessor.Emit(OpCodes.Ceq);

            var exitDestination = Instruction.Create(OpCodes.Nop);
            ilProcessor.Emit(OpCodes.Brtrue, exitDestination);

            ilProcessor.Emit(OpCodes.Ldarg_0);
            foreach (var parameterDefinition in clonedUserExecuteDefinition.Parameters)
                elementProviderInformations.EmitILToLoadValueForParameterOnStack(parameterDefinition, ilProcessor,loopCounter);

            ilProcessor.Emit(OpCodes.Call, clonedUserExecuteDefinition);

            ilProcessor.Emit(OpCodes.Ldloc, loopCounter);
            ilProcessor.Emit(OpCodes.Ldc_I4_1);
            ilProcessor.Emit(OpCodes.Add);
            ilProcessor.Emit(OpCodes.Stloc, loopCounter);

            ilProcessor.Emit(OpCodes.Br, beginLoopInstruction);
            ilProcessor.Append(exitDestination);
            ilProcessor.Emit(OpCodes.Ret);
            return iterateEntitiesMethod;
        }

        private static MethodDefinition CreateMethodImplementingInterfaceMethod(ModuleDefinition moduleDefinition, MethodInfo interfaceMethod)
        {
            var interfaceMethodReference = moduleDefinition.ImportReference(interfaceMethod);
            var newMethod = new MethodDefinition(interfaceMethodReference.Name,
                MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final | MethodAttributes.Public |
                MethodAttributes.HideBySig,
                interfaceMethodReference.ReturnType);

            int index = 0;
            foreach (var pd in interfaceMethodReference.Parameters)
            {
                var pdName = pd.Name;
                if (pdName.Length == 0)
                    pdName = interfaceMethod.GetParameters()[index].Name;
                newMethod.Parameters.Add(new ParameterDefinition(pdName, pd.Attributes,
                    moduleDefinition.ImportReference(pd.ParameterType)));
                index++;
            }

            return newMethod;
        }

        private static void ApplyBurstAttributeIfRequired(TypeDefinition newJobStruct,
            MethodDefinition runWithoutJobSystemMethod,
            LambdaJobDescriptionConstruction lambdaJobDescriptionConstruction)
        {
            var useBurstMethod = lambdaJobDescriptionConstruction.InvokedConstructionMethods.FirstOrDefault(m =>m.MethodName == nameof(LambdaJobDescriptionConstructionMethods.WithBurst));

            var module = newJobStruct.Module;
            var burstCompileAttributeConstructor = AttributeConstructorReferenceFor(typeof(BurstCompileAttribute), module);

            CustomAttributeNamedArgument CustomAttributeNamedArgumentFor(string name, Type type, object value)
            {
                return new CustomAttributeNamedArgument(name,
                    new CustomAttributeArgument(module.ImportReference(type), value));
            }

            var item = new CustomAttribute(burstCompileAttributeConstructor);

            if (useBurstMethod != null && useBurstMethod.Arguments.Length == 4)
            {
                item.Properties.Add(CustomAttributeNamedArgumentFor(nameof(BurstCompileAttribute.FloatMode),typeof(FloatMode), useBurstMethod.Arguments[1]));
                item.Properties.Add(CustomAttributeNamedArgumentFor(nameof(BurstCompileAttribute.FloatPrecision),typeof(FloatPrecision), useBurstMethod.Arguments[2]));
                item.Properties.Add(CustomAttributeNamedArgumentFor(nameof(BurstCompileAttribute.CompileSynchronously),typeof(bool), useBurstMethod.Arguments[3]));
            }
            
            newJobStruct.CustomAttributes.Add(item);
            runWithoutJobSystemMethod?.CustomAttributes.Add(item);
        }

        private static MethodDefinition InjectReadFromDisplayClassMethod(TypeDefinition newJobStruct,
            out Dictionary<string, (FieldDefinition oldField, FieldDefinition newField)> displayClassFieldToJobStructField, IEnumerable<MethodDefinition> methodsThatCanReadFromDisplayClass)
        {
            displayClassFieldToJobStructField = new Dictionary<string, (FieldDefinition oldField, FieldDefinition newField)>();

            var displayClass = methodsThatCanReadFromDisplayClass.First().DeclaringType;
            
            var displayClassFieldsUsedByMethod = 
                methodsThatCanReadFromDisplayClass.SelectMany(method=>method.Body.Instructions)
                .Select(i => i.Operand)
                .OfType<FieldReference>()
                .Where(fr => fr.DeclaringType.TypeReferenceEquals(displayClass))
                .Select(fr => fr.Resolve())
                .Distinct()
                .ToArray();

            foreach (var field in displayClassFieldsUsedByMethod)
            {
                var newField = new FieldDefinition(field.Name, field.Attributes, field.FieldType);
                newJobStruct.Fields.Add(newField);
                displayClassFieldToJobStructField.Add(field.FullName, (field, newField));
            }

            return AddMethodToTransferFieldsWithDisplayClass(displayClassFieldToJobStructField,newJobStruct, "ReadFromDisplayClass", TransferDirection.DisplayClassToJob, displayClass);
        }

        private static MethodDefinition AddMethodToTransferFieldsWithDisplayClass(Dictionary<string, (FieldDefinition oldField, FieldDefinition newField)> displayClassFieldToJobStructField,
            TypeDefinition newJobStruct, string methodName, TransferDirection direction, TypeReference displayClassTypeReference)
        {
            var method =new MethodDefinition(methodName, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot, newJobStruct.Module.TypeSystem.Void);
            var parameterType = displayClassTypeReference.IsValueType
                ? new ByReferenceType(displayClassTypeReference)
                : displayClassTypeReference;
            method.Parameters.Add(new ParameterDefinition("displayClass", ParameterAttributes.None,parameterType));

            var ilProcessor = method.Body.GetILProcessor();
            foreach (var (oldField, newField) in displayClassFieldToJobStructField.Values)
            {
                if (direction == TransferDirection.DisplayClassToJob)
                {
                    ilProcessor.Emit(OpCodes.Ldarg_0);
                    ilProcessor.Emit(OpCodes.Ldarg_1);
                    ilProcessor.Emit(OpCodes.Ldfld, oldField); //load field from displayClassVariable
                    ilProcessor.Emit(OpCodes.Stfld, newField); //store that value in corresponding field in newJobStruct
                }
                else
                {
                    ilProcessor.Emit(OpCodes.Ldarg_1);
                    ilProcessor.Emit(OpCodes.Ldarg_0);
                    ilProcessor.Emit(OpCodes.Ldfld, newField); //load field from job
                    ilProcessor.Emit(OpCodes.Stfld, oldField); //store that value in corresponding field in displayclass
                }
            }

            ilProcessor.Emit(OpCodes.Ret);
            newJobStruct.Methods.Add(method);
            return method;
        }

        public static (Instruction, Instruction) FindClosureCreatingInstructions(MethodBody body, Instruction callInstruction)
        {
            body.EnsurePreviousAndNextAreSet();
            var cursor = callInstruction;
            while (cursor != null)
            {
                if ((cursor.OpCode == OpCodes.Ldftn) && cursor.Next?.OpCode == OpCodes.Newobj)
                {
                    return (cursor, cursor.Next);
                }

                cursor = cursor.Previous;
            }

            InternalCompilerError.DCICE002(body.Method, callInstruction).Throw();
            return (null,null);
        }

        static readonly List<(string methodName, Type attributeType)> ConstructionMethodsThatCorrespondToFieldAttributes = new List<(string, Type)>
        {
            (nameof(LambdaJobDescriptionConstructionMethods.WithReadOnly), typeof(ReadOnlyAttribute)),
            (nameof(LambdaJobDescriptionConstructionMethods.WithWriteOnly), typeof(WriteOnlyAttribute)),
            (nameof(LambdaJobDescriptionConstructionMethods.WithDeallocateOnJobCompletion), typeof(DeallocateOnJobCompletionAttribute)),
            (nameof(LambdaJobDescriptionConstructionMethods.WithNativeDisableContainerSafetyRestriction), typeof(NativeDisableContainerSafetyRestrictionAttribute)),
            (nameof(LambdaJobDescriptionConstructionMethods.WithNativeDisableUnsafePtrRestriction), typeof(NativeDisableUnsafePtrRestrictionAttribute)), 
            (nameof(LambdaJobDescriptionConstructionMethods.WithNativeDisableParallelForRestriction), typeof(NativeDisableParallelForRestrictionAttribute)),
        };

        enum TransferDirection
        {
            DisplayClassToJob,
        }

        public static MethodReference AttributeConstructorReferenceFor(Type attributeType, ModuleDefinition module)
        {
            return module.ImportReference(attributeType.GetConstructors().Single(c=>!c.GetParameters().Any()));
        }
        
        private static void VerifyClosureFunctionDoesNotWriteToCapturedVariable(IEnumerable<MethodDefinition> methods)
        {
            foreach (var method in methods)
            {
                var typeDefinitionFullName = method.DeclaringType.FullName;

                var badInstructions = method.Body.Instructions.Where(i =>
                {
                    if (i.OpCode != OpCodes.Stfld)
                        return false;
                    return ((FieldReference) i.Operand).DeclaringType.FullName == typeDefinitionFullName;
                });

                var first = badInstructions.FirstOrDefault();
                if (first == null)
                    continue;

                UserError.DC0013(((FieldReference) first.Operand), method, first).Throw();
            }
        }
    }
}