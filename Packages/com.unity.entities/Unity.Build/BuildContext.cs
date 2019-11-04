using System;
using System.Collections.Generic;

namespace Unity.Build
{
    /// <summary>
    /// Holds contextual information while a <see cref="BuildPipeline"/> is executing.
    /// </summary>
    public sealed class BuildContext
    {
        readonly List<object> m_Variables = new List<object>();

        /// <summary>
        /// Quick access to <see cref="BuildSettings"/> object.
        /// </summary>
        public BuildSettings BuildSettings => Get<BuildSettings>();

        /// <summary>
        /// Quick access to <see cref="BuildPipeline"/> object.
        /// </summary>
        public BuildPipeline BuildPipeline => Get<BuildPipeline>();

        /// <summary>
        /// Quick access to <see cref="BuildProgress"/> object.
        /// </summary>
        public BuildProgress BuildProgress => Get<BuildProgress>();

        /// <summary>
        /// Quick access to <see cref="BuildManifest"/> object.
        /// </summary>
        public BuildManifest BuildManifest => GetOrCreate<BuildManifest>();

        /// <summary>
        /// Constructor for a new context object.
        /// </summary>
        /// <param name="values">Optional objects to store</param>
        public BuildContext(params object[] values)
        {
            SetAll(values);
        }

        /// <summary>
        /// Get context object of a specific type.
        /// </summary>
        /// <typeparam name="T">The type of context object.</typeparam>
        /// <returns>The object if found, otherwise default(T)</returns>
        public T Get<T>() where T : class
        {
            return (T)Get(typeof(T));
        }

        /// <summary>
        /// Get context object of a specific type.
        /// </summary>
        /// <param name="type">The object type.</param>
        /// <returns>The object if found, otherwise null.</returns>
        public object Get(Type type)
        {
            foreach (var variable in m_Variables)
            {
                if (type.IsAssignableFrom(variable.GetType()))
                {
                    return variable;
                }
            }
            return null;
        }

        /// <summary>
        /// Remove a context object by type.
        /// </summary>
        /// <typeparam name="T">The context object type.</typeparam>
        /// <returns>True if the object was found and removed, false if it was not found.</returns>
        public bool Remove<T>()
        {
            var i = IndexOf(typeof(T));
            if (i < 0)
                return false;
            m_Variables.RemoveAt(i);
            return true;
        }

        /// <summary>
        /// Get context object of a specific type, or create default one if its missing.
        /// </summary>
        /// <typeparam name="T">The type of context object.</typeparam>
        /// <returns>The object if found, otherwise default(T)</returns>
        public T GetOrCreate<T>() where T : class
        {
            var data = Get<T>();
            if (data == null)
            {
                data = Activator.CreateInstance<T>();
                Set(data);
            }
            return data;
        }

        /// <summary>
        /// Sets a context object.
        /// </summary>
        /// <param name="val">The object to set.</param>
        /// <returns>True if the object was set. False if there was another object of the same type.</returns>
        public bool Set<T>(T val) where T : class
        {
            if (Get<T>() != null)
            {
                UnityEngine.Debug.LogWarningFormat("Failed to add context object {0}, there is already an item of type {1}", val, typeof(T).Name);
                return false;
            }
            return AddInternal(val);
        }

        /// <summary>
        /// Set objects
        /// </summary>
        /// <param name="values"></param>
        /// <returns>The number of items actually added.</returns>
        public int SetAll(params object[] values)
        {
            int count = 0;
            foreach (var value in values)
                if (AddInternal(value))
                    count++;
            return count;
        }

        int IndexOf(Type t)
        {
            for (int i = 0; i < m_Variables.Count; i++)
                if (t.IsAssignableFrom(m_Variables[i].GetType()))
                    return i;
            return -1;
        }

        bool AddInternal(object val)
        {
            if (val == null)
                return false;
            if (IndexOf(val.GetType()) >= 0)
                return false;
            m_Variables.Add(val);
            return true;
        }
    }
}
