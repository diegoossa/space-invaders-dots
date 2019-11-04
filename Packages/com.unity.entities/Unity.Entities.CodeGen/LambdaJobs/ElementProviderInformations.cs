﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.CodeGeneratedJobForEach;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Unity.Entities.CodeGen
{
    internal class ElementProviderInformations
    {
        private ElementProviderInformation[] _providers;
            
        Dictionary<string, ElementProviderInformation> _parameterTypeNameToProvider;
        readonly Dictionary<ElementProviderInformation, (FieldDefinition elementField, FieldDefinition runtimeField)> _elementProviderToFields = new Dictionary<ElementProviderInformation, (FieldDefinition elementField, FieldDefinition runtimeField)>();
        TypeDefinition _jobChunkType;
        MethodDefinition _scheduleTimeInitializeMethod;
        TypeDefinition _elementProvidersType;
        public TypeDefinition RuntimesType { get; private set; }
        MethodDefinition _prepareToExecuteOnEntitiesInMethod;
        FieldDefinition _elementsField;
        public FieldDefinition _runtimesField;
            
        private ElementProviderInformations()
        {
        }

        void InjectIntoJobChunkType()
        {
            _elementProvidersType = new TypeDefinition("", "ElementProviders", TypeAttributes.NestedPrivate | TypeAttributes.SequentialLayout, _jobChunkType.Module.ImportReference(typeof(ValueType))) {DeclaringType = _jobChunkType};
            _jobChunkType.NestedTypes.Add(_elementProvidersType);
             
            _elementsField = new FieldDefinition("_elementProviders", FieldAttributes.Private, _elementProvidersType);
            _jobChunkType.Fields.Add(_elementsField);
                
            RuntimesType = new TypeDefinition("", "Runtimes", TypeAttributes.NestedPublic | TypeAttributes.SequentialLayout, _jobChunkType.Module.ImportReference(typeof(ValueType))) { DeclaringType = _elementProvidersType};
            _elementProvidersType.NestedTypes.Add(RuntimesType);

            _runtimesField = new FieldDefinition("_runtimes", FieldAttributes.Private,
                new PointerType(RuntimesType))
            {
                CustomAttributes =
                {
                    new CustomAttribute(EntitiesForEachJobCreator.AttributeConstructorReferenceFor(typeof(NativeDisableUnsafePtrRestrictionAttribute), ModuleDefinition))
                }
            };
            _jobChunkType.Fields.Add(_runtimesField);
            
            
            int counter = 0;
            foreach (var provider in _providers)
            {
                var elementFieldDefinition = new FieldDefinition($"element{counter}", FieldAttributes.Private, provider.Provider);
                if (provider.IsReadOnly)
                {
                    var readOnlyAttributeConstructor = EntitiesForEachJobCreator.AttributeConstructorReferenceFor(typeof(ReadOnlyAttribute), _jobChunkType.Module);
                    elementFieldDefinition.CustomAttributes.Add(new CustomAttribute(readOnlyAttributeConstructor));
                }
                _elementProvidersType.Fields.Add(elementFieldDefinition);
                    
                var runtimeFieldDefinition = new FieldDefinition($"runtime{counter}", FieldAttributes.Public, provider.ProviderRuntime);
                RuntimesType.Fields.Add(runtimeFieldDefinition);
                    
                _elementProviderToFields.Add(provider, (elementFieldDefinition,runtimeFieldDefinition));
                    
                counter++;
            }

            MakeScheduleTimeInitializeMethod();
            MakePrepareToExecuteOnEntitiesInMethod();
        }

        void MakePrepareToExecuteOnEntitiesInMethod()
        {
            var mimickParametersFrom = ModuleDefinition.ImportReference(typeof(ElementProvider_EntityInQueryIndex).GetMethod(nameof(ElementProvider_EntityInQueryIndex.PrepareToExecuteOnEntitiesIn),BindingFlags.Public | BindingFlags.Instance));
                
            _prepareToExecuteOnEntitiesInMethod = new MethodDefinition("PrepareToExecuteOnEntitiesInMethod", MethodAttributes.Public,RuntimesType)
            {
                HasThis = true,
            };
            int counter = 0;
            foreach (var parameter in mimickParametersFrom.Parameters)
                _prepareToExecuteOnEntitiesInMethod.Parameters.Add(new ParameterDefinition($"p{counter++}", parameter.Attributes, parameter.ParameterType));

            _elementProvidersType.Methods.Add(_prepareToExecuteOnEntitiesInMethod);

            var il = _prepareToExecuteOnEntitiesInMethod.Body.GetILProcessor();
            var resultVariable = new VariableDefinition(RuntimesType);
            _prepareToExecuteOnEntitiesInMethod.Body.Variables.Add(resultVariable);
                
            //initialize the result object.
            il.Emit(OpCodes.Ldloca, resultVariable);
            il.Emit(OpCodes.Initobj, RuntimesType);
                
            foreach (var providerInformation in _providers)
            {
                il.Emit(OpCodes.Ldloca, resultVariable);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, _elementProviderToFields[providerInformation].elementField);
                il.Emit(OpCodes.Ldarg_1);
                    
                //most PrepareToExecuteOnEntitiesIn take just the chunk, but some take the chunkIndex and firstEntityIndex as well.
                if (!providerInformation.PrepareToExecuteOnEntitiesTakesJustAChunkParameter)
                {
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_3);
                }

                il.Emit(OpCodes.Call, providerInformation.ProviderPrepareToExecuteOnEntitiesIn);

                //store in the runtime struct that was already on the stack
                il.Emit(OpCodes.Stfld, _elementProviderToFields[providerInformation].runtimeField);
            }
            
            il.Emit(OpCodes.Ldloc, resultVariable);
            il.Emit(OpCodes.Ret);
        }
            
        private void MakeScheduleTimeInitializeMethod()
        {
            _scheduleTimeInitializeMethod = new MethodDefinition("ScheduleTimeInitialize", MethodAttributes.Public,ModuleDefinition.TypeSystem.Void)
            {
                HasThis = true,
                Parameters =
                {
                    new ParameterDefinition("jobComponentSystem", ParameterAttributes.None,ModuleDefinition.ImportReference(typeof(JobComponentSystem))),
                }
            };
            _elementProvidersType.Methods.Add(_scheduleTimeInitializeMethod);

            var il = _scheduleTimeInitializeMethod.Body.GetILProcessor();
            foreach (var providerInformation in _providers)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, _elementProviderToFields[providerInformation].elementField);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(providerInformation.IsReadOnly ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Call, providerInformation.ProviderScheduleTimeInitializeMethod);
            }

            il.Emit(OpCodes.Ret);
        }

        private ModuleDefinition ModuleDefinition => _jobChunkType.Module;


        public static ElementProviderInformations For((MethodDefinition hostMethod, ParameterDefinition definition)[] parameterDefinitions, TypeDefinition jobChunkType)
        {
            var providerInformations = parameterDefinitions .Select(p => ElementProviderInformation.ElementProviderInformationFor(p, null)).ToList();
                
            var parameterTypeNameToProvider = new Dictionary<string, ElementProviderInformation>();
            for (int left = 0; left != providerInformations.Count; left++)
            {
                for (int right = left + 1; right != providerInformations.Count; right++)
                {
                    var leftObject = providerInformations[left];
                    var rightObject = providerInformations[right];

                    if (leftObject == rightObject)
                        continue;

                    if (leftObject.Provider.FullName != rightObject.Provider.FullName) 
                        continue;

                    if (!rightObject.IsReadOnly)
                        providerInformations[left] = rightObject;
                    else
                        providerInformations[right] = leftObject;
                }
                parameterTypeNameToProvider[parameterDefinitions[left].definition.ParameterType.FullName] = providerInformations[left];    
            }

            var result = new ElementProviderInformations()
            {
                _providers = providerInformations.Distinct().ToArray(),
                _parameterTypeNameToProvider = parameterTypeNameToProvider,
                _jobChunkType = jobChunkType
            };
            result.InjectIntoJobChunkType();
            return result;
        }

        public void EmitInvocationToPrepareToRunOnEntitiesInIntoJobChunkExecute(MethodDefinition executeMethod)
        {
            if (executeMethod.DeclaringType != _jobChunkType)
                throw new ArgumentException();
            var il = executeMethod.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0); //<-- for the stdfld at the end
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldflda, _elementsField);
            
            il.Emit(OpCodes.Ldarga,1); // <- the chunk goes by ref
            il.Emit(OpCodes.Ldarg,2);
            il.Emit(OpCodes.Ldarg,3);
            
            il.Emit(OpCodes.Call, _prepareToExecuteOnEntitiesInMethod);
            
            var runtimesVariable = new VariableDefinition(RuntimesType);
            il.Body.Variables.Add(runtimesVariable);
            il.Emit(OpCodes.Stloc, runtimesVariable);
            il.Emit(OpCodes.Ldloca, runtimesVariable);
            il.Emit(OpCodes.Conv_U);
            il.Emit(OpCodes.Stfld, _runtimesField);
        }
        
        public void EmitInvocationToScheduleTimeInitializeIntoJobChunkScheduleTimeInitialize(MethodDefinition jobChunkScheduleTimeInitialize)
        {
            var il = jobChunkScheduleTimeInitialize.Body.GetILProcessor();
            
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldflda, _elementsField);
            
            il.Emit(OpCodes.Ldarg,1);
            il.Emit(OpCodes.Call, _scheduleTimeInitializeMethod);
        }

        public void EmitILToLoadValueForParameterOnStack(ParameterDefinition parameterDefinition,ILProcessor ilProcessor, VariableDefinition loopCounter)
        {
            var provider = _parameterTypeNameToProvider[parameterDefinition.ParameterType.FullName];

            ilProcessor.Emit(OpCodes.Ldarg_2);
            
            ilProcessor.Emit(OpCodes.Ldflda, _elementProviderToFields[provider].runtimeField);
           
            ilProcessor.Emit(OpCodes.Ldloc, loopCounter);
            ilProcessor.Emit(OpCodes.Call, provider.RuntimeForMethod);
                
            if (provider.RuntimeForMethod.ReturnType.IsByReference &&
                !parameterDefinition.ParameterType.IsByReference)
                ilProcessor.Emit(OpCodes.Ldobj, parameterDefinition.ParameterType);

            if (!provider.RuntimeForMethod.ReturnType.IsByReference && parameterDefinition.ParameterType.IsByReference)
            {
                var tempVariable = new VariableDefinition(provider.RuntimeForMethodReturnType);
                ilProcessor.Body.Variables.Add(tempVariable);
                ilProcessor.Emit(OpCodes.Stloc, tempVariable);
                ilProcessor.Emit(OpCodes.Ldloca, tempVariable);
            }
        }
    }
}