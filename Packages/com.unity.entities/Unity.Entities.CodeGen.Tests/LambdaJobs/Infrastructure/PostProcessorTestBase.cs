using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NUnit.Framework;
using Unity.Entities.CodeGen;

namespace Unity.Entities.CodeGen.Tests.LambdaJobs.Infrastructure
{
    public class PostProcessorTestBase
    {
        [SetUp]
        public void Setup()
        {
            // LambdaJobs Integration tests currently only run on Windows due to a dependency on a version of ILSpy command line tool.
#if !UNITY_2019_3_OR_NEWER || !UNITY_EDITOR_WIN
            Assert.Ignore("These tests test a 2019.3 only feature and currently only run on Windows.");
#endif
        }

        protected MethodDefinition MethodDefinitionForOnlyMethodOf(Type type)
        {
            var assemblyLocation = type.Assembly.Location;

            var resolver = new OnDemandResolver();
            
            var ad = AssemblyDefinition.ReadAssembly(assemblyLocation, 
                new ReaderParameters(ReadingMode.Immediate)
                {
                    ReadSymbols = true,
                    ThrowIfSymbolsAreNotMatching = true,
                    SymbolReaderProvider = new PortablePdbReaderProvider(),
                    AssemblyResolver = resolver
                }
            );

            if (!ad.MainModule.HasSymbols)
                throw new Exception("NoSymbols");

            var fullName = type.FullName.Replace("+", "/");
            var typeDefinition = ad.MainModule.GetType(fullName).Resolve();
            var a = typeDefinition.GetMethods().Where(m => !m.IsConstructor && !m.IsStatic).ToList();
            return a.Count == 1 ? a.Single() : a.Single(m=>m.Name == "Test");
        }
    
        class OnDemandResolver : IAssemblyResolver
        {
            public void Dispose()
            {
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                return Resolve(name, new ReaderParameters(ReadingMode.Deferred));
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name.Name);
                var fileName = assembly.Location;
                parameters.AssemblyResolver = this;
                return AssemblyDefinition.ReadAssembly(fileName, parameters);
            }
        }

        protected void AssertProducesError(Type systemType, params string[] shouldContains)
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(systemType);
            var userCodeException = Assert.Throws<FoundErrorInUserCodeException>(() =>
            {
                foreach (var forEachDescriptionConstruction in LambdaJobDescriptionConstruction.FindIn(methodToAnalyze))
                {
                    LambdaJobsPostProcessor.Rewrite(methodToAnalyze, forEachDescriptionConstruction);
                }
            });
            foreach(var s in shouldContains)
                StringAssert.Contains(s, userCodeException.ToString());
        }
    }
}