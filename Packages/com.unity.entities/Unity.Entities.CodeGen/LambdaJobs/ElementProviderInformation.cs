using System;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Entities.CodeGeneratedJobForEach;

namespace Unity.Entities.CodeGen
{
    class ElementProviderInformation
    {
        public readonly TypeReference Provider;
        public readonly MethodReference ProviderScheduleTimeInitializeMethod;
        public readonly MethodReference ProviderPrepareToExecuteOnEntitiesIn;
        public readonly TypeReference ProviderRuntime;
        public readonly MethodReference RuntimeForMethod;
        public readonly TypeReference RuntimeForMethodReturnType;
        public readonly bool IsReadOnly;

        ElementProviderInformation(TypeReference provider, TypeReference providerRuntime, bool readOnly)
        {
            Provider = provider;

            (ProviderScheduleTimeInitializeMethod,_) = MethodReferenceFor(nameof(ElementProvider_Entity.ScheduleTimeInitialize), Provider);
            (ProviderPrepareToExecuteOnEntitiesIn,_) = MethodReferenceFor(nameof(ElementProvider_Entity.PrepareToExecuteOnEntitiesIn), Provider);

            ProviderRuntime = providerRuntime;
            (RuntimeForMethod, RuntimeForMethodReturnType) = MethodReferenceFor("For", ProviderRuntime);
            IsReadOnly = readOnly;
        }

        static (MethodReference methodReference, TypeReference specializedReturnType) MethodReferenceFor(string methodName, TypeReference typeReference)
        {
            var resolvedMethod = typeReference.Module.ImportReference(typeReference.Resolve().Methods.Single(m => m.Name == methodName));
            
            var resolvedMethod2 = typeReference.Module.ImportReference(typeReference.Resolve().Methods.Single(m => m.Name == methodName));
            var specializedReturnType = resolvedMethod2.ReturnType;
            //I tried coding this up in a totally generic way where we can correctly figure out the "specialized" type of the returntype, but our case
            //with ElementProvider_DynamicBuffer<Something>.Runtime takes a better person than me apparently. No worries! this code only needs to deal
            //with this one generic weirdo case correctly, so let's just make a specialized codepath for it, and do the thing that we know is correct
            //just for that once case.
            if (methodName == "For" && typeReference.DeclaringType.Name == typeof(ElementProvider_DynamicBuffer<>).Name)
            {
                ((GenericInstanceType)specializedReturnType).GenericArguments[0] =  ((GenericInstanceType) typeReference).GenericArguments.Single();
            }


            var result = new MethodReference(resolvedMethod.Name, resolvedMethod.ReturnType, typeReference)
            {
                HasThis = resolvedMethod.HasThis,
            };
            foreach (var pd in resolvedMethod.Parameters)
                result.Parameters.Add(pd);
            return (result, specializedReturnType);
        }
        
        public bool PrepareToExecuteOnEntitiesTakesJustAChunkParameter => ProviderPrepareToExecuteOnEntitiesIn.Parameters.Count() == 1;
 

        public static ElementProviderInformation ElementProviderInformationFor((MethodDefinition hostMethod, ParameterDefinition definition) parameter, Instruction diagnosticInstructionInCaseOfError)
        {
            var moduleDefinition = parameter.hostMethod.Module;    
            (TypeReference provider, TypeReference providerRuntime) ImportReferencesFor(Type providerType, Type runtimeType, TypeReference typeOfT)
            {
                var provider = moduleDefinition
                    .ImportReference(providerType)
                    .MakeGenericInstanceType(typeOfT);
                var providerRuntime = moduleDefinition.ImportReference(runtimeType).MakeGenericInstanceType(typeOfT);
                
                return (provider, providerRuntime);
            }

            var parameterType = parameter.definition.ParameterType;
            if (parameterType.IsIComponentData())
            {
                var readOnly = !parameter.definition.ParameterType.IsByReference || HasCompilerServicesIsReadOnlyAttribute(parameter.definition);
                var (provider,providerRuntime) = ImportReferencesFor(typeof(ElementProvider_IComponentData<>),typeof(ElementProvider_IComponentData<>.Runtime), parameter.definition.ParameterType.GetElementType());
                return new ElementProviderInformation(provider, providerRuntime, readOnly);
            }

            if (parameterType.IsDynamicBufferOfT())
            {
                var typeRef = parameterType;
                if (parameterType is ByReferenceType referenceType)
                    typeRef = referenceType.ElementType;
                
                GenericInstanceType bufferOfT = (GenericInstanceType)typeRef;
                TypeReference bufferElementType = bufferOfT.GenericArguments[0];
                var (provider,providerRuntime) = ImportReferencesFor(typeof(ElementProvider_DynamicBuffer<>),typeof(ElementProvider_DynamicBuffer<>.Runtime), bufferElementType);
                return new ElementProviderInformation(provider, providerRuntime, false);
            }

            if (parameterType.TypeReferenceEquals(moduleDefinition.ImportReference(typeof(Entity))))
            {
                var provider = moduleDefinition.ImportReference(typeof(ElementProvider_Entity));
                var runtime = moduleDefinition.ImportReference(typeof(ElementProvider_Entity.Runtime));
                
                return new ElementProviderInformation(provider, runtime, true);
            }
            
            if (parameterType.FullName == moduleDefinition.TypeSystem.Int32.FullName)
            {
                var allNames = new[] {"entityInQueryIndex", "nativeThreadIndex"};
                string entityInQueryIndexName = allNames[0];
                string nativeThreadIndexName = allNames[1];
                
                if (parameter.definition.Name == entityInQueryIndexName)
                {
                    var provider = moduleDefinition.ImportReference(typeof(ElementProvider_EntityInQueryIndex));
                    var runtime = moduleDefinition.ImportReference(typeof(ElementProvider_EntityInQueryIndex.Runtime));
                    return new ElementProviderInformation(provider, runtime, true);
                }

                if (parameter.definition.Name == nativeThreadIndexName)
                {
                    var provider = moduleDefinition.ImportReference(typeof(ElementProvider_NativeThreadIndex));
                    var runtime = moduleDefinition.ImportReference(typeof(ElementProvider_NativeThreadIndex.Runtime));
                    return new ElementProviderInformation(provider, runtime, true);
                }

                UserError.DC0014(parameter.hostMethod, diagnosticInstructionInCaseOfError, parameter.definition, allNames).Throw();
            }

            UserError.DC0005(parameter.hostMethod, diagnosticInstructionInCaseOfError, parameter.definition).Throw();
            return null;
        }
        
        static bool HasCompilerServicesIsReadOnlyAttribute(ParameterDefinition p)
        {
            return p.HasCustomAttributes && p.CustomAttributes.Any(c =>c.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
        }
    }
}