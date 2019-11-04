using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

/*
 * Input C# code will be in the format of:
 *
 * using System;
 * using Unity.Entities;
 *     
 * [GenerateAuthoringComponent]
 * public struct BasicComponent : IComponentData
 * {
 *     public float RadiansPerSecond;
 * }
 * 
 * This code defines a standard Component, the difference being that it also informs the IL post processor that we
 * want a corresponding authoring component generated for us.  Currently, this component must live in it's own C# file
 * (due to a limitation with how Unity processses MonoScripts during asset import).  With GenerateAuthoringComponent attribute
 * Unity will generate the following MonoBehaviour with a Convert method:
 * 
 * internal class BasicComponentAuthoring : MonoBehaviour, IConvertGameObjectToEntity
 * {
 *      public float RadiansPerSecond;
 * 
 *      public sealed override void Convert(Entity entity, EntityManager destinationManager, GameObjectConversionSystem conversionSystem)
 *      {
 *          BasicComponent componentData = default(BasicComponent);
 *          componentData.RadiansPerSecond = RadiansPerSecond;
 *      destinationManager.AddComponentData(entity, componentData);
 *      }
 * }
 * 
 * 
 * This process occurs through the following steps:
 * 1. Find all types that inherit from IComponentData and have the GenerateAuthoringComponent attribute.
 * 2. For each type found:
 *     1. Create a new authoring MonoBehaviour type that inherits from MonoBehaviour.
 *     2. For each field found in our component type, create a field in our authoring type.
 * 3. Create a convert method in our authoring component and inside:
 *     1. Initialize a local component type variable.
 *     2. Transfer every field in the authoring component over to the corresponding field in the component.
 *     3. Add a call to EntityManager.AddComponentData(entity, component).
 * 
 */

[assembly: InternalsVisibleTo("Unity.Entities.CodeGen.Tests")]

namespace Unity.Entities.CodeGen
{
    class AuthoringComponentPostProcessor : EntitiesILPostProcessor
    {
        TypeReference m_EntityTypeReference;
        TypeReference m_EntityManagerTypeReference;
        TypeReference m_GameObjectTypeReference;

        protected TypeReference TypeReferenceFor(ModuleDefinition module, string typeName, bool isValueType = false, string ns = "Unity.Entities")
        {
            return AssemblyDefinition.MainModule.ImportReference(new TypeReference(ns, typeName, module, module, isValueType));
        }
        
        bool ShouldGetAuthoringComponent(TypeDefinition typeDefinition)
        {
            var isComponentData = typeDefinition.Interfaces.Any(i => i.InterfaceType.Name == nameof(IComponentData));
            if (!isComponentData)
                return false;

            var hasCreateAuthoringAttribute = typeDefinition.HasCustomAttributes &&
                                              typeDefinition.CustomAttributes.Any(c =>
                                                  c.AttributeType.Name == nameof(GenerateAuthoringComponentAttribute));

            return hasCreateAuthoringAttribute;
        }

        protected override bool PostProcessImpl()
        {
            var mainModule = AssemblyDefinition.MainModule;
            m_EntityTypeReference = mainModule.ImportReference(typeof(Unity.Entities.Entity));
            m_EntityManagerTypeReference = mainModule.ImportReference(typeof(Unity.Entities.EntityManager));
            m_GameObjectTypeReference = mainModule.ImportReference(typeof(UnityEngine.GameObject));
            
            bool madeChange = false;

            var componentDataTypesRequiringAuthoringComponent = mainModule.Types.Where(ShouldGetAuthoringComponent).ToList();
            if (componentDataTypesRequiringAuthoringComponent.Count == 0)
                return madeChange;

            madeChange = componentDataTypesRequiringAuthoringComponent.Count > 0; 
            foreach (var componentDataType in componentDataTypesRequiringAuthoringComponent)
            {
                var authoringTypeNameSpace = componentDataType.Namespace;
                
                // Create our new authoring behaviour
                var authoringType =
                    new TypeDefinition(authoringTypeNameSpace, componentDataType.Name + "Authoring", TypeAttributes.Class)
                    {
                        Scope = componentDataType.Scope,
                        BaseType = mainModule.ImportReference(typeof(UnityEngine.MonoBehaviour)),
                    };
                authoringType.Interfaces.Add(new InterfaceImplementation(AssemblyDefinition.MainModule.ImportReference(typeof(IConvertGameObjectToEntity))));
                var DisallowMultipleComponentsAttribute = mainModule.ImportReference(typeof(UnityEngine.DisallowMultipleComponent).GetConstructors().Single(c=>!c.GetParameters().Any()));
                authoringType.CustomAttributes.Add(new CustomAttribute(DisallowMultipleComponentsAttribute));

                // Add our new authoring behaviour type to the module
                mainModule.Types.Add(authoringType);
                
                var dataFieldsToAuthoringFields = new Dictionary<FieldDefinition, FieldDefinition>();
                foreach (var field in componentDataType.Fields.Where(f => !f.IsStatic && f.IsPublic && !f.IsPrivate))
                {
                    FieldDefinition AuthoringFieldDefinitionFor(FieldDefinition componentDataField)
                    {
                        var fieldType = componentDataField.FieldType.TypeReferenceEquals(m_EntityTypeReference)
                            ? m_GameObjectTypeReference
                            : componentDataField.FieldType;
                        
                        var newField = new FieldDefinition(componentDataField.Name, componentDataField.Attributes,fieldType);
                        if (componentDataField.HasCustomAttributes)
                        {
                            foreach (var ca in componentDataField.CustomAttributes)
                                newField.CustomAttributes.Add(ca);
                        }

                        return newField;
                    }

                    var authoringFieldDefinition = AuthoringFieldDefinitionFor(field);
                    dataFieldsToAuthoringFields.Add(field, authoringFieldDefinition);
                    authoringType.Fields.Add(authoringFieldDefinition);
                }

                CreateConvertMethod(authoringType, componentDataType);
            }

            return madeChange;
        }

