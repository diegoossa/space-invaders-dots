using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Unity.Collections;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;

namespace Unity.Entities.CodeGen
{
    static class InjectAndInitializeEntityQueryField
    {
        public static FieldDefinition InjectAndInitialize(MethodDefinition methodToAnalyze, LambdaJobDescriptionConstruction descriptionConstruction, Collection<ParameterDefinition> closureParameters)
        {
            /* We're going to generate this code:
             *
             * protected void override OnCreate()
             * {
             *     _entityQuery = GetEntityQuery_ForMyJob_From(this);
             * }
             *
             * static void GetEntityQuery_ForMyJob_From(ComponentSystem componentSystem)
             * {
             *     var result = componentSystem.GetEntityQuery(new[] { new EntityQueryDesc() {
             *         All = new[] { ComponentType.ReadWrite<Position>(), ComponentType.ReadOnly<Velocity>() },
             *         None = new[] { ComponentType.ReadWrite<IgnoreTag>() }
             *     }});
             *     result.SetChangedFilter(new[] { ComponentType.ReadOnly<Position>() } );
             * }
             */

            var module = methodToAnalyze.Module;

            var entityQueryField = new FieldDefinition($"<>{descriptionConstruction.Name}_entityQuery",
                FieldAttributes.Private, module.ImportReference(typeof(EntityQuery)));
            var userSystemType = methodToAnalyze.DeclaringType;
            userSystemType.Fields.Add(entityQueryField);

            var getEntityQueryFromMethod = AddGetEntityQueryFromMethod(descriptionConstruction,closureParameters.ToArray(), methodToAnalyze.DeclaringType);
            
            InsertIntoOnCreate(userSystemType, new[]
            {
                Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Call, getEntityQueryFromMethod),
                Instruction.Create(OpCodes.Stfld, entityQueryField),
            });

            return entityQueryField;
        }

        public static void InsertIntoOnCreate(TypeDefinition userSystemType, Instruction[] instructions)
        {
            var onCreate = userSystemType.Methods.SingleOrDefault(m => m.Name == "OnCreate");
            if (onCreate == null)
            {
                onCreate = new MethodDefinition("OnCreate", MethodAttributes.Virtual | MethodAttributes.Family,userSystemType.Module.TypeSystem.Void)
                {
                    HasThis = true,
                    DeclaringType = userSystemType
                };
                userSystemType.Methods.Add(onCreate);
                var retEmitter = onCreate.Body.GetILProcessor();
                retEmitter.Emit(OpCodes.Ret);
            }

            var ilProcessor = onCreate.Body.GetILProcessor();

            ilProcessor.InsertBefore(ilProcessor.Body.Instructions.First(), instructions);
        }

