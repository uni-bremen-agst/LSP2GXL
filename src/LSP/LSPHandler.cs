using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reflection;
using LSP2GXL.Utils;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using Container = OmniSharp.Extensions.LanguageServer.Protocol.Models.Container;

namespace LSP2GXL.LSP;

/// <summary>
/// Handles the language server process.
///
/// This class is responsible for starting and stopping the language server, and is intended
/// to be the primary interface for other classes to communicate with the language server.
/// </summary>
public class LSPHandler(LSPServer server, string projectPath, bool logLSP = true, TimeSpan? timeoutSpan = null)
{
    /// <summary>
    /// The language server to be used.
    /// This is the backing field for the <see cref="Server"/> property.
    /// </summary>
    public LSPServer Server { get; } = server;

    /// <summary>
    /// The path to the project to be analyzed.
    /// </summary>
    public string ProjectPath { get; } = projectPath;

    /// <summary>
    /// Whether to log the communication between the language server and LSP2GXL to a temporary file.
    /// </summary>
    public bool LogLSP { get; } = logLSP;

    /// <summary>
    /// The maximum time to wait for a response from the language server.
    /// </summary>
    public TimeSpan TimeoutSpan = timeoutSpan ?? TimeSpan.FromSeconds(2);


    /// <summary>
    /// The language client that is used to communicate with the language server.
    /// </summary>
    private LanguageClient? Client { get; set; }

    /// <summary>
    /// The process that runs the language server.
    /// </summary>
    private Process? lspProcess;

    /// <summary>
    /// A semaphore to ensure that nothing interferes with the language server while it is starting or stopping.
    /// </summary>
    private readonly SemaphoreSlim semaphore = new(1, 1);

    /// <summary>
    /// Whether the language server is ready to process requests.
    /// </summary>
    private bool IsReady { get; set; }

    /// <summary>
    /// The capabilities of the language server.
    /// </summary>
    public IServerCapabilities? ServerCapabilities => Client?.ServerSettings.Capabilities;

    /// <summary>
    /// The server-published diagnostics that have not been handled yet.
    /// </summary>
    private readonly ConcurrentQueue<PublishDiagnosticsParams> unhandledDiagnostics = new();

    /// <summary>
    /// A dictionary mapping from file paths to the diagnostics that have been published for that file.
    /// </summary>
    private readonly ConcurrentDictionary<string, List<PublishDiagnosticsParams>> savedDiagnostics = new();

    private Stream outputLog = Stream.Null;
    private Stream inputLog = Stream.Null;

