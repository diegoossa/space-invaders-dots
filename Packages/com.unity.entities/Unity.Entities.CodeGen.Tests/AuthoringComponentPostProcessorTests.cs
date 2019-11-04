using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.CodeGen.Tests
{
    [TestFixture]
    public class AuthoringComponentPostProcessorTests
    {
        static readonly string k_TestFolderPath = "Assets/AuthoringComponentTestFolder";
        
        [SetUp]
	    public void Setup()
	    {
#if !UNITY_2019_3_OR_NEWER
			Assert.Ignore("These tests test a 2019.3 only feature")
#endif
	    }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
            DeleteGeneratedScripts();

            var testObject = GameObject.Find(k_TestObject);
            if (testObject)
                GameObject.DestroyImmediate(testObject);
        }
        
        static void GenerateScript(string scriptName, string scriptContents)
        {
            DeleteGeneratedScripts();

            Directory.CreateDirectory(k_TestFolderPath);
            File.WriteAllText(Path.Combine(k_TestFolderPath, $"{scriptName}.cs"), scriptContents);
        }
        
        static void DeleteGeneratedScripts()
        {
            if (Directory.Exists(k_TestFolderPath))
                Directory.Delete(k_TestFolderPath, true);
            if (File.Exists(k_TestFolderPath + ".meta"))
                File.Delete(k_TestFolderPath + ".meta");
        }
        
        public static Type GetTypeByName(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.Name == name)
                        return type;
                }
            }
 
            return null;
        }
        
        static bool IsComponentInWorld(World world, ComponentType componentType)
        {
            using (var entities = world.EntityManager.GetAllEntities())
            {
                foreach (var entity in entities)
                {
                    if (world.EntityManager.HasComponent(entity, componentType))
                        return true;
                }
            }

            return false;
        }

        readonly string k_BasicComponentName = "BasicComponent";
        readonly string k_BasicComponentContent = @"
using System;
using Unity.Entities;

// Serializable attribute is for editor support.
[GenerateAuthoringComponent]
public struct BasicComponent : IComponentData
{
    public float RadiansPerSecond;
}";
        readonly string k_BasicComponentAuthoringName = "BasicComponentAuthoring";
        readonly string k_AuthoringFieldName = "RadiansPerSecond";
        readonly string k_TestObject = "TestObject";

        [UnityTest]
        public IEnumerator AuthoringComponent_IntegrationTest()
        {
            GenerateScript(k_BasicComponentName, k_BasicComponentContent);
            yield return new RecompileScripts(false);
            AssetDatabase.Refresh();

            var generatedType = GetTypeByName(k_BasicComponentAuthoringName);
            Assert.IsNotNull(generatedType);
            
            var go = new GameObject(k_TestObject);
            var newAuthoringComponent = go.AddComponent(generatedType);
            Assert.IsNotNull(newAuthoringComponent);
            var radiansField = newAuthoringComponent.GetType().GetField(k_AuthoringFieldName);
            Assert.IsNotNull(radiansField);
            go.AddComponent(GetTypeByName("ConvertToEntity"));

            yield return new EnterPlayMode();
            
            var gameObject = GameObject.Find(k_TestObject);
            Assert.IsNull(gameObject);

            var componentType = GetTypeByName(k_BasicComponentName);
            Assert.IsTrue(IsComponentInWorld(World.DefaultGameObjectInjectionWorld, componentType));
        }
    }
}