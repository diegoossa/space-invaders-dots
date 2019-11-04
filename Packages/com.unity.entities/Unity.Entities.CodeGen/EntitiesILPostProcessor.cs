using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;

namespace Unity.Entities.CodeGen
{
    class EntitiesILPostProcessors : ILPostProcessor
    {
        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
                return null;
  
            var assemblyDefinition = AssemblyDefinitionFor(compiledAssembly);

            var postProcessors = new EntitiesILPostProcessor[]
            {
                new LambdaJobsPostProcessor(),
                new AuthoringComponentPostProcessor()
            };
            
            var diagnostics = new List<DiagnosticMessage>();
            bool madeAnyChange = false;
            foreach (var postProcessor in postProcessors)
            {
                diagnostics.AddRange(postProcessor.PostProcess(assemblyDefinition, out var madeChange));
                madeAnyChange |= madeChange;
            };

            if (!madeAnyChange || diagnostics.Any(d=>d.DiagnosticType == DiagnosticType.Error))
                return new ILPostProcessResult(null, diagnostics);
            
            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
            };
            assemblyDefinition.Write(pe, writerParameters);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), diagnostics);
        }

        public override ILPostProcessor GetInstance()
        {
            return this;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name == "Unity.Entities")
                return true;
            return compiledAssembly.References.Any(f => f.EndsWith("Unity.Entities.dll")) && compiledAssembly.Name != "Unity.Entities.CodeGen.Tests";
        }

        class PostProcessorAssemblyResolver : IAssemblyResolver
        {
            private readonly string[] _references;
            Dictionary<string, AssemblyDefinition> _cache = new Dictionary<string, AssemblyDefinition>();
            
            public PostProcessorAssemblyResolver(string[] references)
            {
                _references = references;
            }
            
            public void Dispose()
            {
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                return Resolve(name, new ReaderParameters(ReadingMode.Deferred));
            }

            
            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                lock (_cache)
                {
                    var fileName = _references.FirstOrDefault(r => r.EndsWith(name.Name + ".dll"));
                    if (fileName == null)
                        return null;

                    var lastWriteTime = File.GetLastWriteTime(fileName);

                    var cacheKey = fileName + lastWriteTime.ToString();

                    if (_cache.TryGetValue(cacheKey, out var result))
                        return result;

                    parameters.AssemblyResolver = this;

                    var ms = new MemoryStream(File.ReadAllBytes(fileName));

                    var pdb = fileName + ".pdb";
                    if (File.Exists(pdb))
                        parameters.SymbolStream = new MemoryStream(File.ReadAllBytes(pdb));

                    var assemblyDefinition = AssemblyDefinition.ReadAssembly(ms, parameters);
                    _cache.Add(cacheKey, assemblyDefinition);
                    return assemblyDefinition;
                }
            }
        }

        private static AssemblyDefinition AssemblyDefinitionFor(ICompiledAssembly compiledAssembly)
        {
            var readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = new PostProcessorAssemblyResolver(compiledAssembly.References),
                ReadingMode = ReadingMode.Immediate
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            return AssemblyDefinition.ReadAssembly(peStream, readerParameters);
        }
    }
    
    abstract class EntitiesILPostProcessor
    {
        protected AssemblyDefinition AssemblyDefinition;

        private List<DiagnosticMessage> _diagnosticMessages = new List<DiagnosticMessage>();

        public IEnumerable<DiagnosticMessage> PostProcess(AssemblyDefinition assemblyDefinition, out bool madeAChange)
        {
            AssemblyDefinition = assemblyDefinition;
            try
            {
                madeAChange = PostProcessImpl();
            }
            catch (FoundErrorInUserCodeException e)
            {
                madeAChange = false;
                return e.DiagnosticMessages;
            }

            return _diagnosticMessages;
        }
        
        protected abstract bool PostProcessImpl();

        protected void AddDiagnostic(DiagnosticMessage diagnosticMessage)
        {
            _diagnosticMessages.Add(diagnosticMessage);
        }
    }
}