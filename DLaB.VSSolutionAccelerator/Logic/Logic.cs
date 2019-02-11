﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DLaB.Log;
using Source.DLaB.Common;

namespace DLaB.VSSolutionAccelerator.Logic
{
    public class Logic
    {
        public string OutputBaseDirectory { get; }
        public string SolutionPath { get; }
        public string TemplateDirectory { get; }
        public string StrongNamePath { get; }
        public string NuGetPath { get; }
        public Dictionary<string, ProjectInfo> Projects { get; set; }

        public Logic(string solutionPath, string templateDirectory, string strongNamePath = null, string nugetPath = null)
        {
            SolutionPath = solutionPath;
            OutputBaseDirectory = Path.GetDirectoryName(solutionPath);
            TemplateDirectory = templateDirectory;
            StrongNamePath = strongNamePath ?? Path.Combine(templateDirectory, "bin\\sn.exe");
            NuGetPath = nugetPath ?? Path.Combine(templateDirectory, "bin\\nuget.exe");
        }

        public Dictionary<string, ProjectInfo> GetProjectInfos(InitializeSolutionInfo info)
        {
            var projects = new Dictionary<string, ProjectInfo>();
            AddSharedCommonProject(projects, info);
            AddSharedWorkflowProject(projects, info);
            if (info.ConfigureXrmUnitTest)
            {
                AddSharedTestCoreProject(projects, info);
                AddBaseTestProject(projects, info);
            }
            if (info.CreatePlugin)
            {
                AddPlugin(projects, info);
                if (info.ConfigureXrmUnitTest)
                {
                    AddPluginTest(projects, info);
                }
            }
            if (info.CreateWorkflow)
            {
                AddWorkflow(projects, info);
                if (info.ConfigureXrmUnitTest)
                {
                    AddWorkflowTest(projects, info);
                }
            }

            var mapper = new NuGetMapper(NuGetPath, info.XrmPackage.Version);
            foreach (var project in projects.Values.Where(p => p.Type != ProjectInfo.ProjectType.SharedProj))
            {
                project.AddNugetPostUpdateCommands(mapper, Path.Combine(TemplateDirectory, project.Key, "packages.config"), Path.Combine(OutputBaseDirectory, project.Name, "packages.config"));
            }
            return projects;
        }

        private void AddSharedCommonProject(Dictionary<string, ProjectInfo> projects, InitializeSolutionInfo info)
        {
            var project = CreateDefaultSharedProjectInfo(
                ProjectInfo.Keys.Common,
                info.SharedCommonProject,
                "b22b3bc6-0ac6-4cdd-a118-16e318818ad7");

            project.Files.First(f => f.Name.EndsWith(".projitems")).Removals.Add("$(MSBuildThisFileDirectory)Entities");
            projects.Add(project.Key, project);
        }

        private void AddSharedWorkflowProject(Dictionary<string, ProjectInfo> projects, InitializeSolutionInfo info)
        {
            var project = CreateDefaultSharedProjectInfo(
                ProjectInfo.Keys.WorkflowCommon,
                info.SharedCommonWorkflowProject,
                "dd5aa002-c1ff-4c0e-b9a5-3d63c7809b07",
                "Xyz.Xrm.Workflow",
                info.RootNamespace + ".Workflow");
            projects.Add(project.Key, project);
        }

        private void AddSharedTestCoreProject(Dictionary<string, ProjectInfo> projects, InitializeSolutionInfo info)
        {
            var project = CreateDefaultSharedProjectInfo(
                ProjectInfo.Keys.TestCore,
                info.SharedTestCoreProject,
                "8f91efc7-351b-4802-99aa-6c6f16110505", 
                ProjectInfo.Keys.Test,
                info.TestBaseProject);
            projects.Add(project.Key, project);
        }

