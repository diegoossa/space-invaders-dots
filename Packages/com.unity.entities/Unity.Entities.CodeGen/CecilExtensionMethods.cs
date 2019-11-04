using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Unity.Entities.CodeGen
{
    static class TypeReferenceExtensions
    {
        public static bool TypeReferenceEquals(this TypeReference ref1, TypeReference ref2) =>
            ref1.FullName == ref2.FullName;

        public static bool IsIComponentData(this TypeReference typeReference) =>
            typeReference.TypeImplements(typeof(IComponentData));

        public static bool IsISharedComponentData(this TypeReference typeReference) =>
            typeReference.TypeImplements(typeof(ISharedComponentData));

        public static bool IsDynamicBufferOfT(this TypeReference typeReference) =>
            typeReference.GetElementType().FullName == typeof(DynamicBuffer<>).FullName;

        public static bool TypeImplements(this TypeReference typeReference, Type interfaceType)
        {
            return typeReference.Resolve().Interfaces.Any(i =>
                i.InterfaceType.FullName == typeReference.Module.ImportReference(interfaceType).FullName);
        }

        public static TypeReference StripRef(this TypeReference tr) => tr is ByReferenceType brt ? brt.ElementType : tr;

        public static bool IsVoid(this TypeReference type)
        {
            return type.MetadataType == MetadataType.Void;
        }
    }

    static class MethodReferenceExtensions
    {
        public static MethodReference MakeGenericInstanceMethod(this MethodReference method,
            params TypeReference[] types)
        {
            var result = new GenericInstanceMethod(method);
            foreach (var type in types)
                result.GenericArguments.Add(type);
            return result;
        }
    }

    static class ParameterDefinitionExtensions
    {
        internal static bool HasCompilerServicesIsReadOnlyAttribute(this ParameterDefinition p)
        {
            return p.HasCustomAttributes && p.CustomAttributes.Any(c =>
                       c.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
        }
    }

    static class TypeDefinitionExtensions
    {
        public static bool IsDelegate(this TypeDefinition typeDefinition) =>
            typeDefinition.BaseType?.Name == nameof(MulticastDelegate);

        public static bool IsComponentSystem(this TypeDefinition arg)
        {
            var baseTypeRef = arg.BaseType;

            if (baseTypeRef == null)
                return false;

            if (baseTypeRef.Namespace == "Unity.Entities" && baseTypeRef.Name == nameof(ComponentSystemBase))
                return true;

            if (baseTypeRef.Name == "Object" && baseTypeRef.Namespace == "System")
                return false;

            if (baseTypeRef.Name == "ValueType" && baseTypeRef.Namespace == "System")
                return false;

            return IsComponentSystem(baseTypeRef.Resolve());
        }
    }

    static class ILProcessorExtensions
    {
        public static void EnsurePreviousAndNextAreSet(this MethodBody body)
        {
            for (int i = 0; i != body.Instructions.Count - 1; i++)
            {
                var thisOne = body.Instructions[i];
                var nextOne = body.Instructions[i + 1];
                thisOne.Next = nextOne;
                nextOne.Previous = thisOne;
            }
        }

        public static void InsertAfter(this ILProcessor ilProcessor, Instruction insertAfterThisOne,
            IEnumerable<Instruction> instructions)
        {
            var prev = insertAfterThisOne;
            foreach (var instruction in instructions)
            {
                ilProcessor.InsertAfter(prev, instruction);
                prev = instruction;
            }
        }

        public static void InsertBefore(this ILProcessor ilProcessor, Instruction insertBeforeThisOne,
            IEnumerable<Instruction> instructions)
        {
            foreach (var instruction in instructions)
                ilProcessor.InsertBefore(insertBeforeThisOne, instruction);
        }

        public static void Append(this ILProcessor ilProcessor, IEnumerable<Instruction> instructions)
        {
            foreach (var instruction in instructions)
                ilProcessor.Append(instruction);
        }

        public static void Replace(this ILProcessor ilProcessor, Instruction replaceThisOne,
            IEnumerable<Instruction> withThese)
        {
            ilProcessor.InsertAfter(replaceThisOne, withThese);
            ilProcessor.Remove(replaceThisOne);
        }
    }

    static class InstructionExtensions
    {
        public static void MakeNOP(this Instruction instruction)
        {
            instruction.OpCode = OpCodes.Nop;
            instruction.Operand = null;
        }

        public static bool IsLoadLocalAddress(this Instruction instruction, out int index)
        {
            index = 0;
            switch (instruction.OpCode.Code)
            {
                case Code.Ldloca:
                case Code.Ldloca_S:
                    index = instruction.Operand is VariableDefinition vd ? vd.Index : (int) instruction.Operand;
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsInvocation(this Instruction instruction)
        {
            var opCode = instruction.OpCode;
            return opCode == OpCodes.Call || (opCode == OpCodes.Callvirt || opCode == OpCodes.Calli);
        }

        public static bool IsBranch(this Instruction instruction)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Brtrue:
                case Code.Brtrue_S:
                case Code.Brfalse:
                case Code.Brfalse_S:
                case Code.Br:
                case Code.Br_S:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsLoadLocal(this Instruction instruction, out int index)
        {
            index = 0;
            switch (instruction.OpCode.Code)
            {
                case Code.Ldloc:
                case Code.Ldloc_S:
                    index = instruction.Operand is VariableDefinition vd ? vd.Index : (int) instruction.Operand;
                    return true;
                case Code.Ldloc_0:
                    index = 0;
                    return true;
                case Code.Ldloc_1:
                    index = 1;
                    return true;
                case Code.Ldloc_2:
                    index = 2;
                    return true;
                case Code.Ldloc_3:
                    index = 3;
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsStoreLocal(this Instruction instruction, out int index)
        {
            index = 0;
            switch (instruction.OpCode.Code)
            {
                case Code.Stloc:
                case Code.Stloc_S:
                    if (instruction.Operand is VariableDefinition vd)
                        index = vd.Index;
                    else
                        index = (int) instruction.Operand;
                    return true;
                case Code.Stloc_0:
                    index = 0;
                    return true;
                case Code.Stloc_1:
                    index = 1;
                    return true;
                case Code.Stloc_2:
                    index = 2;
                    return true;
                case Code.Stloc_3:
                    index = 3;
                    return true;
                default:
                    return false;
            }
        }

        public static int GetPushDelta(this Instruction instruction)
        {
            OpCode code = instruction.OpCode;
            switch (code.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                    return 0;

                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    return 1;

                case StackBehaviour.Push1_push1:
                    return 2;

                case StackBehaviour.Varpush:
                    if (code.FlowControl == FlowControl.Call)
                    {
                        var method = (IMethodSignature) instruction.Operand;
                        return method.ReturnType.IsVoid() ? 0 : 1;
                    }

                    break;
            }

            throw new ArgumentException(instruction.ToString());
        }

        public static int GetPopDelta(this Instruction instruction)
        {
            OpCode code = instruction.OpCode;
            switch (code.StackBehaviourPop)
            {
                case StackBehaviour.Pop0:
                    return 0;
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                case StackBehaviour.Pop1:
                    return 1;

                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    return 2;

                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    return 3;

                case StackBehaviour.Varpop:
                    if (code.FlowControl == FlowControl.Call)
                    {
                        var method = (IMethodSignature) instruction.Operand;
                        int count = method.Parameters.Count;
                        if (method.HasThis && OpCodes.Newobj.Value != code.Value)
                            ++count;

                        return count;
                    }

                    break;
            }

            throw new ArgumentException(instruction.ToString());
        }
    }
}