    /// <summary>
    /// The capabilities of the language client.
    /// </summary>
    private static readonly ClientCapabilities ClientCapabilities = new()
    {
        TextDocument = new TextDocumentClientCapabilities
        {
            Declaration = new DeclarationCapability
            {
                LinkSupport = false
            },
            Definition = new DefinitionCapability
            {
                LinkSupport = false
            },
            TypeDefinition = new TypeDefinitionCapability
            {
                LinkSupport = false
            },
            Implementation = new ImplementationCapability
            {
                LinkSupport = false
            },
            References = new ReferenceCapability(),
            CallHierarchy = new CallHierarchyCapability(),
            TypeHierarchy = new TypeHierarchyCapability(),
            Hover = new HoverCapability
            {
                ContentFormat = new Container<MarkupKind>(MarkupKind.PlainText, MarkupKind.Markdown)
            },
            DocumentSymbol = new DocumentSymbolCapability
            {
                SymbolKind = new SymbolKindCapabilityOptions
                {
                    // We use all values of the SymbolKind enum.
                    // This way, if new values are added to SymbolKind, we don't have to update this list.
                    ValueSet = Container.From(Enum.GetValues<NodeKind>().SelectMany(x => x.ToSymbolKind()))
                },
                HierarchicalDocumentSymbolSupport = true,
                TagSupport = new TagSupportCapabilityOptions
                {
                    ValueSet = Container.From(SymbolTag.Deprecated)
                },
                LabelSupport = false
            },
            Diagnostic = new DiagnosticClientCapabilities
            {
                RelatedDocumentSupport = true
            },
            PublishDiagnostics = new PublishDiagnosticsCapability
            {
                RelatedInformation = true,
                VersionSupport = false,
                TagSupport = new Supports<PublishDiagnosticsTagSupportCapabilityOptions?>(new PublishDiagnosticsTagSupportCapabilityOptions
                {
                    ValueSet = Container.From(DiagnosticTag.Unnecessary, DiagnosticTag.Deprecated)
                })
            },
            SemanticTokens = new SemanticTokensCapability
            {
                Requests = new SemanticTokensCapabilityRequests
                {
                    Full = new Supports<SemanticTokensCapabilityRequestFull?>()
                },
                Formats = new[]
                {
                    SemanticTokenFormat.Relative
                },
                TokenModifiers = new[]
                {
                    SemanticTokenModifier.Deprecated,
                    SemanticTokenModifier.Static,
                    SemanticTokenModifier.Abstract,
                    SemanticTokenModifier.Readonly,
                    SemanticTokenModifier.Async,
                    SemanticTokenModifier.Declaration,
                    SemanticTokenModifier.Definition,
                    SemanticTokenModifier.Documentation,
                    SemanticTokenModifier.Modification,
                    SemanticTokenModifier.DefaultLibrary
                },
                TokenTypes = new[]
                {
                    SemanticTokenType.Comment,
                    SemanticTokenType.Keyword,
                    SemanticTokenType.String,
                    SemanticTokenType.Number,
                    SemanticTokenType.Regexp,
                    SemanticTokenType.Operator,
                    SemanticTokenType.Namespace,
                    SemanticTokenType.Type,
                    SemanticTokenType.Struct,
                    SemanticTokenType.Class,
                    SemanticTokenType.Interface,
                    SemanticTokenType.Enum,
                    SemanticTokenType.TypeParameter,
                    SemanticTokenType.Function,
                    SemanticTokenType.Method,
                    SemanticTokenType.Property,
                    SemanticTokenType.Macro,
                    SemanticTokenType.Variable,
                    SemanticTokenType.Parameter,
                    SemanticTokenType.Label,
                    SemanticTokenType.Modifier,
                    SemanticTokenType.Event,
                    SemanticTokenType.EnumMember,
                    SemanticTokenType.Decorator
                },
                OverlappingTokenSupport = false,
                MultilineTokenSupport = false,
                ServerCancelSupport = false,
                AugmentsSyntaxTokens = false
            }
        },
        Window = new WindowClientCapabilities
        {
            WorkDoneProgress = true
        }
    };

    /// <summary>
    /// Initializes the language server such that it is ready to process requests.
    /// </summary>
    public async Task InitializeAsync(string? executablePath = null, CancellationToken token = default)
    {
        if (Server == null)
        {
            throw new InvalidOperationException("LSP server must be set before initializing the handler.\n");
        }
        await semaphore.WaitAsync(token);
        if (IsReady)
        {
            semaphore.Release();
            return;
        }

        savedDiagnostics.Clear();
        unhandledDiagnostics.Clear();
        HashSet<ProgressToken> initialWork = [];

        try
        {
            ProcessStartInfo startInfo = new(fileName: executablePath ?? Server.ServerExecutable,
                                             arguments: Server.Parameters)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = ProjectPath
            };
            try
            {
                lspProcess = Process.Start(startInfo);
            }
            catch (Win32Exception)
            {
                Trace.TraceError($"Failed to start the language server. See {Server.WebsiteURL} for setup instructions.\n");
                throw;
            }
            if (lspProcess == null)
            {
                throw new InvalidOperationException("Failed to start the language server. "
                                                    + $"See {Server.WebsiteURL} for setup instructions.\n");
            }

            if (LogLSP)
            {
                string tempDir = Path.GetTempPath();
                string outputPath = Path.Combine(tempDir, "outputLogLsp.txt");
                string inputPath = Path.Combine(tempDir, "inputLogLsp.txt");
                outputLog = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                inputLog = new FileStream(inputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            }
            else
            {
                outputLog = Stream.Null;
                inputLog = Stream.Null;
            }

            TeeStream teedInputStream = new(lspProcess.StandardOutput.BaseStream, outputLog);
            TeeStream teedOutputStream = new(lspProcess.StandardInput.BaseStream, inputLog);

            Client = LanguageClient.Create(options =>
                                               options.WithInput(teedInputStream)
                                                      .WithOutput(teedOutputStream)
                                                      .WithRootPath(ProjectPath)
                                                      .WithClientCapabilities(ClientCapabilities)
                                                      .DisableDynamicRegistration()
                                                      .DisableWorkspaceFolders()
                                                      .WithUnhandledExceptionHandler(e => Trace.TraceWarning($"Error from LSP handler:{e}"))
                                                      .WithClientInfo(new ClientInfo
                                                      {
                                                          Name = "LSP2GXL",
                                                          Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                                                      })
                                                      .WithMaximumRequestTimeout(TimeoutSpan)
                                                      .WithContentModifiedSupport(false)
                                                      .WithInitializationOptions(Server.InitOptions)
                                                      .DisableProgressTokens()
                                                      .WithWorkspaceFolder(ProjectPath, "Main")
                                                      .OnPublishDiagnostics(HandleDiagnostics)
                                                      .OnLogMessage(LogMessage)
                                                      .OnShowMessage(ShowMessage)
                                                      .OnWorkDoneProgressCreate(HandleInitialWorkDoneProgress));
            // Starting the server might take a little while.
            await Client.Initialize(token).WaitOrThrowAsync(TimeoutSpan * 8, token);
            do
            {
                // We wait until the initial work is done.
                // We detect this by checking if any work progress notifications have been sent,
                // and then wait until the progress is done. As soon as there are 500ms without
                // any progress, we assume the initial work is done.
                bool doneInTimeout = await AsyncUtils.WaitUntilTimeoutAsync(() => initialWork.Count == 0, cancellationToken: token,
                                                                            // We allow more time for the initial work to be done.
                                                                            timeout: TimeoutSpan * 4);

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken: token);
                if (!doneInTimeout)
                {
                    break;
                }
            } while (initialWork.Count > 0);
            IsReady = true;
        }
        finally
        {
            semaphore.Release();
        }
        return;

