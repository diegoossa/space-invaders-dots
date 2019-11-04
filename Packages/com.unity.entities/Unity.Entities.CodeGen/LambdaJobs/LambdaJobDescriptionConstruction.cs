using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using MethodDefinition = Mono.Cecil.MethodDefinition;
using TypeReference = Mono.Cecil.TypeReference;

namespace Unity.Entities.CodeGen
{
    enum LambdaJobDescriptionKind
    {
        Entities,
        Job,
        Chunk
    }
    
    class LambdaJobDescriptionConstruction
    {
        public class InvokedConstructionMethod
        {
            public InvokedConstructionMethod(string methodName, TypeReference[] typeArguments, object[] arguments, Instruction instructionInvokingMethod, MethodDefinition containingMethod)
            {
                MethodName = methodName;
                TypeArguments = typeArguments;
                Arguments = arguments;
                InstructionInvokingMethod = instructionInvokingMethod;
                ContainingMethod = containingMethod;
            }

            public string MethodName { get; }
            public object[] Arguments { get; }
            public Instruction InstructionInvokingMethod { get; }
            public MethodDefinition ContainingMethod { get; }
            public TypeReference[] TypeArguments { get; }
        }
        
        public Instruction WithCodeInvocationInstruction;
        public List<InvokedConstructionMethod> InvokedConstructionMethods = new List<InvokedConstructionMethod>();
        public string Name;
        public Instruction ScheduleOrRunInvocationInstruction { get; set; }
        public LambdaJobDescriptionKind Kind;
        
        public Instruction ChainInitiatingInstruction { get; set; }

