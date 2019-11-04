using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Properties;
using Unity.Serialization;
using Unity.Serialization.Json;
using UnityEditor;
using UnityEngine;
using Property = Unity.Properties.PropertyAttribute;

namespace Unity.Build
{
    /// <summary>
    /// Base class that stores a set of unique component.
    /// Other <see cref="ComponentContainer{TObject}"/> can be added as dependencies to get inherited or overridden items.
    /// </summary>
    /// <typeparam name="TComponent">Components base type.</typeparam>
    public class ComponentContainer<TComponent> : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] string m_AssetAsJson;
        readonly List<ComponentContainer<TComponent>> m_NonAssetDependencies = new List<ComponentContainer<TComponent>>();
        [Property] readonly List<string> Dependencies = new List<string>();
        [Property] readonly List<TComponent> Components = new List<TComponent>();

        /// <summary>
        /// Event invoked when <see cref="ComponentContainer{TObject}"/> registers <see cref="JsonVisitor"/> used for serialization.
        /// It provides an opportunity to register additional property visitor adapters.
        /// </summary>
        protected static event Action<JsonVisitor> JsonVisitorRegistration = delegate { };

        /// <summary>
        /// Determine if a <see cref="Type"/> component is stored in this <see cref="ComponentContainer{TObject}"/> or its dependencies.
        /// </summary>
        /// <param name="type"><see cref="Type"/> of the component.</param>
        public bool HasComponent(Type type)
        {
            foreach (var component in Components)
            {
                if (component.GetType() == type)
                {
                    return true;
                }
            }

            foreach (var dependency in GetDependencies())
            {
                if (dependency.HasComponent(type))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determine if a <typeparamref name="T"/> component is stored in this <see cref="ComponentContainer{TObject}"/> or its dependencies.
        /// </summary>
        /// <typeparam name="T">Type of the component.</typeparam>
        public bool HasComponent<T>() where T : TComponent
        {
            return HasComponent(typeof(T));
        }

        /// <summary>
        /// Determine if a <see cref="Type"/> component is inherited from a dependency.
        /// </summary>
        /// <param name="type"><see cref="Type"/> of the component.</param>
        public bool IsComponentInherited(Type type)
        {
            foreach (var component in Components)
            {
                if (component.GetType() == type)
                {
                    return false;
                }
            }

            foreach (var dependency in GetDependencies())
            {
                if (dependency.HasComponent(type))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determine if a <typeparamref name="T"/> component is inherited from a dependency.
        /// </summary>
        /// <typeparam name="T">Type of the component.</typeparam>
        public bool IsComponentInherited<T>() where T : TComponent
        {
            return IsComponentInherited(typeof(T));
        }

        /// <summary>
        /// Determine if a <see cref="Type"/> component overrides a dependency.
        /// </summary>
        /// <param name="type"><see cref="Type"/> of the component.</param>
        public bool IsComponentOverridden(Type type)
        {
            foreach (var component in Components)
            {
                if (component.GetType() == type)
                {
                    foreach (var dependency in GetDependencies())
                    {
                        if (dependency.HasComponent(type))
                        {
                            return true;
                        }
                    }
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Determine if a <typeparamref name="T"/> component overrides a dependency.
        /// </summary>
        /// <typeparam name="T">Type of the component.</typeparam>
        public bool IsComponentOverridden<T>() where T : TComponent
        {
            return IsComponentOverridden(typeof(T));
        }

        /// <summary>
        /// Get the value of a <see cref="Type"/> component.
        /// </summary>
        /// <param name="type"><see cref="Type"/> of the component.</param>
        public TComponent GetComponent(Type type)
        {
            if (!TryGetComponent(type, out var result))
            {
                throw new InvalidOperationException($"Component of type '{type.FullName}' not found.");
            }

            return result;
        }

        /// <summary>
        /// Get the value of a <typeparamref name="T"/> component.
        /// </summary>
        /// <typeparam name="T">Type of the component.</typeparam>
        public T GetComponent<T>() where T : TComponent
        {
            return (T)GetComponent(typeof(T));
        }

        /// <summary>
        /// Try to get the value of a <see cref="Type"/> component.
        /// </summary>
        /// <param name="type"><see cref="Type"/> of the component.</param>
        /// <param name="value">Out value of the component.</param>
        public bool TryGetComponent(Type type, out TComponent value)
        {
            var found = false;
            var result = Activator.CreateInstance(type);

            foreach (var dependency in GetDependencies())
            {
                if (dependency.TryGetComponent(type, out var data))
                {
                    found = true;
                    PropertyContainer.Transfer(ref result, ref data);
                }
            }

            foreach (var component in Components)
            {
                if (component.GetType() == type)
                {
                    found = true;
                    var data = component;
                    PropertyContainer.Transfer(ref result, ref data);
                    break;
                }
            }

            value = (TComponent)result;
            return found;
        }

        /// <summary>
        /// Try to get the value of a <typeparamref name="T"/> component.
        /// </summary>
        /// <param name="value">Out value of the component.</param>
        /// <typeparam name="T">Type of the component.</typeparam>
        public bool TryGetComponent<T>(out T value) where T : TComponent
        {
            if (TryGetComponent(typeof(T), out var result))
            {
                value = (T)result;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Get a flatten list of all components from this <see cref="ComponentContainer{TObject}"/> and its dependencies.
        /// </summary>
        public List<TComponent> GetComponents()
        {
            var lookup = new Dictionary<Type, TComponent>();

            foreach (var dependency in GetDependencies())
            {
                var components = dependency.GetComponents();
                foreach (var component in components)
                {
                    lookup[component.GetType()] = component;
                }
            }

            foreach (var component in Components)
            {
                lookup[component.GetType()] = CopyComponent(component);
            }

            return lookup.Values.ToList();
        }

        /// <summary>
        /// Set the value of a <see cref="Type"/> component.
        /// </summary>
        /// <param name="type"><see cref="Type"/> of the component.</param>
        /// <param name="value">Value of the component to set.</param>
        public void SetComponent(Type type, TComponent value)
        {
            for (var i = 0; i < Components.Count; ++i)
            {
                if (Components[i].GetType() == type)
                {
                    Components[i] = CopyComponent(value);
                    return;
                }
            }

            Components.Add(CopyComponent(value));
        }

        /// <summary>
        /// Set the value of a <typeparamref name="T"/> component.
        /// </summary>
        /// <param name="value">Value of the component to set.</param>
        /// <typeparam name="T">Type of the component.</typeparam>
        public void SetComponent<T>(T value) where T : TComponent
        {
            SetComponent(typeof(T), value);
        }

        /// <summary>
        /// Remove a <see cref="Type"/> component from this <see cref="ComponentContainer{TObject}"/>.
        /// </summary>
        /// <param name="type"><see cref="Type"/> of the component.</param>
        public bool RemoveComponent(Type type)
        {
            for (var i = 0; i < Components.Count; ++i)
            {
                if (Components[i].GetType() == type)
                {
                    Components.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Remove a <typeparamref name="T"/> component from this <see cref="ComponentContainer{TObject}"/>.
        /// </summary>
        /// <typeparam name="T">Type of the component.</typeparam>
        public bool RemoveComponent<T>() where T : TComponent
        {
            return RemoveComponent(typeof(T));
        }

        /// <summary>
        /// Remove all components from this <see cref="ComponentContainer{TObject}"/>.
        /// </summary>
        public void ClearComponents() => Components.Clear();

        /// <summary>
        /// Visit a flatten list of all components from this <see cref="ComponentContainer{TObject}"/> and its dependencies.
        /// </summary>
        /// <param name="visitor">The visitor to use for visiting each component.</param>
        public void VisitComponents(IPropertyVisitor visitor)
        {
            var changeTracker = new ChangeTracker();
            VisitComponents(visitor, ref changeTracker);
        }

        /// <summary>
        /// Visit a flatten list of all components from this <see cref="ComponentContainer{TObject}"/> and its dependencies.
        /// </summary>
        /// <param name="visitor">The visitor to use for visiting each component.</param>
        /// <param name="changeTracker">The change tracker to record changes while visiting.</param>
        public void VisitComponents(IPropertyVisitor visitor, ref ChangeTracker changeTracker)
        {
            var components = GetComponents();
            for (var i = 0; i < components.Count; ++i)
            {
                var component = components[i];
                PropertyContainer.Visit(ref component, visitor, ref changeTracker);
            }
        }

        /// <summary>
        /// Add a <see cref="ComponentContainer{TObject}"/> dependency.
        /// </summary>
        /// <param name="dependency">The dependency to add to this <see cref="ComponentContainer{TObject}"/>.</param>
        public void AddDependency(ComponentContainer<TComponent> dependency)
        {
            if (dependency == null)
            {
                throw new ArgumentNullException(nameof(dependency));
            }

            var assetPath = AssetDatabase.GetAssetPath(dependency);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.IsNullOrEmpty(assetGuid))
            {
                if (!Dependencies.Contains(assetGuid))
                {
                    Dependencies.Add(assetGuid);
                }
            }
            else
            {
                if (!m_NonAssetDependencies.Contains(dependency))
                {
                    m_NonAssetDependencies.Add(dependency);
                }
            }
        }

        /// <summary>
        /// Add multiple <see cref="ComponentContainer{TObject}"/> dependencies.
        /// </summary>
        /// <param name="dependencies">The dependencies to add to this <see cref="ComponentContainer{TObject}"/>.</param>
        public void AddDependencies(params ComponentContainer<TComponent>[] dependencies)
        {
            foreach (var dependency in dependencies)
            {
                AddDependency(dependency);
            }
        }

        /// <summary>
        /// Get a list of all the dependencies for this <see cref="ComponentContainer{TObject}"/>.
        /// </summary>
        public IReadOnlyList<ComponentContainer<TComponent>> GetDependencies()
        {
            var dependencies = new List<ComponentContainer<TComponent>>(m_NonAssetDependencies);
            foreach (var assetGuid in Dependencies)
            {
                var dependency = LoadDependency(assetGuid);
                if (dependency != null)
                {
                    dependencies.Add(dependency);
                }
            }
            return dependencies;
        }

        /// <summary>
        /// Remove a <see cref="ComponentContainer{TObject}"/> dependency.
        /// </summary>
        /// <param name="dependency">The dependency to remove from this <see cref="ComponentContainer{TObject}"/>.</param>
        public void RemoveDependency(ComponentContainer<TComponent> dependency)
        {
            if (dependency == null)
            {
                throw new ArgumentNullException(nameof(dependency));
            }

            var assetPath = AssetDatabase.GetAssetPath(dependency);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.IsNullOrEmpty(assetGuid))
            {
                Dependencies.Remove(assetGuid);
            }
            else
            {
                m_NonAssetDependencies.Remove(dependency);
            }
        }

        /// <summary>
        /// Remove multiple <see cref="ComponentContainer{TObject}"/> dependencies.
        /// </summary>
        /// <param name="dependencies">The dependencies to remove from this <see cref="ComponentContainer{TObject}"/>.</param>
        public void RemoveDependencies(params ComponentContainer<TComponent>[] dependencies)
        {
            foreach (var dependency in dependencies)
            {
                RemoveDependency(dependency);
            }
        }

        /// <summary>
        /// Remove all dependencies from this <see cref="ComponentContainer{TObject}"/>.
        /// </summary>
        public void ClearDependencies()
        {
            m_NonAssetDependencies.Clear();
            Dependencies.Clear();
        }

        /// <summary>
        /// Serialize this <see cref="ComponentContainer{TObject}"/> to a JSON <see cref="string"/>.
        /// </summary>
        public string SerializeToJson() => JsonSerialization.Serialize(this, new ComponentContainerJsonVisitor());

        /// <summary>
        /// Deserialize a JSON <see cref="string"/> into the <paramref name="container"/>.
        /// </summary>
        /// <param name="container">The container to deserialize into.</param>
        /// <param name="json">The JSON string to deserialize from.</param>
        public static void DeserializeFromJson(ComponentContainer<TComponent> container, string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                DeserializeFromStream(container, stream);
            }
        }

        /// <summary>
        /// Serialize this <see cref="ComponentContainer{TObject}"/> to a file.
        /// </summary>
        /// <param name="path">The file path to write into.</param>
        public void SerializeToFile(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, SerializeToJson());
        }

        /// <summary>
        /// Deserialize a file into the <paramref name="container"/>.
        /// </summary>
        /// <param name="container">The container to deserialize into.</param>
        /// <param name="path">The file path to deserialize from.</param>
        public static void DeserializeFromPath(ComponentContainer<TComponent> container, string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                DeserializeFromStream(container, stream);
            }
        }

        /// <summary>
        /// Serialize this <see cref="ComponentContainer{TObject}"/> to a stream.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        public void SerializeToStream(Stream stream)
        {
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(SerializeToJson());
            }
        }

        /// <summary>
        /// Deserialize a stream into the <paramref name="container"/>.
        /// </summary>
        /// <param name="container">The container to deserialize into.</param>
        /// <param name="stream">The stream to deserialize from.</param>
        public static void DeserializeFromStream(ComponentContainer<TComponent> container, Stream stream)
        {
            container.Reset();

            try
            {
                using (var reader = new SerializedObjectReader(stream))
                {
                    var serializedObject = reader.ReadObject();
                    var changeTracker = new ChangeTracker(null);
                    PropertyContainer.Visit(ref serializedObject, new ComponentContainerAllocatorVisitor(container), ref changeTracker);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

            // Validate container
            container.Dependencies.RemoveAll(string.IsNullOrEmpty);
        }

        void Reset()
        {
            m_AssetAsJson = string.Empty;
            m_NonAssetDependencies.Clear();
            Dependencies.Clear();
            Components.Clear();
        }

        T CopyComponent<T>(T value) where T : TComponent
        {
            var type = value.GetType();
            var instance = (T)Activator.CreateInstance(type);
            PropertyContainer.Transfer(ref instance, ref value);
            return instance;
        }

        ComponentContainer<TComponent> LoadDependency(string assetGuid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            var asset = AssetDatabase.LoadAssetAtPath(assetPath, GetType());
            return asset is ComponentContainer<TComponent> container ? container : null;
        }

        public void OnBeforeSerialize()
        {
            m_AssetAsJson = SerializeToJson();
        }

        public void OnAfterDeserialize()
        {
            // Can't deserialize here, throws: "CreateJobReflectionData is not allowed to be called during serialization, call it from OnEnable instead."
        }

        public void OnEnable()
        {
            if (!string.IsNullOrEmpty(m_AssetAsJson))
            {
                DeserializeFromJson(this, m_AssetAsJson);
            }
        }

        class ComponentContainerJsonVisitor : JsonVisitor
        {
            public ComponentContainerJsonVisitor()
            {
                JsonVisitorRegistration.Invoke(this);
            }

            protected override string GetTypeInfo<TProperty, TContainer, T>(TProperty property, ref TContainer container, ref T value)
            {
                if (container is ComponentContainer<TComponent>)
                {
                    var type = value.GetType();
                    return $"{type}, {type.Assembly.GetName().Name}";
                }
                return null;
            }
        }

        struct ComponentContainerAllocatorVisitor : IPropertyVisitor
        {
            struct TransferValueAction<TSrcProperty, TSrcContainer, TSrcValue> : IPropertyGetter<ComponentContainer<TComponent>>
                where TSrcProperty : IProperty<TSrcContainer, TSrcValue>
            {
                TSrcContainer m_SrcContainer;
                readonly TSrcProperty m_SrcProperty;

                public TransferValueAction(TSrcContainer srcContainer, TSrcProperty srcProperty)
                {
                    m_SrcContainer = srcContainer;
                    m_SrcProperty = srcProperty;
                }

                public void VisitProperty<TDstProperty, TDstValue>(TDstProperty dstProperty, ref ComponentContainer<TComponent> dstContainer, ref ChangeTracker changeTracker)
                    where TDstProperty : IProperty<ComponentContainer<TComponent>, TDstValue>
                {
                    var srcValue = m_SrcProperty.GetValue(ref m_SrcContainer);
                    if (m_SrcProperty.IsContainer)
                    {
                        var dstValue = dstProperty.GetValue(ref dstContainer);
                        PropertyContainer.Transfer(ref dstValue, ref srcValue, ref changeTracker);
                        dstProperty.SetValue(ref dstContainer, dstValue);
                    }
                    else
                    {
                        if (TypeConversion.TryConvert<TSrcValue, TDstValue>(srcValue, out var dstValue))
                        {
                            if (CustomEquality.Equals(dstValue, dstProperty.GetValue(ref dstContainer)))
                            {
                                return;
                            }
                            dstProperty.SetValue(ref dstContainer, dstValue);
                        }
                    }
                }

                public void VisitCollectionProperty<TDstProperty, TDstValue>(TDstProperty dstProperty, ref ComponentContainer<TComponent> dstContainer, ref ChangeTracker changeTracker)
                    where TDstProperty : ICollectionProperty<ComponentContainer<TComponent>, TDstValue>
                {
                    throw new NotSupportedException();
                }
            }

            struct TransferCollectionAction<TSrcProperty, TSrcContainer, TSrcValue> : IPropertyGetter<ComponentContainer<TComponent>>
                where TSrcProperty : ICollectionProperty<TSrcContainer, TSrcValue>
            {
                struct TransferCollectionElement : ICollectionElementPropertyGetter<ComponentContainer<TComponent>>
                {
                    TSrcContainer m_SrcContainer;
                    readonly TSrcProperty m_SrcProperty;
                    readonly int m_Index;

                    public TransferCollectionElement(TSrcContainer srcContainer, TSrcProperty srcProperty, int index)
                    {
                        m_SrcContainer = srcContainer;
                        m_SrcProperty = srcProperty;
                        m_Index = index;
                    }

                    public void VisitProperty<TDstElementProperty, TDstElement>(TDstElementProperty dstElementProperty, ref ComponentContainer<TComponent> dstContainer, ref ChangeTracker changeTracker)
                        where TDstElementProperty : ICollectionElementProperty<ComponentContainer<TComponent>, TDstElement>
                    {
                        var assignment = new AssignDestinationElement<TDstElementProperty, TDstElement>(dstContainer, dstElementProperty);
                        m_SrcProperty.GetPropertyAtIndex(ref m_SrcContainer, m_Index, ref changeTracker, ref assignment);
                    }

                    public void VisitCollectionProperty<TDstElementProperty, TDstElement>(TDstElementProperty dstProperty, ref ComponentContainer<TComponent> dstContainer, ref ChangeTracker changeTracker)
                        where TDstElementProperty : ICollectionProperty<ComponentContainer<TComponent>, TDstElement>, ICollectionElementProperty<ComponentContainer<TComponent>, TDstElement>
                    {
                        throw new NotSupportedException();
                    }
                }

                struct AssignDestinationElement<TDstElementProperty, TDstElement> : ICollectionElementPropertyGetter<TSrcContainer>
                    where TDstElementProperty : ICollectionElementProperty<ComponentContainer<TComponent>, TDstElement>
                {
                    ComponentContainer<TComponent> m_DstContainer;
                    readonly TDstElementProperty m_DstElementProperty;

                    public AssignDestinationElement(ComponentContainer<TComponent> dstContainer, TDstElementProperty dstElementProperty)
                    {
                        m_DstContainer = dstContainer;
                        m_DstElementProperty = dstElementProperty;
                    }

                    public void VisitProperty<TSrcElementProperty, TSrcElement>(TSrcElementProperty srcElementProperty, ref TSrcContainer srcContainer, ref ChangeTracker changeTracker)
                        where TSrcElementProperty : ICollectionElementProperty<TSrcContainer, TSrcElement>
                    {
                        var srcValue = srcElementProperty.GetValue(ref srcContainer);
                        if (srcElementProperty.IsContainer)
                        {
                            var dstValue = m_DstElementProperty.GetValue(ref m_DstContainer);
                            PropertyContainer.Transfer(ref dstValue, ref srcValue, ref changeTracker);
                            m_DstElementProperty.SetValue(ref m_DstContainer, dstValue);
                        }
                        else
                        {
                            if (TypeConversion.TryConvert<TSrcElement, TDstElement>(srcValue, out var dstValue))
                            {
                                if (CustomEquality.Equals(dstValue, m_DstElementProperty.GetValue(ref m_DstContainer)))
                                {
                                    return;
                                }
                                m_DstElementProperty.SetValue(ref m_DstContainer, dstValue);
                            }
                        }
                    }

                    public void VisitCollectionProperty<TSrcElementProperty, TSrcElement>(TSrcElementProperty srcProperty, ref TSrcContainer srcContainer, ref ChangeTracker changeTracker)
                        where TSrcElementProperty : ICollectionProperty<TSrcContainer, TSrcElement>, ICollectionElementProperty<TSrcContainer, TSrcElement>
                    {
                        throw new NotSupportedException();
                    }
                }

                TSrcContainer m_SrcContainer;
                readonly TSrcProperty m_SrcProperty;

                public TransferCollectionAction(TSrcContainer srcContainer, TSrcProperty srcProperty)
                {
                    m_SrcContainer = srcContainer;
                    m_SrcProperty = srcProperty;
                }

                public void VisitProperty<TDstProperty, TDstValue>(TDstProperty dstProperty, ref ComponentContainer<TComponent> dstContainer, ref ChangeTracker changeTracker)
                    where TDstProperty : IProperty<ComponentContainer<TComponent>, TDstValue>
                {
                    throw new NotSupportedException();
                }

                public void VisitCollectionProperty<TDstProperty, TDstValue>(TDstProperty dstProperty, ref ComponentContainer<TComponent> dstContainer, ref ChangeTracker changeTracker)
                    where TDstProperty : ICollectionProperty<ComponentContainer<TComponent>, TDstValue>
                {
                    var value = m_SrcProperty.GetValue(ref m_SrcContainer);
                    if (m_SrcProperty.GetName() == nameof(Components) && value is SerializedArrayView array)
                    {
                        foreach (var item in array)
                        {
                            var view = item.AsObjectView();
                            if (view.TryGetMember(JsonVisitor.Style.TypeInfoKey, out var member))
                            {
                                var typeName = member.Value().AsStringView().ToString();
                                if (string.IsNullOrEmpty(typeName))
                                {
                                    Debug.LogError($"Empty or invalid type information when reading {nameof(Components)}.");
                                    continue;
                                }

                                var type = Type.GetType(typeName);
                                if (type == null)
                                {
                                    Debug.LogError($"Could not resolve type from type name '{typeName}' when reading {nameof(Components)}.");
                                    continue;
                                }

                                var component = (TComponent)Activator.CreateInstance(type);
                                PropertyContainer.Transfer(ref component, ref view, ref changeTracker);
                                dstContainer.Components.Add(component);
                            }
                            else
                            {
                                Debug.LogError($"Missing type information field when reading {nameof(Components)}.");
                            }
                        }
                    }
                    else
                    {
                        var srcCount = m_SrcProperty.GetCount(ref m_SrcContainer);
                        var dstCount = dstProperty.GetCount(ref dstContainer);
                        if (dstCount != srcCount)
                        {
                            dstProperty.SetCount(ref dstContainer, srcCount);
                        }

                        for (var i = 0; i < dstProperty.GetCount(ref dstContainer); ++i)
                        {
                            var transfer = new TransferCollectionElement(m_SrcContainer, m_SrcProperty, i);
                            dstProperty.GetPropertyAtIndex(ref dstContainer, i, ref changeTracker, ref transfer);
                        }
                    }
                }
            }

            readonly IPropertyBag<ComponentContainer<TComponent>> m_DstPropertyBag;
            ComponentContainer<TComponent> m_DstContainer;

            public ComponentContainerAllocatorVisitor(ComponentContainer<TComponent> dstContainer)
            {
                m_DstPropertyBag = PropertyBagResolver.Resolve<ComponentContainer<TComponent>>();
                if (m_DstPropertyBag == null)
                {
                    throw new NullReferenceException($"No {nameof(IPropertyBag<ComponentContainer<TComponent>>)} found for {nameof(ComponentContainer<TComponent>)}");
                }

                m_DstContainer = dstContainer ?? throw new ArgumentNullException(nameof(dstContainer));
            }

            public VisitStatus VisitProperty<TSrcProperty, TSrcContainer, TSrcValue>(TSrcProperty srcProperty, ref TSrcContainer srcContainer, ref ChangeTracker changeTracker)
                where TSrcProperty : IProperty<TSrcContainer, TSrcValue>
            {
                var transfer = new TransferValueAction<TSrcProperty, TSrcContainer, TSrcValue>(srcContainer, srcProperty);
                m_DstPropertyBag.FindProperty(srcProperty.GetName(), ref m_DstContainer, ref changeTracker, ref transfer);
                return VisitStatus.Handled;
            }

            public VisitStatus VisitCollectionProperty<TSrcProperty, TSrcContainer, TSrcValue>(TSrcProperty srcProperty, ref TSrcContainer srcContainer, ref ChangeTracker changeTracker)
                where TSrcProperty : ICollectionProperty<TSrcContainer, TSrcValue>
            {
                var transfer = new TransferCollectionAction<TSrcProperty, TSrcContainer, TSrcValue>(srcContainer, srcProperty);
                m_DstPropertyBag.FindProperty(srcProperty.GetName(), ref m_DstContainer, ref changeTracker, ref transfer);
                return VisitStatus.Handled;
            }
        }
    }
}
