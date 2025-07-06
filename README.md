# LSP2GXL

A tool that converts Language Server Protocol (LSP) information collected for a given project into a hierarchical graph in Graph eXchange Language (GXL) format.

This tool is based on a [master's thesis](https://falko.de/masterthesis.pdf) by Falko Galperin, and was further evaluated as part of a paper by Galperin, Michel, and Koschke, published to [ICSME 2025](https://conf.researchr.org/home/icsme-2025) (link will be added here once it is published).
The code contains parts from the [SEE project](https://github.com/uni-bremen-agst/SEE), such as the graph definition, in slightly modified form (to work outside of Unity in a "pure" C#/.NET environment).

## Build

With .NET 9.0 (or later) installed, you can run `dotnet publish -c Release` to create a build of the application. It should then be accessible at `bin/Release/net9.0/LSP2GXL`, though the path may vary per platform.
You can also use `dotnet run` directly (putting any parameters after a `--`), but this will run in Debug mode by default.

## Usage

Assuming you have a project at `~/sample-rs` that you want to analyze with the Rust Analyzer—and the project only has one source folder of interest at `src`—you could use the following invocation of LSP2GXL to create a GXL file at `output.gxl`:

```bash
$ LSP2GXL ~/sample-rs -o output.gxl -s ~/sample-rs/src -l rust-analyzer
```

Run the tool with the `-h`/`--help` parameter to get an overview of all possible options:

```bash
$ LSP2GXL --help
Description:
  A tool that transforms LSP project information into a GXL file.

Usage:
  LSP2GXL <project> [options]

Arguments:
  <project>  Path to the root of the project that shall be analyzed.

Options:
  -o, --output <output>                                The output GXL file to write to. If not given, the output graph will be discarded.
  --overwrite                                          Whether to overwrite the output file if it already exists.
  -l, --lsp-server <lsp-server> (REQUIRED)             The LSP server to use.
  -x, --lsp-server-executable <lsp-server-executable>  The path to the executable of the LSP server. Note that the given server must match the configured language server.
  --log-lsp                                            Whether to log the raw LSP input and output to a temporary file at /tmp/.
  -s, --source-path <source-path>                      The path to a directory (under the project root) whose contents shall be analyzed. If this is not given, we will query the whole project root.
  -e, --exclude-source-path <exclude-source-path>      The path to a directory (under the project root) whose contents shall NOT be analyzed.
  --timeout <timeout>                                  The maximum time in seconds to wait for the LSP server to respond to any given query. [default: 10]
  --edge-type <edge-type>                              LSP edge type to include in the import. [default: TypeDefinition, Implementation, Reference, Call, Extend]
  --self-references                                    Whether to allow self-references in the generated graph.
  --parent-references                                  Whether to allow references from an element to its direct parent in the generated graph.
  --node-type <node-type>                              LSP node type to include in the import. [default: All]
  --diagnostic-level <diagnostic-level>                LSP diagnostic level to include in the import. [default: All]
  --unoptimized-edges                                  Whether to disable the optimized (kd-tree based) edge connection algorithm.
  -p, --performance-output <performance-output>        The output file to write CSV performance information to. Must not exist yet. [default: performance.csv]
  --version                                            Show version information
  -?, -h, --help                                       Show help and usage information
```

> [!NOTE]
> Aborting a program run with <kbd>Ctrl</kbd>+<kbd>C</kbd> might not take effect immediately, especially during the edge creation phase.