        private void AddBaseTestProject(Dictionary<string, ProjectInfo> projects, InitializeSolutionInfo info)
        {
            var project = CreateDefaultProjectInfo(
                ProjectInfo.Keys.Test,
                info.TestBaseProject,
                "F62103E9-D25D-4F99-AABE-ECF348424366",
                "v4.6.2",
                info.SharedCommonProject);
            projects.Add(project.Key, project);
        }

        private void AddPlugin(Dictionary<string, ProjectInfo> projects, InitializeSolutionInfo info)
        {
            var project = CreateDefaultProjectInfo(
                ProjectInfo.Keys.Plugin,
                info.PluginName,
                "2B294DBF-8730-436E-B401-8745FEA632FE",
                GetPluginAssemblyVersionForSdk(info),
                info.SharedCommonProject);
            project.AddRegenKeyPostUpdateCommand(StrongNamePath);
            project.SharedProjectsReferences.Add(projects[ProjectInfo.Keys.Common]);
            if (!info.IncludeExamplePlugins)
            {
                project.FilesToRemove.AddRange(
                    new []{@"PluginBaseExamples\EntityAccess.cs",
                    @"PluginBaseExamples\ContextExample.cs",
                    @"PluginBaseExamples\VoidPayment.cs",
                    @"Properties\AssemblyInfo.cs",
                    @"RemovePhoneNumberFormatting.cs",
                    @"RenameLogic.cs",
                    @"SyncContactToAccount.cs"
                    });
            }
            projects.Add(project.Key, project);
        }

        private void AddPluginTest(Dictionary<string, ProjectInfo> projects, InitializeSolutionInfo info)
        {
            var project = CreateDefaultProjectInfo(
                ProjectInfo.Keys.PluginTests,
                info.PluginTestName,
                "3016D729-1A3B-43C0-AC2F-D4EF6A305FA6",
                GetPluginAssemblyVersionForSdk(info),
                info.SharedTestCoreProject);

            if (!info.IncludeExamplePlugins)
            {
                project.FilesToRemove.AddRange(
                    new[]{
                        "AssumptionExampleTests.cs",
                        "TestMethodClassExampleTests.cs",
                        "EntityBuilderExampleTests.cs",
                        "RemovePhoneNumberFormattingTests.cs",
                        "MsFakesVsXrmUnitTestExampleTests.cs",
                        "LocalOrServerPluginTest.cs",
                    });
            }
            project.ProjectsReferences.Add(projects[ProjectInfo.Keys.Plugin]);
            project.ProjectsReferences.Add(projects[ProjectInfo.Keys.Test]);
            project.SharedProjectsReferences.Add(projects[ProjectInfo.Keys.TestCore]);
            projects.Add(project.Key, project);
        }

        private void AddWorkflow(Dictionary<string, ProjectInfo> projects, InitializeSolutionInfo info)
        {
            var project = CreateDefaultProjectInfo(
                ProjectInfo.Keys.Workflow,
                info.WorkflowName,
                "5BD39AC9-97F3-47C8-8E1F-6A58A24AFB9E",
                GetPluginAssemblyVersionForSdk(info),
                info.SharedCommonProject);
            project.AddRegenKeyPostUpdateCommand(StrongNamePath);
            project.SharedProjectsReferences.Add(projects[ProjectInfo.Keys.Common]);
            project.Files.First().Replacements.Add(
                @"<Import Project=""..\Xyz.Xrm.WorkflowCore\Xyz.Xrm.WorkflowCore.projitems"" Label=""Shared"" />", 
                $@"<Import Project=""..\{info.SharedCommonWorkflowProject}\{info.SharedCommonWorkflowProject}.projitems"" Label=""Shared"" />");
            if (!info.IncludeExampleWorkflow)
            {
                project.FilesToRemove.Add("CreateGuidActivity.cs");
            }
            projects.Add(project.Key, project);
        }

