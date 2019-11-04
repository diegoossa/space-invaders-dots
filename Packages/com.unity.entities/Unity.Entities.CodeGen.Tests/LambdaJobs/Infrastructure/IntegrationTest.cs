using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Unity.Entities.CodeGen.Tests.LambdaJobs.Infrastructure
{
    [TestFixture]
    public abstract class IntegrationTest : PostProcessorTestBase
    {
        public static bool overwriteExpectationWithReality = false;
        
        protected void RunTest<T>()
        {
            var expectationFile = Path.GetFullPath($"Packages/com.unity.entities/Unity.Entities.CodeGen.Tests/LambdaJobs/IntegrationTests/{GetType().Name}.expectation.txt");
        
            var (jobCSharp, _) = RewriteAndDecompile(typeof(T));

            var shouldOverWrite = overwriteExpectationWithReality || !File.Exists(expectationFile);
            
            if (shouldOverWrite)
            {
                File.WriteAllText(expectationFile, jobCSharp);
                return;
            }
            string expected = File.ReadAllText(expectationFile);

            if (expected != jobCSharp)
            {
                var tempFolder = Path.GetTempPath();
                var path = $@"{tempFolder}decompiled.cs";
                File.WriteAllText(path, jobCSharp);
                Console.WriteLine("Actual Decompiled C#: ");
                Console.WriteLine((string) jobCSharp);
                UnityEngine.Debug.Log($"Wrote expected csharp to editor log and to {path}");
            }

            Assert.AreEqual(expected, jobCSharp);
        }

        private (string jobCharp, string methodIL) RewriteAndDecompile(Type type)
        {
            var methodToAnalyze = MethodDefinitionForOnlyMethodOf(type);
            var forEachDescriptionConstruction = LambdaJobDescriptionConstruction.FindIn(methodToAnalyze).Single();
            var closureType = LambdaJobsPostProcessor.Rewrite(methodToAnalyze, forEachDescriptionConstruction);

            var methodIL = new StringBuilder();
            foreach (var instruction in methodToAnalyze.Body.Instructions)
                methodIL.AppendLine(instruction.ToString());
			
            var jobCharp = Decompiler.DecompileIntoString(closureType.DeclaringType);
            Console.WriteLine(jobCharp + methodIL);
            return (jobCharp, methodIL.ToString());
        }
    }
}