using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Build.Tests
{
    public interface ITestComponent { }

    struct ComponentA : ITestComponent
    {
        public int Integer;
        public float Float;
        public string String;
    }

    struct ComponentB : ITestComponent
    {
        public byte Byte;
        public double Double;
        public short Short;
    }

    class ComplexComponent : ITestComponent
    {
        public int Integer;
        public float Float;
        public string String = string.Empty;
        public ComponentA Nested;
        public List<int> ListInteger = new List<int>();
    }

    class TestComponentContainer : ComponentContainer<ITestComponent> { }

    class ComponentContainerTests
    {
        /// <summary>
        /// Verify that <see cref="ComponentContainer{ITestComponent}"/> can store complex components and get back the value.
        /// </summary>
        [Test]
        public void ComponentValuesAreValid()
        {
            var container = ScriptableObject.CreateInstance<TestComponentContainer>();
            var component = new ComplexComponent
            {
                Integer = 1,
                Float = 123.456f,
                String = "test",
                Nested = new ComponentA
                {
                    Integer = 42
                },
                ListInteger = new List<int> { 1, 1, 2, 3, 5, 8, 13 }
            };
            container.SetComponent(component);

            var value = container.GetComponent<ComplexComponent>();
            Assert.That(value.Integer, Is.EqualTo(1));
            Assert.That(value.Float, Is.EqualTo(123.456f));
            Assert.That(value.String, Is.EqualTo("test"));
            Assert.That(value.Nested.Integer, Is.EqualTo(42));
            Assert.That(value.ListInteger, Is.EquivalentTo(new List<int> { 1, 1, 2, 3, 5, 8, 13 }));
        }

        /// <summary>
        /// Verify that <see cref="ComponentContainer{ITestComponent}"/> can inherit values from dependencies.
        /// </summary>
        [Test]
        public void ComponentInheritance()
        {
            var containerA = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerA.SetComponent(new ComponentA
            {
                Integer = 1,
                Float = 123.456f,
                String = "test"
            });

            var containerB = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerB.AddDependency(containerA);
            containerB.SetComponent(new ComponentB
            {
                Byte = 255,
                Double = 3.14159265358979323846,
                Short = 32767
            });

            Assert.That(containerB.IsComponentInherited<ComponentA>(), Is.True);
            Assert.That(containerB.GetComponent<ComponentA>(), Is.EqualTo(new ComponentA
            {
                Integer = 1,
                Float = 123.456f,
                String = "test"
            }));

            Assert.That(containerB.IsComponentInherited<ComponentB>(), Is.False);
            Assert.That(containerB.GetComponent<ComponentB>(), Is.EqualTo(new ComponentB
            {
                Byte = 255,
                Double = 3.14159265358979323846,
                Short = 32767
            }));
        }

        /// <summary>
        /// Verify that <see cref="ComponentContainer{ITestComponent}"/> can inherit values from multiple dependencies.
        /// </summary>
        [Test]
        public void ComponentInheritanceFromMultipleDependencies()
        {
            var containerA = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerA.SetComponent(new ComponentA
            {
                Integer = 1,
                Float = 123.456f,
                String = "test"
            });

            var containerB = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerB.AddDependency(containerA);
            containerB.SetComponent(new ComponentB
            {
                Byte = 255,
                Double = 3.14159265358979323846,
                Short = 32767
            });

            var containerC = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerC.SetComponent(new ComplexComponent
            {
                Integer = 1,
                Float = 123.456f,
                String = "test",
                Nested = new ComponentA
                {
                    Integer = 42
                },
                ListInteger = new List<int> { 1, 1, 2, 3, 5, 8, 13 }
            });

            var containerD = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerD.AddDependencies(containerB, containerC);

            Assert.That(containerD.IsComponentInherited<ComponentA>(), Is.True);
            Assert.That(containerD.GetComponent<ComponentA>(), Is.EqualTo(new ComponentA
            {
                Integer = 1,
                Float = 123.456f,
                String = "test"
            }));

            Assert.That(containerD.IsComponentInherited<ComponentB>(), Is.True);
            Assert.That(containerD.GetComponent<ComponentB>(), Is.EqualTo(new ComponentB
            {
                Byte = 255,
                Double = 3.14159265358979323846,
                Short = 32767
            }));

            Assert.That(containerD.IsComponentInherited<ComplexComponent>(), Is.True);
            var complexComponent = containerD.GetComponent<ComplexComponent>();
            Assert.That(complexComponent.Integer, Is.EqualTo(1));
            Assert.That(complexComponent.Float, Is.EqualTo(123.456f));
            Assert.That(complexComponent.String, Is.EqualTo("test"));
            Assert.That(complexComponent.Nested.Integer, Is.EqualTo(42));
            Assert.That(complexComponent.ListInteger, Is.EquivalentTo(new List<int> { 1, 1, 2, 3, 5, 8, 13 }));
        }

        /// <summary>
        /// Verify that <see cref="ComponentContainer{ITestComponent}"/> can override values from dependencies.
        /// </summary>
        [Test]
        public void ComponentOverrides()
        {
            var containerA = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerA.SetComponent(new ComponentA
            {
                Integer = 1,
                Float = 123.456f,
                String = "test"
            });

            var containerB = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerB.AddDependency(containerA);
            containerB.SetComponent(new ComponentB
            {
                Byte = 255,
                Double = 3.14159265358979323846,
                Short = 32767
            });

            var component = containerB.GetComponent<ComponentA>();
            component.Integer = 2;
            containerB.SetComponent(component);

            Assert.That(containerB.IsComponentOverridden<ComponentA>(), Is.True);
            Assert.That(containerB.GetComponent<ComponentA>(), Is.EqualTo(new ComponentA
            {
                Integer = 2,
                Float = 123.456f,
                String = "test"
            }));

            Assert.That(containerB.IsComponentOverridden<ComponentB>(), Is.False);
            Assert.That(containerB.GetComponent<ComponentB>(), Is.EqualTo(new ComponentB
            {
                Byte = 255,
                Double = 3.14159265358979323846,
                Short = 32767
            }));
        }

        /// <summary>
        /// Verify that <see cref="ComponentContainer{ITestComponent}"/> can override values from multiple dependencies.
        /// </summary>
        [Test]
        public void ComponentOverridesFromMultipleDependencies()
        {
            var containerA = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerA.SetComponent(new ComponentA { Integer = 1 });

            var containerB = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerB.AddDependency(containerA);

            var componentA = containerB.GetComponent<ComponentA>();
            componentA.Float = 123.456f;
            containerB.SetComponent(componentA);

            var containerC = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerC.AddDependency(containerB);

            componentA = containerC.GetComponent<ComponentA>();
            componentA.String = "test";
            containerC.SetComponent(componentA);

            var containerD = ScriptableObject.CreateInstance<TestComponentContainer>();
            containerD.AddDependency(containerC);

            var value = containerD.GetComponent<ComponentA>();
            Assert.That(value.Integer, Is.EqualTo(1));
            Assert.That(value.Float, Is.EqualTo(123.456f));
            Assert.That(value.String, Is.EqualTo("test"));
        }

        /// <summary>
        /// Verify that ComponentContainer can serialize, deserialize and reserialize to JSON without losing any values.
        /// </summary>
        [Test]
        public void ComponentSerialization()
        {
            var container = ScriptableObject.CreateInstance<TestComponentContainer>();
            container.SetComponent(new ComplexComponent
            {
                Integer = 1,
                Float = 123.456f,
                String = "test",
                Nested = new ComponentA
                {
                    Integer = 42
                },
                ListInteger = new List<int> { 1, 1, 2, 3, 5, 8, 13 }
            });

            var json = container.SerializeToJson();
            Assert.That(json.Length, Is.GreaterThan(3));

            var deserializedContainer = ScriptableObject.CreateInstance<TestComponentContainer>();
            TestComponentContainer.DeserializeFromJson(deserializedContainer, json);

            var component = deserializedContainer.GetComponent<ComplexComponent>();
            Assert.That(component.Integer, Is.EqualTo(1));
            Assert.That(component.Float, Is.EqualTo(123.456f));
            Assert.That(component.String, Is.EqualTo("test"));
            Assert.That(component.Nested.Integer, Is.EqualTo(42));
            Assert.That(component.ListInteger, Is.EquivalentTo(new List<int> { 1, 1, 2, 3, 5, 8, 13 }));

            var reserializedJson = deserializedContainer.SerializeToJson();
            Assert.That(reserializedJson, Is.EqualTo(json));
        }

        [Test]
        public void DeserializeInvalidJson_ShouldNotThrowException()
        {
            var container = ScriptableObject.CreateInstance<TestComponentContainer>();
            LogAssert.Expect(LogType.Error, "Input json was invalid. ExpectedType=[Value] ActualType=[EndObject] ActualChar=['}'] at Line=[1] at Character=[47]");
            TestComponentContainer.DeserializeFromJson(container, "{\"Dependencies\": [], \"Components\": [{\"$type\": }, {\"$type\": }]\n}");
        }

        [Test]
        public void DeserializeInvalidComponent_ShouldNotResetEntireBuildSettings()
        {
            var container = ScriptableObject.CreateInstance<TestComponentContainer>();
            LogAssert.Expect(LogType.Error, "Could not resolve type from type name 'Some.InvalidComponent.Name, Unknown.Assembly' when reading Components.");
            TestComponentContainer.DeserializeFromJson(container, "{\"Dependencies\": [], \"Components\": [{\"$type\": \"Unity.Build.Tests.ComponentA, Unity.Build.Tests\"}, {\"$type\": \"Some.InvalidComponent.Name, Unknown.Assembly\"}]\n}");
            Assert.That(container.HasComponent<ComponentA>(), Is.True);
        }

        [Test]
        public void DeserializeInvalidDependency_ShouldNotResetEntireBuildSettings()
        {
            var container = ScriptableObject.CreateInstance<TestComponentContainer>();
            TestComponentContainer.DeserializeFromJson(container, "{\"Dependencies\": [null, \"\"], \"Components\": [{\"$type\": \"Unity.Build.Tests.ComponentA, Unity.Build.Tests\"}]\n}");
            Assert.That(container.HasComponent<ComponentA>(), Is.True);
            Assert.That(container.GetDependencies().Count, Is.Zero);
        }
    }
}