        private static MethodDefinition AddGetEntityQueryFromMethod(
            LambdaJobDescriptionConstruction descriptionConstruction, ParameterDefinition[] closureParameters,
            TypeDefinition typeToInjectIn)
        {
            var moduleDefinition = typeToInjectIn.Module;

            var typeDefinition = typeToInjectIn;
            var getEntityQueryFromMethod =
                new MethodDefinition($"<>GetEntityQuery_For{descriptionConstruction.Name}_From",
                    MethodAttributes.Public | MethodAttributes.Static,
                    moduleDefinition.ImportReference(typeof(EntityQuery)))
                {
                    DeclaringType = typeDefinition,
                    HasThis = false,
                    Parameters =
                    {
                        new ParameterDefinition("componentSystem", ParameterAttributes.None,
                            moduleDefinition.ImportReference(typeof(ComponentSystemBase)))
                    }
                };

            typeDefinition.Methods.Add(getEntityQueryFromMethod);

            var body = getEntityQueryFromMethod.Body;

            var getEntityQueryMethod = moduleDefinition.ImportReference(typeof(ComponentSystemBase)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Single(m =>
                    m.Name == "GetEntityQuery" && m.GetParameters().Length == 1 &&
                    m.GetParameters().Single().ParameterType == typeof(EntityQueryDesc[])));

            var entityQueryDescConstructor =
                moduleDefinition.ImportReference(typeof(EntityQueryDesc).GetConstructor(Array.Empty<Type>()));
            var componentTypeReference = moduleDefinition.ImportReference(typeof(ComponentType));
            var componentTypeDefinition = componentTypeReference.Resolve();

            MethodReference ComponentTypeMethod(string name) =>
                moduleDefinition.ImportReference(
                    componentTypeDefinition.Methods.Single(m => m.Name == name && m.Parameters.Count == 0));

            var readOnlyMethod = ComponentTypeMethod(nameof(ComponentType.ReadOnly));
            var readWriteMethod = ComponentTypeMethod(nameof(ComponentType.ReadWrite));

            IEnumerable<Instruction> InstructionsToCreateComponentTypeFor(TypeReference typeReference, bool isReadOnly, int arrayIndex)
            {
                yield return Instruction.Create(OpCodes.Dup); //put the array on the stack again
                yield return Instruction.Create(OpCodes.Ldc_I4, arrayIndex);

                var method = isReadOnly ? readOnlyMethod : readWriteMethod;
                yield return Instruction.Create(OpCodes.Call,
                    method.MakeGenericInstanceMethod(typeReference.GetElementType()));
                yield return Instruction.Create(OpCodes.Stelem_Any, componentTypeReference);
            }

            IEnumerable<Instruction> InstructionsToPutArrayOfComponentTypesOnStack((TypeReference typeReference, bool readOnly)[] typeReferences)
            {
                yield return Instruction.Create(OpCodes.Ldc_I4, typeReferences.Length);
                yield return Instruction.Create(OpCodes.Newarr, componentTypeReference);

                for (int i = 0; i != typeReferences.Length; i++)
                    foreach (var instruction in InstructionsToCreateComponentTypeFor(typeReferences[i].typeReference, typeReferences[i].readOnly, i))
                        yield return instruction;
            }

            TypeReference[] AllTypeArgumentsOfMethod(string methodName)
            {
                return descriptionConstruction.InvokedConstructionMethods
                    .Where(m => m.MethodName == methodName)
                    .SelectMany(m => m.TypeArguments).ToArray();
            }

            var withNoneTypes = AllTypeArgumentsOfMethod(nameof(LambdaJobQueryConstructionMethods.WithNone));
            var withAllTypes = AllTypeArgumentsOfMethod(nameof(LambdaJobQueryConstructionMethods.WithAll))
                .Concat(
                    AllTypeArgumentsOfMethod(nameof(LambdaJobQueryConstructionMethods
                        .WithSharedComponentFilter)));
            
            var withAnyTypes = AllTypeArgumentsOfMethod(nameof(LambdaJobQueryConstructionMethods.WithAny));
            var withChangeFilterTypes = AllTypeArgumentsOfMethod(nameof(LambdaJobQueryConstructionMethods.WithChangeFilter));

            var arrayOfSingleEQDVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(EntityQueryDesc[])));
            var localVarOfEQD = new VariableDefinition(moduleDefinition.ImportReference(typeof(EntityQueryDesc)));
            var localVarOfResult = new VariableDefinition(moduleDefinition.ImportReference(typeof(EntityQuery)));

            body.Variables.Add(arrayOfSingleEQDVariable);
            body.Variables.Add(localVarOfEQD);
            body.Variables.Add(localVarOfResult);
            
            IEnumerable<Instruction> InstructionsToSetChangeFilterFor(TypeReference[] typeReferences)
            {
                if (typeReferences.Length == 0)
                    yield break;

                yield return Instruction.Create(OpCodes.Ldarg_0);
                yield return
                    Instruction.Create(OpCodes.Ldloc, localVarOfResult); //<- target of the SetChangedFilter call

                //create the array for the first argument:   new[] { ComponentType.ReadOnly<Position>(), ComponentType>.ReadOnly<Velocity>() }
                foreach (var instruction in InstructionsToPutArrayOfComponentTypesOnStack(typeReferences.Select(t=>(t,false)).ToArray()))
                    yield return instruction;

                EntityQuery eq;
                var setChangeFilter = moduleDefinition.ImportReference(
                    typeof(EntityQuery).GetMethod(nameof(eq.SetFilterChanged), new[] {typeof(ComponentType[])}));
                //and do the actual invocation
                yield return Instruction.Create(OpCodes.Call, setChangeFilter);
            }

            IEnumerable<Instruction> InstructionsToSetEntityQueryDescriptionField(string fieldName,
                (TypeReference typeReference, bool readOnly)[] typeReferences)
            {
                if (typeReferences.Length == 0)
                    yield break;
                yield return Instruction.Create(OpCodes.Ldloc, localVarOfEQD);
                foreach (var instruction in InstructionsToPutArrayOfComponentTypesOnStack(typeReferences))
                    yield return instruction;
                var fieldReference = new FieldReference(fieldName, moduleDefinition.ImportReference(typeof(ComponentType[])),
                    entityQueryDescConstructor.DeclaringType);
                yield return Instruction.Create(OpCodes.Stfld,fieldReference);
            }

            EntityQueryDesc eqd;
            
            IEnumerable<Instruction> InstructionsToSetEntityQueryDescriptionOptions()
            {
                var withOptionsInvocation = descriptionConstruction.InvokedConstructionMethods.FirstOrDefault(m =>
                    m.MethodName == nameof(LambdaJobQueryConstructionMethods.WithEntityQueryOptions));
                if (withOptionsInvocation == null)
                    yield break;
                
                yield return Instruction.Create(OpCodes.Ldloc, localVarOfEQD);
                yield return Instruction.Create(OpCodes.Ldc_I4, (int)withOptionsInvocation.Arguments.Single());
                var fieldReference = new FieldReference(nameof(eqd.Options),moduleDefinition.ImportReference(typeof(EntityQueryOptions)),entityQueryDescConstructor.DeclaringType);
                yield return Instruction.Create(OpCodes.Stfld,fieldReference);
            }

            var combinedWithAllTypes = withAllTypes.Select(typeReference=>(typeReference,true))
                .Concat(closureParameters.Select(WithAllTypeArgumentForLambdaParameter).Where(t=>t.typeReference != null))
                .ToArray();

            foreach (var noneType in withNoneTypes)
            {
                if (combinedWithAllTypes.Select(c=>c.typeReference).Any(allType => allType.TypeReferenceEquals(noneType)))
                    UserError.DC0015(noneType.Name, descriptionConstruction.InvokedConstructionMethods.First().ContainingMethod, descriptionConstruction.InvokedConstructionMethods.First().InstructionInvokingMethod).Throw();

                if (withAnyTypes.Any(anyType => anyType.TypeReferenceEquals(noneType)))
                    UserError.DC0016(noneType.Name,descriptionConstruction.InvokedConstructionMethods.First().ContainingMethod,descriptionConstruction.InvokedConstructionMethods.First().InstructionInvokingMethod).Throw();
            }

            var instructions = new List<Instruction>()
            {
                //var arrayOfSingleEQDVariable = new EnityQueryDesc[1];
                Instruction.Create(OpCodes.Ldc_I4_1),
                Instruction.Create(OpCodes.Newarr, moduleDefinition.ImportReference(typeof(EntityQueryDesc))),
                Instruction.Create(OpCodes.Stloc, arrayOfSingleEQDVariable),

                //var localVarOfEQD = new EntityQuery();
                Instruction.Create(OpCodes.Newobj, entityQueryDescConstructor),
                Instruction.Create(OpCodes.Stloc, localVarOfEQD),

                // arrayOfSingleEQDVariable[0] = localVarOfEQD;
                Instruction.Create(OpCodes.Ldloc, arrayOfSingleEQDVariable),
                Instruction.Create(OpCodes.Ldc_I4_0),
                Instruction.Create(OpCodes.Ldloc, localVarOfEQD),
                Instruction.Create(OpCodes.Stelem_Any, moduleDefinition.ImportReference(typeof(EntityQueryDesc))),

                InstructionsToSetEntityQueryDescriptionField(nameof(eqd.All), combinedWithAllTypes),
                InstructionsToSetEntityQueryDescriptionField(nameof(eqd.None), withNoneTypes.Select(t=>(t,false)).ToArray()),
                InstructionsToSetEntityQueryDescriptionField(nameof(eqd.Any), withAnyTypes.Select(t=>(t,false)).ToArray()),

                InstructionsToSetEntityQueryDescriptionOptions(),
                
                Instruction.Create(OpCodes.Ldarg_0), //the this for this.GetEntityQuery()
                Instruction.Create(OpCodes.Ldloc, arrayOfSingleEQDVariable),
                Instruction.Create(OpCodes.Call, getEntityQueryMethod),

                Instruction.Create(OpCodes.Stloc, localVarOfResult),

                InstructionsToSetChangeFilterFor(withChangeFilterTypes),

                Instruction.Create(OpCodes.Ldloc, localVarOfResult),
                Instruction.Create(OpCodes.Ret),
            };

            var ilProcessor = getEntityQueryFromMethod.Body.GetILProcessor();
            ilProcessor.Append(instructions);

            return getEntityQueryFromMethod;
        }

        private static (TypeReference typeReference, bool readOnly) WithAllTypeArgumentForLambdaParameter(ParameterDefinition p)
        {
            var isMarkedReadOnly = p.HasCompilerServicesIsReadOnlyAttribute();
            var isMarkedAsRef = p.ParameterType.IsByReference && !isMarkedReadOnly;
            
            var type = p.ParameterType.StripRef();
            if (type.IsIComponentData() || type.IsISharedComponentData())
                return (p.ParameterType.GetElementType(), !isMarkedAsRef);
            if (type.IsDynamicBufferOfT())
            {
                var typeReference = ((GenericInstanceType) type).GenericArguments.Single();
                return (typeReference, isMarkedReadOnly);
            }

            return (null,true);
        }
    }
}