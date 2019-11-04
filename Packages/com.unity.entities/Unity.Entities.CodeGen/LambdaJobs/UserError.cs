using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Unity.Entities.CodeGen
{
    static class InternalCompilerError
    {
        public static DiagnosticMessage DCICE001(MethodDefinition method)
        {
            return UserError.MakeError(nameof(DCICE001),"Entities.ForEach Lambda expression uses something from its outer class. This is not supported.", method, null);
        }
        
        public static DiagnosticMessage DCICE002(MethodDefinition method, Instruction instruction)
        {
            return UserError.MakeError(nameof(DCICE001),$"Unable to find LdFtn & NewObj pair preceding call instruction in {method.Name}", method, instruction);
        }
    }
    
    static class UserError
    {
        public static DiagnosticMessage DC0001(MethodDefinition method, Instruction instruction, FieldReference fr)
        {
            return MakeError(nameof(DC0001),$"Entities.ForEach Lambda expression uses field '{fr.Name}'. This is not supported, assign the field to a local outside of the lambda expression and use that instead.", method, instruction);
        }

        public static DiagnosticMessage DC0002(MethodDefinition method, Instruction instruction, MethodReference mr)
        {
            return MakeError(nameof(DC0002),$"Entities.ForEach Lambda expression uses instance method '{mr.Name}'. Invoking instance methods is not supported.", method, instruction);
        }
        
        public static DiagnosticMessage DC0003(string name, MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0003),$"The name {name} is already used in this system.", method, instruction);
        }
        
        public static DiagnosticMessage DC0004(MethodDefinition methodToAnalyze, Instruction illegalInvocation, FieldDefinition field)
        {
            return MakeError(nameof(DC0004),$"Entities.ForEach Lambda expression captures a non-value type '{field.Name}'", methodToAnalyze, illegalInvocation);
        }
        
        public static DiagnosticMessage DC0005(MethodDefinition method, Instruction instruction, ParameterDefinition parameter)
        {
            return MakeError(nameof(DC0005),$"Entities.ForEach Lambda expression parameter '{parameter.Name}' with type {parameter.ParameterType.FullName} is not supported", method, instruction);
        }

        public static DiagnosticMessage DC0006(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0006),$"Scheduling an Entities query requires a .{nameof(LambdaForEachDescriptionConstructionMethods.ForEach)} invocation", method, instruction);
        }

        public static DiagnosticMessage DC0017(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0006),$"Scheduling an Lambda job requires a .{nameof(LambdaSimpleJobDescriptionConstructionMethods.WithCode)} invocation", method, instruction);
        }
        
        public static DiagnosticMessage DC0018(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0006),$"Scheduling an Chunk job requires a .{nameof(LambdaJobChunkDescriptionConstructionMethods.ForEach)} invocation", method, instruction);
        }

        public static DiagnosticMessage DC0007(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0007),$"Unexpected code structure in Entities/Job query. Make sure to immediately end each ForEach query with a .Schedule() or .Run() call", method, instruction);
        }

        public static DiagnosticMessage DC0008(MethodDefinition method, Instruction instruction, MethodReference mr)
        {
            return MakeError(nameof(DC0008),$"The argument to {mr.Name} needs to be a literal value.", method, instruction);
        }

        public static DiagnosticMessage DC0009(MethodDefinition method, Instruction instruction, MethodReference mr)
        {
            return MakeError(nameof(DC0009),$"{mr.Name} is only allowed to be called once.", method, instruction);
        }

        public static DiagnosticMessage DC0010(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0010),$"The Entities.ForEach statement contains dynamic code that cannot be statically analyzed.", method, instruction);
        }

        public static DiagnosticMessage DC0011(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0011),$"Every Entities.ForEach statement needs to end with a .Schedule() or .Run() invocation", method, instruction);
        }
        
        public static DiagnosticMessage DC0012(MethodDefinition methodToAnalyze, LambdaJobDescriptionConstruction.InvokedConstructionMethod constructionMethod)
        {
            return MakeError(nameof(DC0012),$"{constructionMethod.MethodName} requires its argument to be a local variable that is captured by the lambda expression.", methodToAnalyze, constructionMethod.InstructionInvokingMethod);
        }
        
        public static DiagnosticMessage DC0013(FieldReference fieldReference, MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0013), $"Entities.ForEach Lambda expression writes to captured variable '{fieldReference.Name}'. This is not supported.", method, instruction);
        }

        public static DiagnosticMessage DC0014(MethodDefinition method, Instruction instruction, ParameterDefinition parameter, string[] supportedParameters)
        {
            return MakeError(nameof(DC0014),$"Entities.ForEach Lambda expression parameter '{parameter.Name}' is not a supported parameter. Supported parameter names are {supportedParameters.SeparateByComma()}", method, instruction);
        }
        
        public static DiagnosticMessage DC0015(string noneTypeName, MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0015),$"Entities.ForEach will never run because it both requires and excludes {noneTypeName}", method, instruction);
        }
        
        public static DiagnosticMessage DC0016(string noneTypeName, MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0016),$"Entities.ForEach lists both WithAny<{noneTypeName}() and WithNone<{noneTypeName}().", method, instruction);
        }

        public static DiagnosticMessage MakeError(string errorCode, string messageData, MethodDefinition method,Instruction instruction)
        {
            var result = new DiagnosticMessage {Column = 0, Line = 0, DiagnosticType = DiagnosticType.Error, File = ""};
            
            var seq = instruction != null ? CecilHelpers.FindBestSequencePointFor(method, instruction) : null;

            if (errorCode.Contains("ICE"))
            {
                messageData = messageData + " Seeing this error indicates a bug in the dots compiler. We'd appreciate a bug report (About->Report a Problem). Thnx! <3";
            }

            messageData = $"error {errorCode}: {messageData}";
            if (seq != null)
            {
                result.File = seq.Document.Url;
                result.Column = seq.StartColumn;
                result.Line = seq.StartLine;
                result.MessageData = $"{seq.Document.Url}:({seq.StartLine},{seq.StartColumn}) {messageData}";
            }
            else
            {
                result.MessageData = messageData;
            }
                
            return result;
        }

        public static void Throw(this DiagnosticMessage dm)
        {
            throw new FoundErrorInUserCodeException(new[] { dm});
        }
    }
}