        void CreateConvertMethod(TypeDefinition authoringType, TypeDefinition componentDataType)
        {
            var convertMethod = new MethodDefinition("Convert",
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig |
                MethodAttributes.Virtual, AssemblyDefinition.MainModule.TypeSystem.Void)
            {
                Parameters =
                {
                    new ParameterDefinition("entity", ParameterAttributes.None, m_EntityTypeReference),
                    new ParameterDefinition("destinationManager", ParameterAttributes.None, m_EntityManagerTypeReference),
                    new ParameterDefinition("conversionSystem", ParameterAttributes.None, 
                        AssemblyDefinition.MainModule.ImportReference(typeof(GameObjectConversionSystem)))
                }
            };
            authoringType.Methods.Add(convertMethod);

            // Make a local variable which we'll populate with the values stored in the MonoBehaviour
            var variableDefinition = new VariableDefinition(componentDataType);
            convertMethod.Body.Variables.Add(variableDefinition);

            var ilProcessor = convertMethod.Body.GetILProcessor();

            // Initialize the local variable.  (we might not need this, but all c# compilers emit it, so let's play it safe for now
            ilProcessor.Emit(OpCodes.Ldloca_S, variableDefinition);
            ilProcessor.Emit(OpCodes.Initobj, componentDataType);
            
            var getPrimaryEntityGameObjectMethod = AssemblyDefinition.MainModule.ImportReference(
                typeof(GameObjectConversionSystem).GetMethod("GetPrimaryEntity", new Type[] { typeof(UnityEngine.GameObject) }));
            var getPrimaryEntityComponentMethod = AssemblyDefinition.MainModule.ImportReference(
                typeof(GameObjectConversionSystem).GetMethod("GetPrimaryEntity", new Type[] { typeof(UnityEngine.Component) }));

            // Let's transfer every field in the MonoBehaviour over to the corresponding field in the IComponentData
            foreach (var field in authoringType.Fields)
            {
                var destinationField = componentDataType.Fields.Single(f => f.Name == field.Name);

                // Load the local iComponentData we are populating, so we can later write to it
                ilProcessor.Emit(OpCodes.Ldloca_S, variableDefinition);

                if (destinationField.FieldType.TypeReferenceEquals(m_EntityTypeReference))
                {
                    // conversionSystem.GetPrimaryEntity(myThing);
                    ilProcessor.Emit(OpCodes.Ldarg_3);
                    ilProcessor.Emit(OpCodes.Ldarg_0);
                    ilProcessor.Emit(OpCodes.Ldfld, field);

                    var methodToCall = field.FieldType.TypeReferenceEquals(m_GameObjectTypeReference)
                        ? getPrimaryEntityGameObjectMethod
                        : getPrimaryEntityComponentMethod;
                    ilProcessor.Emit(OpCodes.Callvirt, methodToCall);
                }
                else
                {
                    // Load this (our MonoBehaviour itself)
                    ilProcessor.Emit(OpCodes.Ldarg_0);
                    // Load the field in question from the MonoBehaviour
                    ilProcessor.Emit(OpCodes.Ldfld, field);
                }

                // Store it to the IComponentData we already placed on the stack
                ilProcessor.Emit(OpCodes.Stfld, destinationField);
            }

            // Now that our local IComponentData is properly setup, the only thing left for us is to call:
            // entityManager.AddComponentData(entity, myPopulatedIComponentData). 
            // IL method arguments go on the stack from first to last so:
            ilProcessor.Emit(OpCodes.Ldarg_2); //entityManager
            ilProcessor.Emit(OpCodes.Ldarg_1); //entity
            ilProcessor.Emit(OpCodes.Ldloc_0); //myPopulatedIComponentData

            // Build a MethodReference to EntityManager.AddComponentData<T>(Entity target, T payload);
            var addComponentDataMethodReference =
                new MethodReference("AddComponentData", AssemblyDefinition.MainModule.TypeSystem.Void, m_EntityManagerTypeReference)
                {   
                    HasThis = true,
                    Parameters =
                    {
                        new ParameterDefinition("entity", ParameterAttributes.None, m_EntityTypeReference),
                    },
                    ReturnType = AssemblyDefinition.MainModule.TypeSystem.Boolean
                };
            var genericParameter = new GenericParameter("T", addComponentDataMethodReference);
            addComponentDataMethodReference.GenericParameters.Add(genericParameter);
            addComponentDataMethodReference.Parameters.Add(new ParameterDefinition("payload", ParameterAttributes.None,
                genericParameter));

            // Since AddComponentData<T> is a generic method, we cannot call it super easily.  
            // We have to wrap the generic method reference into a GenericInstanceMethod, 
            // which let's us specify what we want to use for T for this specific invocation. 
            // In our case T is the IComponentData we're operating on
            var genericInstanceMethod = new GenericInstanceMethod(addComponentDataMethodReference)
            {
                GenericArguments = {componentDataType},
            };
            ilProcessor.Emit(OpCodes.Callvirt, genericInstanceMethod);
            
            // Pop off return value since AddComponentData returns a bool
            ilProcessor.Emit(OpCodes.Pop);

            // We're done already!  Easy peasy.
            ilProcessor.Emit(OpCodes.Ret);
        }
    }
}