        public override string ToString()
        {
            var analysis = this;
            var sb = new StringBuilder();
            foreach (var m in analysis.InvokedConstructionMethods)
            {
                sb.Append($"{m.MethodName} ");

                foreach (var tp in m.TypeArguments)
                    sb.Append($"<{tp.Name}> ");
                
                foreach (var i in m.Arguments)
                    sb.Append(i + " ");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static Instruction FindInstructionThatPushedArg(MethodDefinition containingMethod, int argNumber,
            Instruction callInstructionsWhoseArgumentsWeWantToFind)
        {
            containingMethod.Body.EnsurePreviousAndNextAreSet();
            
            var cursor = callInstructionsWhoseArgumentsWeWantToFind.Previous;

            int stackSlotWhoseWriteWeAreLookingFor = argNumber;
            int stackSlotWhereNextPushWouldBeWrittenTo = InstructionExtensions.GetPopDelta(callInstructionsWhoseArgumentsWeWantToFind);

            var seenInstructions = new HashSet<Instruction>() {callInstructionsWhoseArgumentsWeWantToFind, cursor};
            
            while (cursor != null)
            {
                if (cursor.IsBranch())
                {
                    var target = (Instruction) cursor.Operand;
                    if (!seenInstructions.Contains(target))
                    {
                        if (IsUnsupportedBranch(cursor))
                            UserError.DC0010(containingMethod, cursor).Throw();        
                    }
                }
                
                var pushAmount = cursor.GetPushDelta();

                for (int i = 0; i != pushAmount; i++)
                {
                    stackSlotWhereNextPushWouldBeWrittenTo--;
                    if (stackSlotWhereNextPushWouldBeWrittenTo == stackSlotWhoseWriteWeAreLookingFor)
                        return cursor;
                }

                var popAmount = cursor.GetPopDelta();
                for (int i = 0; i != popAmount; i++)
                {
                    stackSlotWhereNextPushWouldBeWrittenTo++;
                }
                
                cursor = cursor.Previous;
                seenInstructions.Add(cursor);
            }

            return null;
        }

        private static bool IsUnsupportedBranch(Instruction cursor)
        {
            if (cursor.OpCode.FlowControl == FlowControl.Next) 
                return false;
            
            if (cursor.OpCode.FlowControl == FlowControl.Call)
                return false;

            //If we encounter any branches while walking up the instructions, that means there's some dynamic code in there. things like if something then something.
            //Since we need the entire chain of .With* calls to be analyzable statically, we will tell the user that they cannot do this.
            //Unfortunately there is one case where the roslyn compiler itself injects some branches.  If your lambda expression does not capture any variables,
            //roslyn makes an optimization where it will only generate a delegate once, and stores it in a static field somewhere. The code it generates basically does
            //if (myStaticFieldHoldingMyDelegate == null) myStaticFieldHoldingMyDelegate = MakeDelegate();
            //
            //We want to support that scenario, so we try to specifically detect this pattern. If we find the pattern, we just continue walking up, which is safe.
            //We do check pretty strictly on the pattern that the compiler generates, so it's not unimaginable that if roslyn ever changes the IL instructions it uses
            //for this pattern, we'll have to add support for that new pattern here as well.
            //
            //the pattern "starts" with the branch in question always jumping straight to our callInstruction.  If it doesn't do that, this is not the special pattern, so
            //the branch is bad, and we should give up.

            /*
             *  IL_0030: pop
                IL_0031: ldsfld class C/'<>c' C/'<>c'::'<>9'
                IL_0036: ldftn instance void C/'<>c'::'<M>b__2_0'(int32, int32)
                IL_003c: newobj instance void class [mscorlib]System.Action`2<int32, int32>::.ctor(object, native int)
                IL_0041: dup
                IL_0042: stsfld class [mscorlib]System.Action`2<int32, int32> C/'<>c'::'<>9__2_0'
             */

            if (cursor.Next?.OpCode != OpCodes.Pop) return true;
            if (cursor.Next?.Next?.OpCode != OpCodes.Ldsfld) return true;
            if (cursor.Next?.Next?.Next?.OpCode != OpCodes.Ldftn) return true;
            if (cursor.Next?.Next?.Next?.Next?.OpCode != OpCodes.Newobj) return true;
            if (cursor.Next?.Next?.Next?.Next?.Next?.OpCode != OpCodes.Dup) return true;
            if (cursor.Next?.Next?.Next?.Next?.Next?.Next?.OpCode != OpCodes.Stsfld) return true;
            // if (cursor.Next?.Next?.Next?.Next?.Next?.Next?.Next != callInstructionsWhoseArgumentsWeWantToFind) return true;

            return false;
        }

        public static IEnumerable<LambdaJobDescriptionConstruction> FindIn(MethodDefinition method)
        {
            var body = method.Body;

            if (body == null)
                yield break;

            var lambdaJobStatementStartingInstructions = body.Instructions.Where(i =>
            {
                if (i.OpCode != OpCodes.Call && i.OpCode != OpCodes.Callvirt)
                    return false;
                var mr = (MethodReference) i.Operand;

                if (mr.DeclaringType.Namespace != "Unity.Entities")
                    return false;

                if (mr.Name == "get_" + nameof(JobComponentSystem.Entities) && mr.DeclaringType.Name == nameof(JobComponentSystem))
                    return true;
                if (mr.Name == "get_" + nameof(JobComponentSystem.Job) && mr.DeclaringType.Name == nameof(JobComponentSystem))
                    return true;
#if ENABLE_DOTS_COMPILER_CHUNKS                
                if (mr.Name == "get_" + nameof(JobComponentSystem.Chunks) && mr.DeclaringType.Name == nameof(JobComponentSystem))
                    return true;
#endif
                return false;
            }).ToList();

            int counter = 0;
            
            foreach (var lambdaJobStatementStartingInstruction in lambdaJobStatementStartingInstructions)
            {
                LambdaJobDescriptionConstruction result = default;
                result = AnalyzeLambdaJobStatement(method, lambdaJobStatementStartingInstruction, counter++);
                yield return result;
            }
        }

        static LambdaJobDescriptionConstruction AnalyzeLambdaJobStatement(MethodDefinition method, Instruction getEntitiesOrJobInstruction, int lambdaNumber)
        {
            List<InvokedConstructionMethod> modifiers = new List<InvokedConstructionMethod>();

            Instruction cursor = getEntitiesOrJobInstruction;
            var expectedPreviousMethodPushingDescription = getEntitiesOrJobInstruction;
            while (true)
            {
                cursor = FindNextConstructionMethod(method, cursor);

                var mr = cursor?.Operand as MethodReference;
                
                if (cursor == null || FindInstructionThatPushedArg(method, 0, cursor) != expectedPreviousMethodPushingDescription)
                    UserError.DC0007(method, cursor).Throw();
                
                if (mr.Name == nameof(LambdaJobDescriptionConstructionMethods.Schedule) || mr.Name == nameof(LambdaJobDescriptionConstructionMethods.Run))
                {
                    var withNameModifier = modifiers.FirstOrDefault(m => m.MethodName == nameof(LambdaJobDescriptionConstructionMethods.WithName));
                    var name = withNameModifier?.Arguments.OfType<string>().Single() ?? $"{method.DeclaringType.Name}_{method.Name}_LambdaJob{lambdaNumber}";

                    

                    LambdaJobDescriptionKind FindLambdaDescriptionKind()
                    {
                        switch (((MethodReference) getEntitiesOrJobInstruction.Operand).Name)
                        {
                            case "get_" + nameof(JobComponentSystem.Entities):
                                return LambdaJobDescriptionKind.Entities;
                            case "get_" + nameof(JobComponentSystem.Job):
                                return LambdaJobDescriptionKind.Job;
#if ENABLE_DOTS_COMPILER_CHUNKS
                            case "get_" + nameof(JobComponentSystem.Chunks):
                                return LambdaJobDescriptionKind.Chunk;
#endif
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                    if (modifiers.All(m => m.MethodName != nameof(LambdaForEachDescriptionConstructionMethods.ForEach) && m.MethodName != nameof(LambdaSimpleJobDescriptionConstructionMethods.WithCode)))
                    {
                        DiagnosticMessage MakeDiagnosticMessage()
                        {
                            switch (FindLambdaDescriptionKind())
                            {
                                case LambdaJobDescriptionKind.Entities:
                                    return UserError.DC0006(method, getEntitiesOrJobInstruction);
                                case LambdaJobDescriptionKind.Job:
                                    return UserError.DC0017(method, getEntitiesOrJobInstruction);
                                case LambdaJobDescriptionKind.Chunk:
                                    return UserError.DC0018(method, getEntitiesOrJobInstruction);
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }

                        MakeDiagnosticMessage().Throw();
                    }

                    return new LambdaJobDescriptionConstruction()
                    {
                        Kind = FindLambdaDescriptionKind(),
                        InvokedConstructionMethods = modifiers,
                        WithCodeInvocationInstruction = modifiers
                            .Single(m => m.MethodName == nameof(LambdaForEachDescriptionConstructionMethods.ForEach) || m.MethodName == nameof(LambdaSimpleJobDescriptionConstructionMethods.WithCode))
                            .InstructionInvokingMethod,
                        ScheduleOrRunInvocationInstruction = cursor,
                        Name = name,
                        ChainInitiatingInstruction = getEntitiesOrJobInstruction
                    };
                }

                var instructions = mr.Parameters.Skip(1)
                    .Select(p => OperandObjectFor(FindInstructionThatPushedArg(method, p.Index, cursor))).ToArray();

                var invokedConstructionMethod = new InvokedConstructionMethod(mr.Name,
                    (mr as GenericInstanceMethod)?.GenericArguments.ToArray() ?? Array.Empty<TypeReference>(),
                    instructions, cursor, method);

                var allowDynamicValue = method.Module.ImportReference(typeof(AllowDynamicValueAttribute));
                for (int i = 0; i != invokedConstructionMethod.Arguments.Length; i++)
                {
                    if (invokedConstructionMethod.Arguments[i] != null) 
                        continue;
                    
                    var inbovokedForEachMethod = mr.Resolve();
                    var methodDefinitionParameter = inbovokedForEachMethod.Parameters[i + 1];

                    if (!methodDefinitionParameter.CustomAttributes.Any(c =>c.AttributeType.TypeReferenceEquals(allowDynamicValue)))
                        UserError.DC0008(method, cursor, mr).Throw();
                }

                if (modifiers.Any(m => m.MethodName == mr.Name) && !HasAllowMultipleAttribute(mr.Resolve()))
                    UserError.DC0009(method, cursor, mr).Throw();

                expectedPreviousMethodPushingDescription = cursor;
                modifiers.Add(invokedConstructionMethod);
            }
        }

        private static Instruction FindNextConstructionMethod(MethodDefinition method, Instruction cursor)
        {
            //the description object should be on the stack, and nothing on top of it.
            int stackDepth = 1;
            while (cursor.Next != null)
            {
                cursor = cursor.Next;
                
                if (IsUnsupportedBranch(cursor))
                    UserError.DC0010(method, cursor).Throw();

                if (cursor.OpCode == OpCodes.Call && cursor.Operand is MethodReference mr && IsLambdaJobDescriptionConstructionMethod(mr))
                    return cursor;
                
                stackDepth -= cursor.GetPopDelta();
                if (stackDepth < 1)
                    UserError.DC0011(method, cursor).Throw();
                    
                stackDepth += cursor.GetPushDelta();
            }

            return null;
        }

        static bool HasAllowMultipleAttribute(MethodDefinition mr) => mr.HasCustomAttributes && mr.CustomAttributes.Any(c => c.AttributeType.Name == nameof(LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute));

        private static object OperandObjectFor(Instruction argumentPushingInstruction)
        {
            var opCode = argumentPushingInstruction.OpCode;
            
            if (opCode == OpCodes.Ldstr)
                return (string) argumentPushingInstruction.Operand;
            if (opCode == OpCodes.Ldc_I4)
                return (int) argumentPushingInstruction.Operand;
            if (opCode == OpCodes.Ldc_I4_0)
                return 0;
            if (opCode == OpCodes.Ldc_I4_1)
                return 1;
            if (opCode == OpCodes.Ldc_I4_2)
                return 2;
            if (opCode == OpCodes.Ldc_I4_3)
                return 3;
            if (opCode == OpCodes.Ldc_I4_4)
                return 4;
            if (opCode == OpCodes.Ldc_I4_5)
                return 5;
            if (opCode == OpCodes.Ldc_I4_6)
                return 6;
            if (opCode == OpCodes.Ldc_I4_7)
                return 7;
            if (opCode == OpCodes.Ldc_I4_8)
                return 8;
            if (opCode == OpCodes.Ldfld)
                return argumentPushingInstruction.Operand;
            return null;
        }

        static bool IsLambdaJobDescriptionConstructionMethod(MethodReference mr) => mr.DeclaringType.Name.EndsWith("ConstructionMethods") && mr.DeclaringType.Namespace == "Unity.Entities";
    }
}