        private void AddWorkflowTest(Dictionary<string, ProjectInfo> projects, InitializeSolutionInfo info)
        {
            var project = CreateDefaultProjectInfo(
                ProjectInfo.Keys.WorkflowTests,
                info.WorkflowTestName,
                "7056423A-373E-463D-B552-D2F305F5C041",
                GetPluginAssemblyVersionForSdk(info),
                info.SharedTestCoreProject);

            if (!info.IncludeExampleWorkflow)
            {
                project.FilesToRemove.AddRange(
                    new[]{
                        "WorkflowActivityExampleTests.cs"
                    });
            }
            project.ProjectsReferences.Add(projects[ProjectInfo.Keys.Workflow]);
            project.ProjectsReferences.Add(projects[ProjectInfo.Keys.Test]);
            project.SharedProjectsReferences.Add(projects[ProjectInfo.Keys.TestCore]);
            projects.Add(project.Key, project);
        }

        private string GetPluginAssemblyVersionForSdk(InitializeSolutionInfo info)
        {
            return info.XrmPackage.Version.Major >= 9 ? "v4.6.2" : "v4.5.2";
        }

        private ProjectInfo CreateDefaultProjectInfo(string key, string name, string originalId, string dotNetFramework, string sharedCommonProject)
        {
            Logger.AddDetail($"Configuring Project {name} based on {key}.");
            var id = Guid.NewGuid();
            var project = new ProjectInfo
            {
                Key = key,
                Id = id,
                Type = ProjectInfo.ProjectType.CsProj,
                NewDirectory = Path.Combine(OutputBaseDirectory, name),
                Name = name,
                Files = new List<ProjectFile>
                {
                    new ProjectFile
                    {
                        Name = name + ".csproj",
                        Replacements = new Dictionary<string, string>
                        {
                            {originalId, id.ToString().ToUpper()},
                            {$"<RootNamespace>{key}</RootNamespace>", $"<RootNamespace>{name}</RootNamespace>"},
                            {$"<AssemblyName>{key}</AssemblyName>", $"<AssemblyName>{name}</AssemblyName>"},
                            {"<TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>", $"<TargetFrameworkVersion>{dotNetFramework}</TargetFrameworkVersion>"},
                            {"<TargetFrameworkVersion>v4.5.2</TargetFrameworkVersion>", $"<TargetFrameworkVersion>{dotNetFramework}</TargetFrameworkVersion>"},
                            {$"<AssemblyOriginatorKeyFile>{key}.Key.snk</AssemblyOriginatorKeyFile>", $"<AssemblyOriginatorKeyFile>{name}.Key.snk</AssemblyOriginatorKeyFile>"},
                            {$"<None Include=\"{key}.Key.snk\" />", $"<None Include=\"{name}.Key.snk\" />"},
                            {@"<Import Project=""..\Xyz.Xrm\Xyz.Xrm.projitems"" Label=""Shared"" />", $@"<Import Project=""..\{sharedCommonProject}\{sharedCommonProject}.projitems"" Label=""Shared"" />"},
                            {@"<Import Project=""..\Xyz.Xrm.TestCore\Xyz.Xrm.TestCore.projitems"" Label=""Shared"" />", $@"<Import Project=""..\{sharedCommonProject}\{sharedCommonProject}.projitems"" Label=""Shared"" />"},
                        },
                        Removals = new List<string>
                        {
                            "<CodeAnalysisRuleSet>"
                        }
                    }
                },
            };
            return project;
        }

        private ProjectInfo CreateDefaultSharedProjectInfo(string key, string name, string originalId, string originalNamespace = null, string newNamespace = null)
        {
            Logger.AddDetail($"Configuring Project {name} based on {key}.");
            originalNamespace = originalNamespace ?? key;
            newNamespace = newNamespace ?? name;
            var id = Guid.NewGuid();
            var project = new ProjectInfo
            {
                Key = key,
                Id = id,
                Type = ProjectInfo.ProjectType.SharedProj,
                NewDirectory = Path.Combine(OutputBaseDirectory, name),
                Name = name,
                Files = new List<ProjectFile>
                {
                    new ProjectFile
                    {
                        Name = name + ".projitems",
                        Replacements = new Dictionary<string, string>
                        {
                            {originalId, id.ToString()},
                            {$"<Import_RootNamespace>{originalNamespace}</Import_RootNamespace>", $"<Import_RootNamespace>{newNamespace}</Import_RootNamespace>"}
                        },
                    },
                    new ProjectFile
                    {
                        Name = name + ".shproj",
                        Replacements = new Dictionary<string, string>
                        {
                            {originalId, id.ToString()},
                            { key +".projitems", name + ".projitems"}
                        }
                    },
                }
            };
            return project;
        }

