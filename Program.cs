using System.CommandLine;
using System.Diagnostics;
using Essential.Diagnostics;
using LSP2GXL.LSP;
using LSP2GXL.Model;
using LSP2GXL.Utils;
using ShellProgressBar;

namespace LSP2GXL;

public class Program
{
    private static string? GetExecutablePath(string executableName)
    {
        if (File.Exists(executableName))
        {
            return Path.GetFullPath(executableName);
        }

        string[] paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        return paths.Select(path => Path.Combine(path, executableName)).FirstOrDefault(File.Exists);
    }

    private static async Task<int> Main(string[] args)
    {
        ColoredConsoleTraceListener listener = new(false);
        listener.Template = "{EventType}: {Message}";
        Trace.Listeners.Add(listener);
        Trace.AutoFlush = true;

        Argument<DirectoryInfo> projectRootArgument = new(name: "project",
                                                          description: "Path to the root of the project that shall be analyzed.");

        Option<FileInfo?> outputFileOption = new(aliases: ["-o", "--output"],
                                                 description: "The output GXL file to write to. "
                                                 + "If not given, the output graph will be discarded.");

        Option<bool> overwriteOption = new(name: "--overwrite",
                                           description: "Whether to overwrite the output file if it already exists.");

        Option<LSPServer> lspServerOption = new(aliases: ["-l", "--lsp-server"],
                                                description: "The LSP server to use.",
                                                parseArgument: result =>
                                                {
                                                    string name = result.Tokens.Single().Value;
                                                    LSPServer? server = LSPServer.GetByName(name);
                                                    if (server == null)
                                                    {
                                                        string availableServers = string.Join(", ", LSPServer.All.Select(x => x.Name));
                                                        result.ErrorMessage = "The given LSP server does not exist. "
                                                            + "Available servers are: " + availableServers;
                                                        return LSPServer.All.First(); // Ignored
                                                    }
                                                    else
                                                    {
                                                        return server;
                                                    }
                                                })
            { IsRequired = true };

        Option<FileInfo?> lspServerPathOption = new(aliases: ["-x", "--lsp-server-executable"],
                                                    description: "The path to the executable of the LSP server. "
                                                    + "Note that the given server must match the configured language server.");

        Option<DirectoryInfo[]> sourcePathsOption = new(aliases: ["-s", "--source-path"],
                                                        description: "The path to a directory (under the project root) whose contents shall be analyzed. "
                                                        + "If this is not given, we will query the whole project root.");

        Option<DirectoryInfo[]> excludedSourcePathsOption = new(aliases: ["-e", "--exclude-source-path"],
                                                                description: "The path to a directory (under the project root) whose contents shall NOT be analyzed. ");

        Option<bool> logLspOption = new(name: "--log-lsp",
                                        description: $"Whether to log the raw LSP input and output to a temporary file at {Path.GetTempPath()}.");

        Option<float> timeoutOption = new(name: "--timeout",
                                          description: "The maximum time in seconds to wait for the LSP server to respond to any given query.",
                                          getDefaultValue: () => 10);

        Option<EdgeKind[]> edgeTypesOption = new(name: "--edge-type",
                                                 description: "LSP edge type to include in the import.",
                                                 getDefaultValue: () => [EdgeKind.All & ~(EdgeKind.Definition | EdgeKind.Declaration)]);

        Option<bool> selfReferencesOption = new(name: "--self-references",
                                                description: "Whether to allow self-references in the generated graph.");

        Option<bool> parentReferencesOption = new(name: "--parent-references",
                                                  description: "Whether to allow references from an element to its direct parent in the generated graph.");

        Option<NodeKind[]> nodeTypesOption = new(name: "--node-type",
                                                 description: "LSP node type to include in the import.",
                                                 getDefaultValue: () => [NodeKind.All]);

        Option<DiagnosticKind[]> diagnosticLevelsOption = new(name: "--diagnostic-level",
                                                              description: "LSP diagnostic level to include in the import.",
                                                              getDefaultValue: () => [DiagnosticKind.All]);

        Option<uint?> parallelismOption = new(aliases: ["-j", "--parallel-tasks"],
                                              description: "The number of parallel tasks to use for the edge phase of the import.");
        Option<bool> unoptimizedEdgeConnectionOption = new(name: "--unoptimized-edges",
                                                           description: "Whether to disable the optimized (kd-tree based) "
                                                           + "edge connection algorithm.");
        Option<FileInfo> performanceOutputFileOption = new(aliases: ["-p", "--performance-output"],
                                                           description: "The output file to write CSV performance information to. Must not exist yet.",
                                                           getDefaultValue: () => new FileInfo("performance.csv"));

        projectRootArgument.AddValidator(result =>
        {
            if (!result.GetArgumentValueOrDefault(projectRootArgument)?.Exists ?? false)
            {
                result.ErrorMessage = "The given project root does not exist.";
            }
        });
        outputFileOption.AddValidator(result =>
        {
            FileInfo? outputFile = result.GetValueForOption(outputFileOption);
            if ((outputFile?.Exists ?? false) && !result.GetValueForOption(overwriteOption))
            {
                result.ErrorMessage = "The given output file already exists. Use --overwrite to overwrite it.";
            }
        });
        sourcePathsOption.AddValidator(result =>
        {
            DirectoryInfo[] sourcePaths = result.GetOptionValueOrDefault(sourcePathsOption) ?? [];
            foreach (DirectoryInfo sourcePath in sourcePaths)
            {
                if (!sourcePath.Exists)
                {
                    result.ErrorMessage = $"The given source path '{sourcePath}' does not exist.";
                }
            }
        });
        excludedSourcePathsOption.AddValidator(result =>
        {
            DirectoryInfo[] excludedSourcePaths = result.GetOptionValueOrDefault(excludedSourcePathsOption) ?? [];
            foreach (DirectoryInfo excludedSourcePath in excludedSourcePaths)
            {
                if (!excludedSourcePath.Exists)
                {
                    result.ErrorMessage = $"The given excluded source path '{excludedSourcePath}' does not exist.";
                }
            }
        });
        timeoutOption.AddValidator(result =>
        {
            float timeout = result.GetValueForOption(timeoutOption);
            if (timeout < 0)
            {
                result.ErrorMessage = "The timeout must be a non-negative number.";
            }
        });
        performanceOutputFileOption.AddValidator(result =>
        {
            FileInfo? performanceOutput = result.GetOptionValueOrDefault(performanceOutputFileOption);
            if (performanceOutput?.Exists ?? false)
            {
                result.ErrorMessage = "The performance output file already exists. Please choose a different name.";
            }
        });

        RootCommand rootCommand = new("A tool that transforms LSP project information into a GXL file.")
        {
            projectRootArgument,
            outputFileOption,
            overwriteOption,
            lspServerOption,
            lspServerPathOption,
            logLspOption,
            sourcePathsOption,
            excludedSourcePathsOption,
            timeoutOption,
            edgeTypesOption,
            selfReferencesOption,
            parentReferencesOption,
            nodeTypesOption,
            diagnosticLevelsOption,
            parallelismOption,
            unoptimizedEdgeConnectionOption,
            performanceOutputFileOption,
        };

        int returnCode = 1;

        rootCommand.SetHandler(async context =>
        {
            DirectoryInfo projectRoot = context.ParseResult.GetValueForArgument(projectRootArgument);
            FileInfo? outputFile = context.ParseResult.GetValueForOption(outputFileOption);
            LSPServer lspServer = context.ParseResult.GetValueForOption(lspServerOption)!;
            FileInfo? lspServerPath = context.ParseResult.GetValueForOption(lspServerPathOption);
            bool logLsp = context.ParseResult.GetValueForOption(logLspOption);
            DirectoryInfo[] sourcePaths = context.ParseResult.GetValueForOption(sourcePathsOption) ?? [];
            DirectoryInfo[] excludedSourcePaths = context.ParseResult.GetValueForOption(excludedSourcePathsOption) ?? [];
            float timeout = context.ParseResult.GetValueForOption(timeoutOption);
            EdgeKind edgeTypes = context.ParseResult.GetValueForOption(edgeTypesOption)!.Aggregate((a, b) => a | b);
            bool selfReferences = context.ParseResult.GetValueForOption(selfReferencesOption);
            bool parentReferences = context.ParseResult.GetValueForOption(parentReferencesOption);
            NodeKind nodeTypes = context.ParseResult.GetValueForOption(nodeTypesOption)!.Aggregate((a, b) => a | b);
            DiagnosticKind diagnosticLevels = context.ParseResult.GetValueForOption(diagnosticLevelsOption)!.Aggregate((a, b) => a | b);
            uint? parallelism = context.ParseResult.GetValueForOption(parallelismOption);
            bool unoptimizedEdgeConnection = context.ParseResult.GetValueForOption(unoptimizedEdgeConnectionOption);
            FileInfo performanceOutputFile = context.ParseResult.GetValueForOption(performanceOutputFileOption)!;

            returnCode = await ConvertAsync(projectRoot, outputFile, lspServer, lspServerPath, logLsp, sourcePaths,
                                            excludedSourcePaths, timeout, edgeTypes, selfReferences, parentReferences,
                                            nodeTypes, diagnosticLevels, unoptimizedEdgeConnection, parallelism,
                                            performanceOutputFile, context.GetCancellationToken());
        });

        await rootCommand.InvokeAsync(args);

        return returnCode;
    }

