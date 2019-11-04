using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Unity.Entities.CodeGen
{
    static class CecilHelpers
    {
        public static Instruction MakeInstruction(OpCode opcode, object operand)
        {
            switch (operand)
            {
                case null:
                    return Instruction.Create(opcode);
                case FieldReference o:
                    return Instruction.Create(opcode, o);
                case MethodReference o:
                    return Instruction.Create(opcode, o);
                case VariableDefinition o:
                    return Instruction.Create(opcode, o);
                case ParameterDefinition o:
                    return Instruction.Create(opcode, o);
                case GenericInstanceType o:
                    return Instruction.Create(opcode, o);
                case TypeReference o:
                    return Instruction.Create(opcode, o);
                case CallSite o:
                    return Instruction.Create(opcode, o);
                case int o:
                    return Instruction.Create(opcode, o);
                case float o:
                    return Instruction.Create(opcode, o);
                case double o:
                    return Instruction.Create(opcode, o);
                case sbyte o:
                    return Instruction.Create(opcode, o);
                case byte o:
                    return Instruction.Create(opcode, o);
                case long o:
                    return Instruction.Create(opcode, o);
                case uint o:
                    return Instruction.Create(opcode, o);
                case string o:
                    return Instruction.Create(opcode, o);
                case Instruction o:
                    return Instruction.Create(opcode, o);
                default:
                    throw new NotSupportedException("Unknown operand: " + operand.GetType());
            }
        }

        public static SequencePoint FindBestSequencePointFor(MethodDefinition method, Instruction instruction)
        {
            var sequencePoints = method.DebugInformation?.GetSequencePointMapping().Values.OrderBy(s => s.Offset).ToList();

            for (int i = 0; i != sequencePoints.Count-1; i++)
            {
                if (sequencePoints[i].Offset < instruction.Offset &&
                    sequencePoints[i + 1].Offset > instruction.Offset)
                    return sequencePoints[i];
            }

            return sequencePoints.FirstOrDefault();
        }

        public static MethodDefinition[] CloneClosureExecuteMethodAndItsLocalFunctions(
            Dictionary<string, (FieldDefinition oldField, FieldDefinition newField)> displayClassFieldToJobStructField, 
            IEnumerable<MethodDefinition> methodsToClone, TypeDefinition targetType, 
            string newMethodName)
        {
            var executeMethod = methodsToClone.First();
            
            if (executeMethod.HasGenericParameters)
                throw new ArgumentException();

            var clonedMethods = methodsToClone.ToDictionary(m => m, m =>
                {
                    var clonedMethod = new MethodDefinition(m == executeMethod ? newMethodName : m.Name, MethodAttributes.Public, m.ReturnType)
                            {HasThis = m.HasThis, DeclaringType = targetType};
                    targetType.Methods.Add(clonedMethod);
                    return clonedMethod;
                }
            );
            
            foreach (var methodToClone in methodsToClone)
            {
                var methodDefinition = clonedMethods[methodToClone];
                
                foreach (var lambdaParameter in methodToClone.Parameters)
                {
                    var executeParameter = new ParameterDefinition(lambdaParameter.Name, lambdaParameter.Attributes,
                        lambdaParameter.ParameterType);
                    foreach (var ca in lambdaParameter.CustomAttributes)
                        executeParameter.CustomAttributes.Add(ca);

                    methodDefinition.Parameters.Add(executeParameter);
                }

                var ilProcessor = methodDefinition.Body.GetILProcessor();

                var oldVarToNewVar = new Dictionary<VariableDefinition, VariableDefinition>();
                foreach (var vd in methodToClone.Body.Variables)
                {
                    var newVd = new VariableDefinition(vd.VariableType);
                    methodDefinition.Body.Variables.Add(newVd);
                    oldVarToNewVar.Add(vd, newVd);
                }

                var oldToNewInstructions = new Dictionary<Instruction, Instruction>();
                Instruction previous = null;
                foreach (var instruction in methodToClone.Body.Instructions)
                {
                    instruction.Previous = previous;
                    if (previous != null)
                        previous.Next = instruction;
                    
                    var clonedOperand = instruction.Operand;
                    if (clonedOperand is FieldReference fr)
                    {
                        if (displayClassFieldToJobStructField.TryGetValue(fr.FullName, out var replacement))
                            clonedOperand = replacement.Item2;
                    }

                    if (clonedOperand is VariableDefinition vd)
                    {
                        if (oldVarToNewVar.TryGetValue(vd, out var replacement))
                            clonedOperand = replacement;
                    }

                    if (clonedOperand is MethodReference mr)
                    {
                        var targetThatWeAreCloning = methodsToClone.FirstOrDefault(m => m.FullName == mr.FullName);
                        if (targetThatWeAreCloning != null)
                        {
                            var replacement = clonedMethods[targetThatWeAreCloning];
                            clonedOperand = replacement;
                        }
                    }

                    var newInstruction = MakeInstruction(instruction.OpCode, clonedOperand);
                    oldToNewInstructions.Add(instruction, newInstruction);
                    ilProcessor.Append(newInstruction);

                    previous = instruction;
                }

                var oldDebugInfo = methodToClone.DebugInformation;
                var newDebugInfo = methodDefinition.DebugInformation;
                foreach (var seq in oldDebugInfo.SequencePoints)
                    newDebugInfo.SequencePoints.Add(seq);

                //for all instructions that point to another instruction (like branches), make sure we patch those instructions to the new ones too.
                foreach (var newInstruction in oldToNewInstructions.Values)
                {
                    if (newInstruction.Operand is Instruction oldInstruction)
                        newInstruction.Operand = oldToNewInstructions[oldInstruction];
                }
            }

            return clonedMethods.Values.ToArray();
        }


        public static void EraseMethodInvocationFromInstructions(ILProcessor ilProcessor, Instruction callInstruction)
        {
            var argumentPushingInstructions = new List<Instruction>();
            int succesfullyEraseArguments = 0;
            
            if (!(callInstruction.Operand is MethodReference methodReference))
                return;

            bool isMethodThatReturnsItsFirstArgument = !methodReference.HasThis && (methodReference.Parameters.FirstOrDefault()?.ParameterType.TypeReferenceEquals(methodReference.ReturnType) ?? false);
            
            var parametersCount = methodReference.Parameters.Count + (methodReference.HasThis ? 1 : 0);
            for (int i = 0; i != parametersCount; i++)
            {
                if (isMethodThatReturnsItsFirstArgument && i == 0)
                    continue;
                
                var instructionThatPushedArg = LambdaJobDescriptionConstruction.FindInstructionThatPushedArg(ilProcessor.Body.Method, i, callInstruction);
                if (instructionThatPushedArg == null)
                    continue;

                if (instructionThatPushedArg.IsInvocation())
                    continue;
                
                var pushDelta = InstructionExtensions.GetPushDelta(instructionThatPushedArg);
                var popDelta = InstructionExtensions.GetPopDelta(instructionThatPushedArg);
                
                if (pushDelta == 1 && popDelta == 0)
                {
                    argumentPushingInstructions.Add(instructionThatPushedArg);
                    succesfullyEraseArguments++;
                    continue;
                }

                if (pushDelta == 1 && popDelta == 1)
                {
                    var whoPushedThat = LambdaJobDescriptionConstruction.FindInstructionThatPushedArg(ilProcessor.Body.Method, 0, instructionThatPushedArg);
                    if (InstructionExtensions.GetPopDelta(whoPushedThat) == 0 && InstructionExtensions.GetPushDelta(whoPushedThat) == 1)
                    {
                        argumentPushingInstructions.Add(instructionThatPushedArg);
                        argumentPushingInstructions.Add(whoPushedThat);
                        succesfullyEraseArguments++;
                        continue;
                    }
                }
            }
            

            foreach (var i in argumentPushingInstructions)
                i.MakeNOP();
            
            //we're going to remove the invocation. While we do this we want to remain stack neutral. The stackbehaviour going in is that the jobdescription itself will be on the stack,
            //plus any arguments the method might have.  After the function, it will put the same jobdescription on the stack as the return value.
            //we're going to pop all the arguments, but leave the jobdescription itself on the stack, since that the behaviour of the original method.
            var parametersToErase = parametersCount - (isMethodThatReturnsItsFirstArgument ? 1 : 0);
            var popInstructions = Enumerable.Repeat(Instruction.Create(OpCodes.Pop), parametersToErase - succesfullyEraseArguments);
            ilProcessor.InsertBefore(callInstruction, popInstructions);

            //instead of removing the call instruction, we'll replace it with a NOP opcode. This is safer as this instruction
            //might be the target of a branch instruction that we don't want to become invalid.
            callInstruction.MakeNOP();
            
            if (!methodReference.ReturnType.TypeReferenceEquals(methodReference.Module.TypeSystem.Void) && !isMethodThatReturnsItsFirstArgument)
            {
                callInstruction.OpCode = OpCodes.Ldnull;
            }
        }

        public static bool IsInstructionSequenceRoslynCachingADelegate(Instruction instructionThatPushedDelegate, out Instruction[] instructions)
        {
            var cacheInStaticFieldPattern = new Func<Instruction, bool>[]
            {
                i => i.OpCode == OpCodes.Ldsfld,
                i => i.OpCode == OpCodes.Dup,
                i => i.IsBranch(),
                i => i.OpCode == OpCodes.Pop,
                i => i.OpCode == OpCodes.Ldsfld,
                i => i.OpCode == OpCodes.Ldftn,
                i => i.OpCode == OpCodes.Newobj,
                i => i.OpCode == OpCodes.Dup,
                i => i.OpCode == OpCodes.Stsfld,
            };
            
            /*
             *        IL_0007: ldarg.0
                IL_0008: ldfld class [mscorlib]System.Action`1<valuetype Translation> C/System/'<>c__DisplayClass0_0'::'<>9__1'
                IL_000d: dup
                IL_000e: brtrue.s IL_0026

                IL_0010: pop
                IL_0011: ldarg.0
                IL_0012: ldarg.0
                IL_0013: ldftn instance void C/System/'<>c__DisplayClass0_0'::'<OnUpdate>b__1'(valuetype Translation)
                IL_0019: newobj instance void class [mscorlib]System.Action`1<valuetype Translation>::.ctor(object, native int)
                IL_001e: dup
                IL_001f: stloc.0
                IL_0020: stfld class [mscorlib]System.Action`1<valuetype Translation> C/System/'<>c__DisplayClass0_0'::'<>9__1'
                IL_0025: ldloc.0
             */
            
            var cacheInInstanceFieldPattern = new Func<Instruction, bool>[]
            {
                i => i.OpCode == OpCodes.Ldarg || i.OpCode == OpCodes.Ldarg_0,
                i => i.OpCode == OpCodes.Ldfld,
                i => i.OpCode == OpCodes.Dup,
                i => i.IsBranch(),
                i => i.OpCode == OpCodes.Pop,
                i => i.OpCode == OpCodes.Ldarg || i.OpCode == OpCodes.Ldarg_0,
                i => i.OpCode == OpCodes.Ldarg || i.OpCode == OpCodes.Ldarg_0,
                i => i.OpCode == OpCodes.Ldftn,
                i => i.OpCode == OpCodes.Newobj,
                i => i.OpCode == OpCodes.Dup,
                i => i.OpCode == OpCodes.Stfld,
                i => i.IsLoadLocal(out _),
            };
            
            foreach(var pattern in new[] { cacheInStaticFieldPattern})
                if (MatchAgainstExpectation(instructionThatPushedDelegate, out instructions, pattern))
                    return true;
            instructions = null;
            return false;
        }
        
        private static bool MatchAgainstExpectation(Instruction instructionThatPushedDelegate, out Instruction[] instructions, Func<Instruction, bool>[] expectation)
        {
            instructions = null;

            Instruction[] LastTenInstructions()
            {
                var temp = new List<Instruction>();

                //the instruction that actually pushed the delegate is the "Dup" one.  it's not at the very end. to find this sequence//we have to move forward one instructions.
                var cursor = instructionThatPushedDelegate.Next;

                if (cursor == null)
                    return null;
                for (var i = 0; i != expectation.Length; i++)
                {
                    temp.Add(cursor);
                    var prev = cursor.Previous;
                    if (prev == null)
                        return null;
                    cursor = prev;
                }

                temp.Reverse();
                return temp.ToArray();
            }

            var lastTen = LastTenInstructions();
            for (int i = 0; i != lastTen.Length; i++)
            {
                if (!expectation[i](lastTen[i]))
                    return false;
            }

            instructions = lastTen;

            return true;
        }

        public static IEnumerable<MethodDefinition> FindUsedInstanceMethodsOnSameType(MethodDefinition method, HashSet<string> foundSoFar = null)
        {
            foundSoFar = foundSoFar ?? new HashSet<string>();

            var usedInThisMethod = method.Body.Instructions.Where(i=>i.IsInvocation()).Select(i => i.Operand).OfType<MethodReference>().Where(mr => mr.DeclaringType.TypeReferenceEquals(method.DeclaringType));

            foreach (var usedMethod in usedInThisMethod)
            {
                if (foundSoFar.Contains(usedMethod.FullName))
                    continue;
                foundSoFar.Add(usedMethod.FullName);
                var usedMethodResolved = usedMethod.Resolve();
                yield return usedMethodResolved;

                foreach (var used in FindUsedInstanceMethodsOnSameType(usedMethodResolved, foundSoFar))
                    yield return used;
            }
        }

        static readonly string _universalDelegatesNamespace = nameof(Unity) + "." + nameof(Unity.Entities) + "." + nameof(Unity.Entities.UniversalDelegates);

        public static bool AllDelegatesAreGuaranteedNotToOutliveMethodFor(MethodDefinition methodToAnalyze)
        {
            //in order to make lambda jobs be able to not allocate GC memory, we want to change the DisplayClass that stores the variables from a class to a struct.
            //This is only safe if the only delegates that are used in the methods are the ones for lambda jobs, because we know that those will not leak.  If any other
            //delegates are used, we cannot guarantee this, and we will keep the displayclass as a class, which results in a heap allocation for every invocation of the method.

            foreach (var instruction in methodToAnalyze.Body.Instructions)
            {
                //we'll find all occurrences of delegates by scanning all constructor invocations.
                if (instruction.OpCode != OpCodes.Newobj)
                    continue;
                var mr = (MethodReference) instruction.Operand;
                
                //to avoid a potentially expensive resolve, we'll first try to rule out this instruction as delegate creating by doing some pattern checks:
                
                //all delegate creation constructors take two arguments.
                if (mr.Parameters.Count != 2)
                    continue;
                
                //if this delegate is one of our UniversalDelegates we'll assume we're cool. This is not waterproof, as you could imagine a situation where someone
                //makes an instance of our delegate manually, and intentionally leaks that. We'll consider that scenario near-malice for now, and assume that the UniversalDelegates
                //are exclusively used as arguments for lambda jobs that do not leak.
                if (mr.DeclaringType.Namespace == _universalDelegatesNamespace)
                    continue;

                if (mr.DeclaringType.Name == typeof(LambdaJobChunkDescriptionConstructionMethods.JobChunkDelegate).Name && mr.DeclaringType.DeclaringType?.Name == nameof(LambdaJobChunkDescriptionConstructionMethods))
                    continue;
                
                //ok, it walks like a delegate constructor invocation, let's see if it talks like one:
                var constructedType = mr.DeclaringType.Resolve();
                if (constructedType.BaseType.Name == nameof(MulticastDelegate))
                    return false;
            }

            return true;
        }

        public static void PatchDisplayClassToBeAStruct(TypeDefinition displayClass)
        {
            displayClass.BaseType = displayClass.Module.ImportReference(typeof(ValueType));
            displayClass.IsClass = false;
            
            //we have to kill the body of the default constructor, as it invokes the base class constructor, which makes no sense for a valuetype
            var constructorDefinition = displayClass.Methods.Single(m => m.IsConstructor);
            constructorDefinition.Body = new MethodBody(constructorDefinition);
            constructorDefinition.Body.GetILProcessor().Emit(OpCodes.Ret);
        }

        public static void PatchMethodThatUsedDisplayClassToTreatItAsAStruct(MethodBody body, VariableDefinition displayClassVariable, TypeReference displayClassTypeReference)
        {
            var instructions = body.Instructions.ToArray();
            var ilProcessor = body.GetILProcessor();
            foreach (var instruction in instructions)
            {
                //we will replace all LdLoc of our displayclass to LdLoca_S of our displayclass variable that now lives on the stack.
                if (instruction.IsLoadLocal(out int loadIndex) && displayClassVariable.Index == loadIndex)
                {
                    ilProcessor.Replace(instruction,Instruction.Create(OpCodes.Ldloca_S, body.Variables[loadIndex]));
                }

                //same thing for all stores into the displayclass.
                if (instruction.IsStoreLocal(out int storeIndex) && displayClassVariable.Index == storeIndex)
                {
                    ilProcessor.Replace(instruction, Instruction.Create(OpCodes.Pop));
                }

                bool IsInstructionNewObjOfDisplayClass(Instruction thisInstruction)
                {
                    return thisInstruction.OpCode.Code == Code.Newobj && ((MethodReference) thisInstruction.Operand).DeclaringType.TypeReferenceEquals(displayClassTypeReference);
                }

                //we need to replace the creation of the displayclass object on the heap, with a initobj of the displayclass on the stack.
                //the final sequence will be ldloca, initobj, ldloca.  The first ldloca is the argument for the initobj opcode. the second
                //one is to maintain stack behaviour with the original code, which used newobj, which places the heap-displayclass 
                if (IsInstructionNewObjOfDisplayClass(instruction))
                {
                    ilProcessor.Replace(instruction, new[]
                    {
                        Instruction.Create(OpCodes.Ldloca, displayClassVariable),
                        Instruction.Create(OpCodes.Initobj, displayClassTypeReference),
                        Instruction.Create(OpCodes.Ldloca, displayClassVariable)
                    });
                }
            }
        }

        public static void CloneMethodForDiagnosingProblems(MethodDefinition methodToAnalyze)
        {
            var cloneName = methodToAnalyze.Name + "_Unmodified";
            if (methodToAnalyze.DeclaringType.Methods.Any(m => m.Name == cloneName))
                return;
            
            var clonedMethod = new MethodDefinition(cloneName, methodToAnalyze.Attributes, methodToAnalyze.ReturnType);
            foreach (var parameter in methodToAnalyze.Parameters)
                clonedMethod.Parameters.Add(parameter);
            foreach (var v in methodToAnalyze.Body.Variables)
                clonedMethod.Body.Variables.Add(new VariableDefinition(v.VariableType));
            var p = clonedMethod.Body.GetILProcessor();
            var oldToNew = new Dictionary<Instruction, Instruction>();
            foreach (var i in methodToAnalyze.Body.Instructions)
            {
                var newInstruction = CecilHelpers.MakeInstruction(i.OpCode, i.Operand);
                oldToNew.Add(i, newInstruction);
                p.Append(newInstruction);
            }

            foreach (var i in oldToNew.Values)
            {
                if (i.Operand is Instruction operand)
                {
                    if (oldToNew.TryGetValue(operand, out var replacement))
                        i.Operand = replacement;
                }
            }

            methodToAnalyze.DeclaringType.Methods.Add(clonedMethod);
        }
    }
}