        public static void Execute(InitializeSolutionInfo info, string templateDirectory, string strongNamePath = null)
        {
            Logger.AddDetail($"Starting to process solution '{info.SolutionPath}' using templates from '{templateDirectory}'");
            var logic = new Logic(info.SolutionPath, templateDirectory, strongNamePath);
            logic.Projects = logic.GetProjectInfos(info);
            foreach (var project in logic.Projects)
            {
                logic.CreateProject(project.Key, info);
            }
            IEnumerable<string> solution = File.ReadAllLines(logic.SolutionPath);
            solution = SolutionFileEditor.AddMissingProjects(solution, logic.Projects.Values);
            File.WriteAllLines(logic.SolutionPath, solution);
            logic.ExecuteNuGetRestoreForSolution();
            UpdateEarlyBoundConfigOutputPaths(info);
        }

        private static void UpdateEarlyBoundConfigOutputPaths(InitializeSolutionInfo info)
        {
            if (!info.ConfigureEarlyBound)
            {
                return;
            }
            var settings = EarlyBoundGenerator.Settings.EarlyBoundGeneratorConfig.Load(info.EarlyBoundSettingsPath);
            var settingsDirectory = Path.Combine(info.SolutionPath, info.SharedCommonProject);
            settings.ActionOutPath = Path.Combine(settingsDirectory, settings.ExtensionConfig.CreateOneFilePerAction ? @"Actions" : @"Actions.cs");
            settings.EntityOutPath = Path.Combine(settingsDirectory, settings.ExtensionConfig.CreateOneFilePerEntity ? @"Entities" : @"Entities.cs");
            settings.OptionSetOutPath = Path.Combine(settingsDirectory, settings.ExtensionConfig.CreateOneFilePerOptionSet ? @"OptionSets" : @"OptionSets.cs");
            var settingsPath = Path.Combine(settingsDirectory, "DLaB.EBG." + info.RootNamespace + ".Settings");
            settings.Save(Path.Combine(settingsDirectory, settingsPath));
            Clipboard.SetText(settingsPath);
            Logger.AddDetail(@"Now you should generate your Early Bound Entities for your Org!");
            Logger.AddDetail($@"Open the Early Bound Generator XrmToolBox plugin, connect to your org, and then set the Settings Path to ""{settingsPath}"" (which has been already been copied to your clipboard for your convenience) and generate your entities." + Environment.NewLine);
            Logger.AddDetail($@"These settings should be checked into TFS and should be the settings used by all individuals on your project plugin for generating entities!");
            MessageBox.Show(@"Please refer to the instructions in the text box for generating your early bound entities.", @"Generate Early Bound Entities!", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        }

        private void ExecuteNuGetRestoreForSolution()
        {
            var cmd = new ProcessExecutorInfo(NuGetPath, $"restore \"{SolutionPath}\" -NonInteractive");
            Logger.Show("Restoring Nuget for the solution.");
            Logger.AddDetail(cmd.FileName + " " + cmd.Arguments);
            var results = ProcessExecutor.ExecuteCmd(cmd);
            Logger.Show(results);
        }

        public void CreateProject(string projectKey, InitializeSolutionInfo info)
        {
            Projects[projectKey].CopyFromAndUpdate(TemplateDirectory, info.RootNamespace);
        }
    }
}