    private static async Task<int> ConvertAsync(DirectoryInfo project, FileInfo? outputFile,
                                                LSPServer lspServer, FileInfo? lspServerPath, bool logLsp,
                                                DirectoryInfo[] sourcePaths, DirectoryInfo[] excludedSourcePaths,
                                                float timeout, EdgeKind edgeTypes, bool selfReferences,
                                                bool parentReferences, NodeKind nodeTypes, DiagnosticKind diagnosticLevels,
                                                bool unoptimizedEdgeConnection, uint? parallelism,
                                                FileInfo performanceOutputFile, CancellationToken token)
    {
        string path = lspServerPath?.FullName ?? lspServer.ServerExecutable;
        string? fullPath = GetExecutablePath(path);
        if (fullPath == null)
        {
            Trace.TraceError($"The executable {path} for the LSP server {lspServer.Name} could not be found.");
            return 1;
        }

        Trace.TraceInformation($"Analyzing project: {project}, output: {outputFile}");

        token.ThrowIfCancellationRequested();

        using ProgressBar progressBar = new(10_000, "Starting LSP server...");
        Trace.Listeners.Clear();
        ProgressBarTraceListener listener = new(progressBar);
        Trace.Listeners.Add(listener);

        if (sourcePaths.Length == 0)
        {
            sourcePaths = [project];
        }

        LSPHandler handler = new(lspServer, project.FullName, logLsp, TimeSpan.FromSeconds(timeout));
        Graph? graph;
        try
        {
            Performance startupPerf = Performance.Begin("LSP Startup", performanceOutputFile.FullName);
            await handler.InitializeAsync(executablePath: fullPath, token: token);
            token.ThrowIfCancellationRequested();
            startupPerf.End();
            progressBar.Tick(1);

            LSPImporter importer = new(handler,
                                       sourcePaths.Select(x => x.FullName).ToList(),
                                       excludedSourcePaths.Select(x => x.FullName).ToList(),
                                       performanceOutputFile.FullName,
                                       OptimizedEdgeConnection: !unoptimizedEdgeConnection,
                                       IncludeNodeTypes: nodeTypes,
                                       IncludeDiagnostics: diagnosticLevels,
                                       IncludeEdgeTypes: edgeTypes,
                                       AvoidSelfReferences: !selfReferences,
                                       AvoidParentReferences: !parentReferences,
                                       ParallelTasks: parallelism);
            progressBar.Message = "Creating graph using LSP...";
            IProgress<float> progress = progressBar.AsProgress<float>();
            graph = await importer.ImportAsync(progress.Report, x => progressBar.Message = x, token);
        }
        catch (TimeoutException)
        {
            string message = "The language server did not respond in time (or you aborted the process).";
            if (logLsp)
            {
                message += $" Check the output log at {Path.GetTempPath()}outputLogLsp.txt";
            }
            else
            {
                message += " Enable logging in the graph provider to see what went wrong.";
            }
            Trace.TraceError(message);
            return 1;
        }
        finally
        {
            progressBar.Message = "Shutting down LSP server...";
            // Two of the language servers block in/out streams on shutdown, and the shutdown doesn't
            // seem to accomplish much anyway, so we just disable it for them.
            if (lspServer != LSPServer.Pyright && lspServer != LSPServer.TypescriptLanguageServer) {
              await handler.ShutdownAsync(token);
            }
            handler.ReleaseStreams();
        }
        if (graph == null)
        {
            Trace.TraceError("The graph could not be created (see log messages).");
            return 1;
        }

        if (outputFile != null)
        {
            Trace.TraceInformation($"Writing graph to {outputFile.FullName}.");
            GraphWriter.Save(outputFile.FullName, graph);
        }
        else
        {
            Trace.TraceInformation("No output file given, discarding graph.");
        }

        Trace.TraceInformation("Done.");
        return listener.AnyErrors ? 1 : 0;
    }
}