        void HandleInitialWorkDoneProgress(WorkDoneProgressCreateParams progressParams)
        {
            if (!IsReady && progressParams.Token != null)
            {
                initialWork.Add(progressParams.Token);
                _ = MonitorInitialWorkDoneProgress(progressParams.Token);
            }
        }

        async Task MonitorInitialWorkDoneProgress(ProgressToken progressToken)
        {
            await foreach (WorkDoneProgress _ in Client!.WorkDoneManager.Monitor(progressToken)
                                                        .Where(x => x.Kind == WorkDoneProgressKind.End)
                                                        .ToAsyncEnumerable().WithCancellation(token))
            {
                initialWork.Remove(progressToken);
            }
        }
    }

    /// <summary>
    /// Handles the diagnostics published by the language server by storing them
    /// in the <see cref="unhandledDiagnostics"/> queue, as well as
    /// in the <see cref="savedDiagnostics"/> dictionary.
    /// </summary>
    /// <param name="diagnosticsParams">The parameters of the diagnostics.</param>
    private void HandleDiagnostics(PublishDiagnosticsParams diagnosticsParams)
    {
        unhandledDiagnostics.Enqueue(diagnosticsParams);
        savedDiagnostics.GetOrAdd(diagnosticsParams.Uri.GetFileSystemPath(), []).Add(diagnosticsParams);
    }

    /// <summary>
    /// Handles the ShowMessage notification with the given <paramref name="messageParams"/>
    /// by showing a notification to the user.
    /// </summary>
    /// <param name="showMessageParams">The parameters of the ShowMessage notification.</param>
    private void ShowMessage(ShowMessageParams showMessageParams)
    {
        if (showMessageParams.Message.Contains("window/workDoneProgress/cancel"))
        {
            // Cancellation messages are sometimes sent to the language server even when they don't support them.
            // We can safely ignore any failing cancellations.
            return;
        }
        string languageServerName = Server.Name;
        switch (showMessageParams.Type)
        {
            case MessageType.Error:
                Trace.TraceError($"{languageServerName} Error", showMessageParams.Message);
                break;
            case MessageType.Warning:
                Trace.TraceWarning($"{languageServerName} Warning", showMessageParams.Message);
                break;
            case MessageType.Info:
                Trace.TraceInformation($"{languageServerName} Info", showMessageParams.Message);
                break;
            case MessageType.Log:
            default:
                Trace.TraceInformation($"{languageServerName} Log", showMessageParams.Message);
                break;
        }
    }

    /// <summary>
    /// Handles the LogMessage notification with the given <paramref name="messageParams"/>
    /// by logging the message to the Unity console.
    /// </summary>
    /// <param name="messageParams">The parameters of the LogMessage notification.</param>
    private static void LogMessage(LogMessageParams messageParams)
    {
        switch (messageParams.Type)
        {
            case MessageType.Error:
                if (messageParams.Message.Contains("client exited without proper shutdown sequence"))
                {
                    // We can safely ignore this message, as we don't care about shutdown errors.
                    return;
                }
                if (messageParams.Message.Contains(".mvn/wrapper [in mailserver]"))
                {
                    // This JDTLS error can be ignored—it doesn't cause any actual errors in the exported graph.
                    return;
                }
                if (messageParams.Message.Contains("Cannot download Gradle sha256 checksum"))
                {
                    // This JDTLS error can be ignored—it doesn't cause any actual errors in the exported graph.
                    return;
                }
                if (messageParams.Message.Contains("unable to compute deps errors: stat : no such file or directory"))
                {
                    // I think this error from Gopls is only relevant for external dependencies, which we don't care about.
                    return;
                }
                if (messageParams.Message.Contains("OmniSharp.Extensions.JsonRpc.DefaultRequestInvoker: Failed to handle request textDocument/didOpen"))
                {
                    // This means a document failed to open, which is fine for the OmniSharp server.
                    return;
                }
                Trace.TraceError(messageParams.Message);
                break;
            case MessageType.Warning:
                Trace.TraceWarning(messageParams.Message);
                break;
            case MessageType.Info or MessageType.Log:
            default:
                Trace.TraceInformation(messageParams.Message);
                break;
        }
    }

    /// <summary>
    /// Opens the document at the given <paramref name="path"/> in the language server.
    ///
    /// Note that the document needs to be closed manually after it is no longer needed.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    public void OpenDocument(string path)
    {
        DidOpenTextDocumentParams parameters = new()
        {
            TextDocument = new TextDocumentItem
            {
                Uri = DocumentUri.File(path),
                LanguageId = Server.LanguageIdFor(Path.GetExtension(path).TrimStart('.')),
                Version = 1,
                Text = File.ReadAllText(path)
            }
        };
        Client!.DidOpenTextDocument(parameters);
    }

    /// <summary>
    /// Closes the document at the given <paramref name="path"/> in the language server.
    /// The document needs to have been opened before.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    public void CloseDocument(string path)
    {
        DidCloseTextDocumentParams parameters = new()
        {
            TextDocument = new TextDocumentIdentifier(path)
        };
        Client!.DidCloseTextDocument(parameters);
    }

    /// <summary>
    /// Retrieves the symbols in the document at the given <paramref name="path"/>.
    ///
    /// See the LSP specification for <c>textDocument/documentSymbol</c> for more information.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    /// <returns>An asynchronous enumerable that emits the symbols in the document.</returns>
    public IAsyncEnumerable<SymbolInformationOrDocumentSymbol> DocumentSymbolsAsync(string path)
    {
        DocumentSymbolParams symbolParams = new()
        {
            TextDocument = new TextDocumentIdentifier(path),
            PartialResultToken = null
        };
        return Client!.RequestDocumentSymbol(symbolParams).ObserveEnumerableForAsync(TimeoutSpan);
    }

    /// <summary>
    /// Retrieves hover information for the document at the given <paramref name="path"/> at the given
    /// <paramref name="line"/> and <paramref name="character"/>.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    /// <param name="line">The line number in the document.</param>
    /// <param name="character">The column in the line.</param>
    /// <returns>The hover information for the document at the given position.</returns>
    public async Task<Hover?> HoverAsync(string path, int line, int character = 0)
    {
        HoverParams hoverParams = new()
        {
            TextDocument = new TextDocumentIdentifier(path),
            Position = new Position(line, character)
        };
        try {
          return await Client!.RequestHover(hoverParams).WaitOrDefaultAsync(TimeoutSpan);
        } catch (JsonRpcException e) when (Server == LSPServer.Gopls && e.Message.Contains("no package metadata for file")) {
          // This means we cannot get metadata for the given file, which is fine.
          return null;
        }
    }

    /// <summary>
    /// Requests diagnostics for the document at the given <paramref name="path"/>.
    /// If the diagnostics are not available, or the diagnostics for the given document are unchanged
    /// compared to the last call, the method returns <c>null</c>.
    ///
    /// Note that this is a very new feature (LSP 3.17) and not all language servers support it.
    /// An alternative is to use the <see cref="GetUnhandledPublishedDiagnostics"/> method to
    /// retrieve the diagnostics that have been published by the language server.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    /// <returns>The diagnostics for the document at the given path,
    /// or <c>null</c> if the diagnostics are unchanged/unavailable.</returns>
    public async Task<IEnumerable<Diagnostic>> PullDocumentDiagnosticsAsync(string path)
    {
        DocumentDiagnosticParams diagnosticsParams = new()
        {
            TextDocument = new TextDocumentIdentifier(path)
        };
        RelatedDocumentDiagnosticReport report = await Client!.RequestDocumentDiagnostic(diagnosticsParams).AsTask();
        DocumentDiagnosticReport? diagnostics = report.RelatedDocuments?[diagnosticsParams.TextDocument.Uri];
        return (diagnostics as FullDocumentDiagnosticReport)?.Items ?? [];
    }

    /// <summary>
    /// Retrieves the unhandled diagnostics that have been published by the language server.
    /// </summary>
    /// <returns>An enumerable of the unhandled published diagnostics.</returns>
    public IEnumerable<PublishDiagnosticsParams> GetUnhandledPublishedDiagnostics()
    {
        while (unhandledDiagnostics.TryDequeue(out PublishDiagnosticsParams? diagnostics))
        {
            yield return diagnostics;
        }
    }

    /// <summary>
    /// Returns the diagnostics that were saved for the given <paramref name="path"/>.
    /// Note that this may not include every diagnostic the language server would have sent,
    /// as we only listen to published diagnostics for a certain timeframe (see <see cref="LSPImporter"/>).
    /// </summary>
    /// <param name="path">The path for which to retrieve the diagnostics.</param>
    /// <returns>The published diagnostics for the given path.</returns>
    public IEnumerable<PublishDiagnosticsParams> GetPublishedDiagnosticsForPath(string path)
    {
        return savedDiagnostics.GetValueOrDefault(path) ?? Enumerable.Empty<PublishDiagnosticsParams>();
    }

    /// <summary>
    /// Retrieves all references to the symbol in the document with the given <paramref name="path"/> at the given
    /// <paramref name="line"/> and <paramref name="character"/>.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    /// <param name="line">The line number in the document.</param>
    /// <param name="character">The column in the line.</param>
    /// <param name="includeDeclaration">Whether to include the declaration of the symbol in the results.</param>
    /// <returns>An asynchronous enumerable that emits the locations of the references to the symbol.</returns>
    public IAsyncEnumerable<LocationOrLocationLink> ReferencesAsync(string path, int line, int character = 0, bool includeDeclaration = false)
    {
        ReferenceParams parameters = new()
        {
            TextDocument = new TextDocumentIdentifier(path),
            Position = new Position(line, character),
            Context = new ReferenceContext { IncludeDeclaration = includeDeclaration },
        };
        return GetLocationsByLspFuncAsync<ReferenceParams, Location>(path, line, character,
                                                                     _ => Client!.RequestReferences(parameters))
            .Select(x => new LocationOrLocationLink(x));
    }

    /// <summary>
    /// Retrieves the type definition belonging to the symbol in the document with the given
    /// <paramref name="path"/> at the given <paramref name="line"/> and <paramref name="character"/>.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    /// <param name="line">The line number in the document.</param>
    /// <param name="character">The column in the line.</param>
    /// <returns>An asynchronous enumerable that emits the location of the type definition of the symbol.</returns>
    public IAsyncEnumerable<LocationOrLocationLink> TypeDefinitionAsync(string path, int line, int character = 0)
    {
        return GetLocationsByLspFuncAsync<TypeDefinitionParams, LocationOrLocationLink>(path, line, character, p => Client!.RequestTypeDefinition(p));
    }

    /// <summary>
    /// Retrieves the declaration of the symbol in the document with the given <paramref name="path"/> at the given
    /// <paramref name="line"/> and <paramref name="character"/>.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    /// <param name="line">The line number in the document.</param>
    /// <param name="character">The column in the line.</param>
    /// <returns>An asynchronous enumerable that emits the location of the declaration of the symbol.</returns>
    public IAsyncEnumerable<LocationOrLocationLink> DeclarationAsync(string path, int line, int character = 0)
    {
        return GetLocationsByLspFuncAsync<DeclarationParams, LocationOrLocationLink>(path, line, character, p => Client!.RequestDeclaration(p));
    }

    /// <summary>
    /// Retrieves the definition of the symbol in the document with the given <paramref name="path"/> at the given
    /// <paramref name="line"/> and <paramref name="character"/>.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    /// <param name="line">The line number in the document.</param>
    /// <param name="character">The column in the line.</param>
    /// <returns>An asynchronous enumerable that emits the location of the definition of the symbol.</returns>
    public IAsyncEnumerable<LocationOrLocationLink> DefinitionAsync(string path, int line, int character = 0)
    {
        return GetLocationsByLspFuncAsync<DefinitionParams, LocationOrLocationLink>(path, line, character, p => Client!.RequestDefinition(p));
    }

    /// <summary>
    /// Retrieves the implementation of the function or method in the document with the given
    /// <paramref name="path"/> at the given <paramref name="line"/> and <paramref name="character"/>.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    /// <param name="line">The line number in the document.</param>
    /// <param name="character">The column in the line.</param>
    /// <returns>An asynchronous enumerable that emits the location of the implementation of the symbol.</returns>
    public IAsyncEnumerable<LocationOrLocationLink> ImplementationAsync(string path, int line, int character = 0)
    {
        return GetLocationsByLspFuncAsync<ImplementationParams, LocationOrLocationLink>(path, line, character, p => Client!.RequestImplementation(p));
    }

    /// <summary>
    /// Retrieves all outgoing calls for the symbol in the document with the given <paramref name="path"/> at the given
    /// <paramref name="line"/> and <paramref name="character"/>.
    ///
    /// In case there are multiple symbols at the given position, the <paramref name="selectItems"/> function is used
    /// to select the desired symbols.
    /// </summary>
    /// <param name="selectItems">A function that should return <c>true</c> for the desired symbols to select
    /// the outgoing calls for.</param>
    /// <param name="path">The path to the document.</param>
    /// <param name="line">The line number in the document.</param>
    /// <param name="character">The column in the line.</param>
    /// <returns>An asynchronous enumerable that emits the <see cref="CallHierarchyItem"/>s of the outgoing calls.</returns>
    public IAsyncEnumerable<CallHierarchyItem> OutgoingCallsAsync(Func<CallHierarchyItem, bool> selectItems,
                                                                  string path, int line, int character = 0)
    {
        CallHierarchyPrepareParams prepareParams = new()
        {
            TextDocument = new TextDocumentIdentifier(path),
            Position = new Position(line, character)
        };
        IAsyncEnumerable<CallHierarchyItem> callItems;
        try
        {
            callItems = Client!.RequestCallHierarchyPrepare(prepareParams)
                               .ObserveEnumerableForAsync(TimeoutSpan)
                               .Where(selectItems);
        }
        catch (TimeoutException)
        {
            return AsyncEnumerable.Empty<CallHierarchyItem>();
        }

        return callItems.SelectMany(item =>
        {
            CallHierarchyOutgoingCallsParams outgoingParams = new()
            {
                Item = item
            };
            // We can not use the built-in method here and have to make the request manually,
            // as the specialized method contains a bug (issue #1303 in OmniSharp/csharp-language-server-protocol).
            // return AsyncUtils.ObserveUntilTimeout(t => Client.RequestCallHierarchyOutgoing(outgoingParams, t), TimeoutSpan).Select(x => x.To);
            return MakeOutgoingCallRequest(outgoingParams).WaitOrThrowAsync(TimeoutSpan)
                                                          .ToAsyncEnumerable()
                                                          .SelectMany(x => x.ToAsyncEnumerable())
                                                          .Select(x => x.To);
        });

        Task<IEnumerable<CallHierarchyOutgoingCall>> MakeOutgoingCallRequest(CallHierarchyOutgoingCallsParams outgoingParams)
        {
            return Client!.SendRequest("callHierarchy/outgoingCalls", outgoingParams)
                          .Returning<IEnumerable<CallHierarchyOutgoingCall>>(CancellationToken.None);
        }
    }

    /// <summary>
    /// Retrieves all supertypes for the symbol in the document with the given <paramref name="path"/> at the given
    /// <paramref name="line"/> and <paramref name="character"/>.
    ///
    /// In case there are multiple symbols at the given position, the <paramref name="selectItems"/> function is used
    /// to select the desired symbols.
    /// </summary>
    /// <param name="selectItems">A function that should return <c>true</c> for the desired symbols to select
    /// the supertypes for.</param>
    /// <param name="path">The path to the document.</param>
    /// <param name="line">The line number in the document.</param>
    /// <param name="character">The column in the line.</param>
    /// <returns>An asynchronous enumerable that emits the <see cref="TypeHierarchyItem"/>s of the supertypes.</returns>
    public IAsyncEnumerable<TypeHierarchyItem> SupertypesAsync(Func<TypeHierarchyItem, bool> selectItems, string path, int line, int character = 0)
    {
        TypeHierarchyPrepareParams prepareParams = new()
        {
            TextDocument = new TextDocumentIdentifier(path),
            Position = new Position(line, character)
        };
        IAsyncEnumerable<TypeHierarchyItem> items = Client!.RequestTypeHierarchyPrepare(prepareParams)
                                                           .ObserveEnumerableForAsync(TimeoutSpan)
                                                           .Where(selectItems);
        return items.SelectMany(item =>
        {
            TypeHierarchySupertypesParams supertypesParams = new()
            {
                Item = item
            };
            return Client!.RequestTypeHierarchySupertypes(supertypesParams).ObserveEnumerableForAsync(TimeoutSpan);
        });
    }

    /// <summary>
    /// Retrieves all locations of type <typeparamref name="R"/> using the given <paramref name="lspFunction"/>.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    /// <param name="line">The line number in the document.</param>
    /// <param name="character">The column in the line.</param>
    /// <param name="lspFunction">The function to retrieve the locations.</param>
    /// <typeparam name="P">The type of the parameters for the LSP function.</typeparam>
    /// <typeparam name="R">The type of the locations to retrieve.</typeparam>
    /// <returns>An asynchronous enumerable that emits the locations of type <typeparamref name="R"/>.</returns>
    private IAsyncEnumerable<R> GetLocationsByLspFuncAsync<P, R>(string path, int line, int character,
                                                                 Func<P, IObservable<IEnumerable<R>>> lspFunction)
        where P : TextDocumentPositionParams, new()
    {
        P parameters = new()
        {
            TextDocument = new TextDocumentIdentifier(path),
            Position = new Position(line, character),
        };
        return lspFunction(parameters).ObserveEnumerableForAsync(timeout: TimeoutSpan);
    }

    /// <summary>
    /// Retrieves semantic tokens for the document at the given <paramref name="path"/>.
    ///
    /// Note that the returned semantic tokens may be empty if the document has not been fully analyzed yet.
    /// </summary>
    /// <param name="path">The path to the document.</param>
    /// <returns>The semantic tokens for the document at the given path.</returns>
    public async Task<SemanticTokens?> GetSemanticTokensAsync(string path)
    {
        SemanticTokensParams parameters = new()
        {
            TextDocument = new TextDocumentIdentifier(path)
        };
        return await Client!.RequestSemanticTokensFull(parameters);
    }

    public void ReleaseStreams()
    {
        outputLog.Dispose();
        inputLog.Dispose();
    }

    /// <summary>
    /// Shuts down the language server and exits its process.
    ///
    /// After this method is called, the language server is no longer
    /// ready to process requests until it is initialized again.
    /// </summary>
    public async Task ShutdownAsync(CancellationToken token = default)
    {
        await semaphore.WaitAsync(token);
        if (!IsReady)
        {
            // LSP server is not running.
            return;
        }

        try
        {
            if (Client != null)
            {
                await Client.Shutdown().WaitOrThrowAsync(TimeoutSpan, token);
            }
        }
        catch (InvalidParametersException)
        {
            // Some language servers (e.g., rust-analyzer) have trouble with OmniSharp's empty map.
            // They throw an InvalidParameterException, which we can ignore for now.
        }
        catch (TimeoutException)
        {
            // It's fine if the shutdown operation times out.
        }
        finally
        {
            // In case Client.SendExit() fails, we release the semaphore and resources first to avoid a deadlock.
            IsReady = false;
            semaphore.Release();

            Client?.SendExit();
            ReleaseStreams();
        }
    }
}
