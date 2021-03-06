// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Razor.Chunks;
using Microsoft.AspNet.Razor.CodeGenerators.Visitors;
using Microsoft.Framework.Internal;

namespace Microsoft.AspNet.Razor.CodeGenerators
{
    public class CSharpCodeGenerator : CodeGenerator
    {
        // See http://msdn.microsoft.com/en-us/library/system.codedom.codechecksumpragma.checksumalgorithmid.aspx
        private const string Sha1AlgorithmId = "{ff1816ec-aa5e-4d10-87f7-6f4963833460}";
        private const int DisableAsyncWarning = 1998;

        public CSharpCodeGenerator(CodeGeneratorContext context)
            : base(context)
        {
        }

        private ChunkTree Tree { get { return Context.ChunkTreeBuilder.ChunkTree; } }
        public RazorEngineHost Host { get { return Context.Host; } }

        /// <summary>
        /// Protected for testing.
        /// </summary>
        /// <returns>A new instance of <see cref="CSharpCodeWriter"/>.</returns>
        protected virtual CSharpCodeWriter CreateCodeWriter()
        {
            return new CSharpCodeWriter();
        }

        public override CodeGeneratorResult Generate()
        {
            var writer = CreateCodeWriter();

            if (!Host.DesignTimeMode && !string.IsNullOrEmpty(Context.Checksum))
            {
                writer.Write("#pragma checksum \"")
                      .Write(Context.SourceFile)
                      .Write("\" \"")
                      .Write(Sha1AlgorithmId)
                      .Write("\" \"")
                      .Write(Context.Checksum)
                      .WriteLine("\"");
            }

            using (writer.BuildNamespace(Context.RootNamespace))
            {
                // Write out using directives
                AddImports(Tree, writer, Host.NamespaceImports);
                // Separate the usings and the class
                writer.WriteLine();

                using (BuildClassDeclaration(writer))
                {
                    if (Host.DesignTimeMode)
                    {
                        writer.WriteLine("private static object @__o;");
                    }

                    var csharpCodeVisitor = CreateCSharpCodeVisitor(writer, Context);

                    new CSharpTypeMemberVisitor(csharpCodeVisitor, writer, Context).Accept(Tree.Chunks);
                    new CSharpDesignTimeHelpersVisitor(csharpCodeVisitor, writer, Context).AcceptTree(Tree);
                    new CSharpTagHelperFieldDeclarationVisitor(writer, Context).Accept(Tree.Chunks);

                    BuildConstructor(writer);

                    // Add space in-between constructor and method body
                    writer.WriteLine();

                    using (writer.BuildDisableWarningScope(DisableAsyncWarning))
                    {
                        using (writer.BuildMethodDeclaration("public override async", "Task", Host.GeneratedClassContext.ExecuteMethodName))
                        {
                            new CSharpTagHelperRunnerInitializationVisitor(writer, Context).Accept(Tree.Chunks);
                            csharpCodeVisitor.Accept(Tree.Chunks);
                        }
                    }
                }
            }

            return new CodeGeneratorResult(writer.GenerateCode(), writer.LineMappingManager.Mappings);
        }

        protected virtual CSharpCodeVisitor CreateCSharpCodeVisitor([NotNull] CSharpCodeWriter writer,
                                                                    [NotNull] CodeGeneratorContext context)
        {
            return new CSharpCodeVisitor(writer, context);
        }

        protected virtual CSharpCodeWritingScope BuildClassDeclaration(CSharpCodeWriter writer)
        {
            var baseTypeVisitor = new CSharpBaseTypeVisitor(writer, Context);
            baseTypeVisitor.Accept(Tree.Chunks);

            var baseType = baseTypeVisitor.CurrentBaseType ?? Host.DefaultBaseClass;

            var baseTypes = string.IsNullOrEmpty(baseType) ? Enumerable.Empty<string>() : new string[] { baseType };

            return writer.BuildClassDeclaration("public", Context.ClassName, baseTypes);
        }

        protected virtual void BuildConstructor(CSharpCodeWriter writer)
        {
            writer.WriteLineHiddenDirective();
            using (writer.BuildConstructor(Context.ClassName))
            {
                // Any constructor based logic that we need to add?
            };
        }

        private void AddImports(ChunkTree chunkTree, CSharpCodeWriter writer, IEnumerable<string> defaultImports)
        {
            // Write out using directives
            var usingVisitor = new CSharpUsingVisitor(writer, Context);
            foreach (Chunk chunk in Tree.Chunks)
            {
                usingVisitor.Accept(chunk);
            }

            defaultImports = defaultImports.Except(usingVisitor.ImportedUsings);

            foreach (string import in defaultImports)
            {
                writer.WriteUsing(import);
            }

            var taskNamespace = typeof(Task).Namespace;

            // We need to add the task namespace but ONLY if it hasn't been added by the default imports or using imports yet.
            if (!defaultImports.Contains(taskNamespace) && !usingVisitor.ImportedUsings.Contains(taskNamespace))
            {
                writer.WriteUsing(taskNamespace);
            }
        }
    }
}
