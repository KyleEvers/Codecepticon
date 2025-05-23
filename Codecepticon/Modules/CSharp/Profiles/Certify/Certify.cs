﻿using Codecepticon.CommandLine;
using Codecepticon.Modules.CSharp.Profiles.Certify.Rewriters;
using Codecepticon.Modules.CSharp.Rewriters;
using Codecepticon.Utils;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
//using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using BuildEvaluation = Microsoft.Build.Evaluation;

namespace Codecepticon.Modules.CSharp.Profiles.Certify
{
    class Certify : BaseProfile
    {
        public override string Name { get; } = "Certify";
        public override async Task<Solution> Before(Solution solution, Project project)
        {
            if (CommandLineData.CSharp.Rename.CommandLine)
            {
                Logger.Debug("Certify: Rewriting command line");
                solution = await RewriteCommandLine(solution, project);
            }

            Logger.Debug("Certify: Removing help text");
            solution = await RemoveHelpText(solution, project);

            Logger.Debug("Certify: Rewriting switch expressions");
            solution = await RewriteSwitchExpressions(solution, project);

            return solution;
        }

        public override async Task<Solution> After(Solution solution, Project project)
        {
            BuildEvaluation.Project buildProject = VisualStudioManager.GetBuildProject(project);

            if (CommandLineData.CSharp.Rename.Namespaces)
            {
                Logger.Debug("Certify: Renaming RootNamespace");
                string rootNamespace = buildProject.GetPropertyValue("RootNamespace");
                string newNamespace = DataCollector.Mapping.Namespaces[rootNamespace];
                buildProject.SetProperty("RootNamespace", newNamespace);
            }

            Logger.Debug("Certify: Renaming Misc Properties");
            buildProject.SetProperty("AssemblyName", CommandLineData.Global.NameGenerator.Generate());
            buildProject.SetProperty("Company", CommandLineData.Global.NameGenerator.Generate());
            buildProject.SetProperty("Product", CommandLineData.Global.NameGenerator.Generate());
            buildProject.Save();
            return solution;
        }

        protected async Task<Solution> RewriteSwitchExpressions(Solution solution, Project project)
        {
            foreach (Document document in project.Documents)
            {
                SyntaxNode syntaxRoot = await document.GetSyntaxRootAsync();
                SwitchExpressions switchExpressions = new SwitchExpressions();
                switchExpressions.documentModel = await document.GetSemanticModelAsync();
                syntaxRoot = switchExpressions.Visit(syntaxRoot);

                solution = solution.WithDocumentSyntaxRoot(document.Id, syntaxRoot);
            }

            return solution;
        }

        protected async Task<Solution> RewriteCommandLine(Solution solution, Project project)
        {
            Rewriters.CommandLine rewriteCommandLine = new Rewriters.CommandLine();

            project = VisualStudioManager.GetProjectByName(solution, project.Name);
            foreach (Document document in project.Documents)
            {
                SyntaxNode syntaxRoot = await document.GetSyntaxRootAsync();

                FileInfo file = new FileInfo(document.FilePath);
                if (file.DirectoryName.Split('\\').Last() == "Commands")
                {
                    syntaxRoot = rewriteCommandLine.Visit(syntaxRoot);
                }

                solution = solution.WithDocumentSyntaxRoot(document.Id, syntaxRoot);
            }
            return solution;
        }

        protected async Task<Solution> RemoveHelpText(Solution solution, Project project)
        {
            project = VisualStudioManager.GetProjectByName(solution, project.Name);
            Document csInfo = VisualStudioManager.GetDocumentByName(project, "Info.cs");

            SyntaxNode syntaxRoot = await csInfo.GetSyntaxRootAsync();
            RemoveHelpText rewriter = new RemoveHelpText();
            SyntaxNode newSyntaxRoot = rewriter.Visit(syntaxRoot);

            solution = solution.WithDocumentSyntaxRoot(csInfo.Id, newSyntaxRoot);

            return solution;
        }
    }
}
