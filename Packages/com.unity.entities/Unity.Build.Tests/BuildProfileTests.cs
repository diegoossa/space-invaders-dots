using NUnit.Framework;
using Unity.Properties;

namespace Unity.Build.Tests
{
    [TestFixture]
    class BuildProfileTests
    {
        [Test]
        public void HasType()
        {
            var buildProfile = new DotsRuntimeBuildProfile();
            if (buildProfile.Target == null)
            {
                return; // cannot test if no platforms
            }
            Assert.That(buildProfile.TypeCache.HasType<PropertyVisitor>(), Is.True);

            buildProfile.ExcludedAssemblies.Add(typeof(PropertyVisitor).Assembly.GetName().Name);
            buildProfile.TypeCache.SetDirty();
            Assert.That(buildProfile.TypeCache.HasType<PropertyVisitor>(), Is.False);
        }

        [Test]
        public void HasAssembly()
        {
            var buildProfile = new DotsRuntimeBuildProfile();
            if (buildProfile.Target == null)
            {
                return; // cannot test if no platforms
            }
            Assert.That(buildProfile.TypeCache.HasAssembly(typeof(PropertyVisitor).Assembly), Is.True);

            buildProfile.ExcludedAssemblies.Add(typeof(PropertyVisitor).Assembly.GetName().Name);
            buildProfile.TypeCache.SetDirty();
            Assert.That(buildProfile.TypeCache.HasAssembly(typeof(PropertyVisitor).Assembly), Is.False);
        }

        [Test]
        public void HasAssemblyByName()
        {
            var buildProfile = new DotsRuntimeBuildProfile();
            if (buildProfile.Target == null)
            {
                return; // cannot test if no platforms
            }
            Assert.That(buildProfile.TypeCache.HasAssembly(typeof(PropertyVisitor).Assembly.GetName().Name), Is.True);

            buildProfile.ExcludedAssemblies.Add(typeof(PropertyVisitor).Assembly.GetName().Name);
            buildProfile.TypeCache.SetDirty();
            Assert.That(buildProfile.TypeCache.HasAssembly(typeof(PropertyVisitor).Assembly.GetName().Name), Is.False);
        }
    }
}