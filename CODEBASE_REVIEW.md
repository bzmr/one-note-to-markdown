# OneNote to Markdown Exporter - Comprehensive Code Review

## Summary Documentation

### Purpose & Functionality
The **OneNote to Markdown Exporter** is a .NET 10 desktop application that converts OneNote notebooks into well-organized Markdown files while preserving content structure, metadata, formatting, and embedded assets. The application provides both a WPF graphical interface and a command-line interface (CLI) to accommodate different user workflows.

### Key Design Philosophy
The codebase adheres to a deliberate philosophy of **simplicity over complexity**:
- **Shared Logic**: GUI and CLI both use identical `ExportService` to avoid duplicated behavior
- **Single Responsibility**: Each service handles one concern (path planning, markdown conversion, asset resolution, etc.)
- **Testability**: All core logic is unit-testable without requiring OneNote to be installed
- **Data Loss Prevention (DLP) Safe**: Uses COM `GetPageContent()` with raw XML instead of `Publish()` to bypass DLP policy restrictions

### Architecture Overview
The application uses a **layered, service-oriented architecture**:

```
┌─────────────────────────────────────┐
│   UI Layer (WPF + CLI)              │
│ MainWindow.xaml.cs / CliHandler.cs  │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│   Orchestration (ExportService)     │
│  - Coordinates all export steps     │
│  - Manages progress reporting       │
│  - Handles error recovery           │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────────────────────┐
│   Specialized Services Layer                        │
├──────────────┬──────────────┬──────────────────────┤
│ OneNoteServ. │ XMLConverter │ PathPlanning Services│
│ (COM Interop)│(XML→HTML→MD) │ (Collision Detection)│
├──────────────┼──────────────┼──────────────────────┤
│ LintService  │AssetResolver │ TimestampService    │
│ (Markdown)   │ (File Layout) │ (Metadata Preserve) │
└──────────────▴──────────────▴──────────────────────┘
```

### Data Flow: OneNote → Markdown
```
1. User initiates export (GUI or CLI)
   ↓
2. Load full OneNote hierarchy via COM (cached for duration)
   ↓
3. Filter selected items (notebooks, sections, pages)
   ↓
4. Pre-compute all file paths and detect name collisions
   ↓
5. Build hyperlink index for OneNote link rewriting
   ↓
6. Recursively process each page:
   - Fetch page XML from OneNote
   - Extract images/attachments to assets folder
   - Convert OneNote XML → HTML → Markdown
   - Rewrite OneNote links to relative .md paths
   - Apply optional YAML front matter
   - Run markdownlint for formatting cleanup
   - Preserve file timestamps (optional)
   ↓
7. Archive export logs and return statistics
```

### Technology Stack
| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| **Runtime** | .NET | 10.0 | Windows desktop framework |
| **GUI** | WPF + Windows Forms | Native | User interface |
| **OneNote API** | COM Interop | 1.1.0.2 | Live notebook access (no installation required) |
| **Content Processing** | HtmlAgilityPack | 1.12.4 | HTML DOM manipulation |
| **Markdown Generation** | ReverseMarkdown | 5.3.0 | HTML-to-Markdown conversion |
| **CLI Parsing** | System.CommandLine | 2.0.7 | Argument parsing & validation |
| **Markdown Linting** | Node.js + markdownlint-cli2 | Bundled | Post-export formatting |

### Key Features
1. **Dual Interface**: Same export engine for GUI and CLI
2. **Unified Config System**: JSON-based config file for reproducible exports (GUI and CLI)
3. **Selection Persistence**: Saves/restores notebook, section, and page selections between sessions
4. **Bulk Selection**: "Select All Notebooks" checkbox for quick multi-notebook exports
5. **DLP-Safe Extraction**: Bypasses sensitivity label restrictions
6. **Path Collision Handling**: Pre-computes collisions and appends stable hashes
7. **Asset Organization**: Centralized, notebook-scoped, section-scoped, or page-scoped storage
8. **Hyperlink Rewriting**: Converts OneNote links to relative Markdown links
9. **Persistent Caching**: Hyperlink cache survives restarts for faster re-exports
10. **Metadata Preservation**: Optional YAML front matter with creation/modification dates
11. **Timestamp Preservation**: Sets file creation/modification times from OneNote data
12. **Table Pipe Escaping**: Handles pipe characters in table cells for valid Markdown output
13. **Format Validation**: Optional markdownlint-cli2 integration for output cleanup
14. **Network Resilience**: Local logs written first; network archive failures non-fatal

---

## File-by-File Explanation

### Project Structure
```
OneNoteMarkdownExporter/
├─ App.xaml / App.xaml.cs              [WPF application entry point]
├─ MainWindow.xaml / MainWindow.xaml.cs [Primary UI]
├─ OneNoteMarkdownExporter.csproj      [Project configuration]
├─ AssemblyInfo.cs                      [Assembly metadata]
├─ Models/
│  └─ OneNoteItem.cs                   [Hierarchy node model]
├─ Services/
│  ├─ ExportService.cs                 [Main orchestrator]
│  ├─ ExportConfigFile.cs              [Config model & persistence]
│  ├─ OneNoteService.cs                [COM interop wrapper]
│  ├─ OneNoteXmlToMarkdownConverter.cs  [Content converter]
│  ├─ ExportPathPlanner.cs             [Path pre-computation]
│  ├─ ExportPathSanitizer.cs           [Windows path safety]
│  ├─ AssetPathResolver.cs             [Asset folder routing]
│  ├─ ExportOptions.cs                 [Configuration object]
│  ├─ ExportSelectionHelper.cs         [Tree navigation]
│  ├─ CliHandler.cs                    [CLI interface]
│  ├─ MarkdownLintCliService.cs        [Linting integration]
│  ├─ YamlFrontMatterService.cs        [YAML metadata]
│  ├─ FileTimestampService.cs          [Timestamp preservation]
│  ├─ ExportFailureFormatter.cs        [Error formatting]
│  └─ [Various interfaces]             [Service contracts]
└─ resources/
   ├─ node.exe                         [Bundled Node.js runtime]
   ├─ markdownlint-cli2.mjs            [Linting script]
   ├─ .markdownlint.json               [Linting config]
   └─ THIRD-PARTY-NOTICES.txt          [License notices]
```

### Core Files

#### **App.xaml & App.xaml.cs**
- **Purpose**: WPF application entry point and startup orchestration
- **Responsibility**: 
  - Detects CLI vs. GUI mode via argument inspection
  - CLI mode: Attaches/allocates console, runs `CliHandler.RunAsync()`, exits
  - GUI mode: Continues normal WPF initialization to MainWindow
- **Key Detail**: Uses P/Invoke to `AttachConsole()` for proper console attachment in CLI mode (critical for piping and script integration)

#### **MainWindow.xaml.cs**
- **Purpose**: Primary UI controller for WPF application
- **Responsibility**:
  - Loads OneNote hierarchy into WPF tree control
  - Manages user selection state (`IsSelected`, `IsExpanded` binding)
  - Provides export options UI (output path, asset organization, linting, etc.)
  - Loads/saves export configuration via `ExportConfigFileService`
  - Calls `ExportService.ExportSelectedAsync()` and tracks progress
- **Key Features**:
  - Real-time progress reporting (pages exported/failed counts)
  - Configuration persistence via `%LOCALAPPDATA%\OneNoteMarkdownExporter\export-config.json`
  - Selection persistence: Saves and restores selected notebooks, sections, and pages
  - "Select All Notebooks" checkbox for bulk selection convenience
  - Error dialog display for OneNote connection failures
  - Async/await pattern to keep UI responsive during export
- **Configuration Lifecycle**:
  - On load: `LoadExportConfig()` reads export config and applies saved selections to tree
  - On save: `SaveExportConfigFromForm()` collects current tree selections and saves to config file
  - Config includes all 16 export options plus selection criteria (NotebookNames, SectionPaths, PageIds)

#### **OneNoteMarkdownExporter.csproj**
- **Purpose**: Project configuration and build instructions
- **Key Settings**:
  ```xml
  <Version>1.0.0</Version>
  <TargetFramework>net10.0-windows</TargetFramework>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  ```
- **Version Management**: Explicit `<Version>` property enables `--version` CLI flag; auto-incremented for releases
- **Bundled Resources**: Commits `node.exe`, `markdownlint-cli2.mjs`, and default linting config to repo (copied to output on build)
- **Build Validation**: Pre-build target ensures markdown linting runtime is present
- **Deployment**: Publishes as single self-contained executable; no .NET installation required on target machine

#### **OneNoteItem.cs** (Models)
- **Purpose**: Represents OneNote hierarchy node for binding to WPF controls
- **Properties**:
  - `Id`: Runtime OneNote identifier
  - `Name`, `Type` (Notebook, SectionGroup, Section, Page)
  - `PageLevel`: Indentation level (critical for nested page reconstruction)
  - `CreatedTime`, `LastModifiedTime`: Metadata from OneNote
  - `IsSelected`, `IsExpanded`: WPF binding properties (trigger `PropertyChanged` events)
  - `Children`: Nested items for tree structure
- **Event**: Static `SelectionChanged` event notifies UI when user selects/deselects items
- **Binding**: Implements `INotifyPropertyChanged` for reactive UI updates

---

### Service Layer: Core Logic

#### **ExportConfigFile.cs** (Configuration Persistence)
- **Purpose**: Unified JSON config model for both GUI and CLI with automatic serialization
- **Key Classes**:
  ```csharp
  public class ExportConfigFile
  {
      public int Version { get; set; } = 1;  // Schema version for future compatibility
      public string? OutputPath { get; set; }
      public string? AssetsFolderPath { get; set; }
      public string? AssetOrganizationMode { get; set; }
      public bool? Overwrite { get; set; }
      public bool? ApplyLinting { get; set; }
      public string? LintConfigPath { get; set; }
      public bool? ExportAll { get; set; }
      public List<string>? NotebookNames { get; set; }
      public List<string>? SectionPaths { get; set; }
      public List<string>? PageIds { get; set; }
      public bool? DryRun { get; set; }
      public bool? Verbose { get; set; }
      public bool? Quiet { get; set; }
      public bool? PreserveDates { get; set; }
      public string? DateMetadataMode { get; set; }
      public string? ListMode { get; set; }
  }
  ```
- **Static Service Methods**:
  - `GetDefaultConfigPath()`: Returns `%LOCALAPPDATA%/OneNoteMarkdownExporter/export-config.json`
  - `Load(path)`: Deserializes JSON config file; returns empty config if file missing
  - `TryLoad(path)`: Returns null if file doesn't exist
  - `Save(path, config)`: Serializes to JSON with camelCase naming and enum-to-string conversion
- **JSON Serialization**:
  - Property naming: `camelCase` (e.g., `outputPath`, `assetOrganizationMode`)
  - Enums: Converted to lowercase strings for readability
  - Example config file: [docs/export-config.example.json](docs/export-config.example.json)
- **Selection Persistence**: Stores notebook names, section paths, and page IDs for reproducible exports

#### **ExportService.cs** (Orchestrator)
- **Purpose**: Central coordinator for all export operations; UI-agnostic
- **Shared by**: GUI (MainWindow) and CLI (CliHandler)
- **Key Methods**:
  ```csharp
  // Full export with progress reporting
  ExportAsync(
    ExportOptions options,
    IProgress<ExportProgressUpdate>? progress = null,
    CancellationToken cancellation = default
  ) → ExportResult

  // Export subset of items (maintains full hierarchy for link planning)
  ExportSelectedAsync(
    List<OneNoteItem> selectedItems,
    ...
  ) → ExportResult
  ```
- **Internal Workflow**:
  1. Validates options and output directory
  2. Creates local log file in `%LOCALAPPDATA%/logs/`
  3. Loads full OneNote hierarchy via `IOneNoteExportSource`
  4. Applies selection filters (--all, --notebook, --section, --page)
  5. Loads persistent hyperlink cache from disk
  6. Builds path index with collision detection via `ExportPathPlanner`
  7. Recursively processes pages:
     - `ExportItemsAsync()` → tree traversal
     - `ExportPageAsync()` → single-page conversion
  8. Archives local logs to `<output>/_export_logs/` (async, non-blocking)
- **Result Object**:
  ```csharp
  public class ExportResult
  {
      public int ExportedPages { get; set; }
      public int FailedPages { get; set; }
      public int TotalPages { get; set; }
      public List<string> Warnings { get; set; }
      public string? Error { get; set; }  // Only fatal errors
  }
  ```
- **Error Handling**:
  - Page-level failures logged but non-fatal (continues export)
  - Fatal errors stop export and populate `Error` property
  - Asset failures logged as warnings (non-blocking)
  - Network failures on log archiving ignored (logs already written locally)

#### **OneNoteService.cs** (COM Interop)
- **Purpose**: Wrapper around Microsoft Office Interop for OneNote COM API
- **Implements**: `IOneNoteExportSource`, `IOneNoteHyperlinkSource`
- **Key Methods**:
  ```csharp
  // Load all notebooks with full hierarchy
  GetNotebookHierarchy() → List<OneNoteItem>

  // Get page XML with embedded images (base64)
  GetPageContent(pageId) → string (raw XML)

  // Fetch binary objects referenced by callback ID
  GetBinaryPageContent(pageId, callbackId) → string? (base64)

  // Convert runtime ID to portable hyperlink
  GetHyperlinkToObject(hierarchyId) → string?
  ```
- **Architecture**:
  - Lazy initialization: Connects to OneNote on first call
  - `GetHierarchy()` call with `HierarchyScope.hsPages` returns all items in XML
  - Parses XML with XDocument; extracts nested `<Notebook>`, `<SectionGroup>`, `<Section>`, `<Page>` elements
  - Handles page-level nesting (parent pages with child pages)
  - Calls `BuildPageHierarchy()` to reconstruct hierarchy based on page levels
- **Error Handling**:
  - Catches COM exceptions if OneNote not running
  - Throws custom exception with user-friendly message
  - No silent failures; explicit error reporting

#### **OneNoteXmlToMarkdownConverter.cs** (Content Conversion)
- **Purpose**: Transforms OneNote page XML into Markdown
- **Implements**: `IMarkdownContentConverter`, `IAssetExportStatisticsProvider`
- **Conversion Pipeline**:
  ```
  OneNote Page XML
    ↓
  1. Parse namespace-qualified XML (<T>, <Image>, <InsertedFile>, etc.)
  2. Extract page title from <Title> element
  3. Process <Outline> (content containers):
     - Text elements with styling (bold, italic, strikethrough)
     - Lists (<List>, <Bullet>, <Number>)
     - Tables (<Table>)
     - Images (<Image> with base64 or callback ID)
     - Attachments (<InsertedFile>)
  4. Build intermediate HTML
  5. Convert to Markdown via ReverseMarkdown (GFM flavor)
  6. Final cleanup and link rewriting
    ↓
  Markdown output
  ```
- **Key Features**:
  - **Asset Handling**: Extracts images to assets folder, generates relative paths for Markdown links
  - **Statistics Tracking**: Counts source/exported/failed images and attachments
  - **Diagnostic Logging**: Tracks unprocessed images (OCR fallbacks, printout suppression)
  - **Literal Angle Bracket Protection**: Escapes `<` and `>` to prevent Markdown misinterpretation
  - **Table Pipe Escaping**: Escapes pipe characters (`|`) in table cells to `\|` to prevent "too many cells" Markdown parsing errors
  - **Collapsed Section Expansion**: Automatically expands collapsed OneNote paragraphs before processing
- **Configuration**:
  ```csharp
  var config = new ReverseMarkdown.Config
  {
      UnknownTags = Config.UnknownTagsOption.Drop,
      GithubFlavored = true,
      RemoveComments = true,
      SmartHrefHandling = true
  };
  ```

#### **ExportPathPlanner.cs** (Path Pre-computation)
- **Purpose**: Pre-computes all file paths and detects name collisions before writing
- **Critical Design**: Prevents silent overwrites; ensures stable, predictable output structure
- **Key Methods**:
  ```csharp
  // Build index of all page paths with collision detection
  BuildPagePathIndexes(fullHierarchy) → PagePathIndexes

  // Create context for single-page export
  CreatePageContext(page, markdownFolderPath, assetsFolderPath) → PageExportContext
  ```
- **Collision Detection**:
  - Sanitizes all filenames via `ExportPathSanitizer`
  - Detects when multiple pages produce identical filenames
  - Appends stable hash suffix to collisions: `Page__a1b2c3d4`
  - Hash is deterministic (same page ID always produces same hash)
- **Asset Folder Tracking**: Prevents duplicate asset folder names when using scoped modes

#### **ExportPathSanitizer.cs** (Windows Path Safety)
- **Purpose**: Enforces Windows filesystem constraints for portable, collision-free filenames
- **Constraints Enforced**:
  - Max filename: 255 characters (UTF-8 bytes for NAS compatibility)
  - Max full path: 259 characters (safety margin for edge cases)
  - Reserved names: CON, PRN, AUX, NUL, COM1-COM9, LPT1-LPT9 + Unicode variants (COM¹, LPT²)
  - Invalid characters: `< > : " / \ | ? *`
  - Trailing dots/spaces removed
- **Process**:
  1. Remove/replace forbidden characters
  2. Check UTF-8 byte length (critical for NAS mounts where encoding matters)
  3. Truncate + append hash if exceeds limits
  4. Mangle reserved names
  5. Returns sanitized filename with stable hash for collision resolution

#### **AssetPathResolver.cs** (Asset Folder Routing)
- **Purpose**: Determines asset folder paths based on organization mode
- **Modes Supported**:
  - `Centralized`: All assets in `<output>/_assets`
  - `Notebook`: Scoped per notebook: `<output>/_assets_NotebookName`
  - `Section`: Scoped per section: `<output>/_assets_SectionName`
  - `Page`: Scoped per page: `<output>/_assets_PageName`
- **Outputs**: Relative paths from markdown file location (always forward slashes for Markdown links)

#### **ExportOptions.cs** (Configuration)
- **Purpose**: Immutable export configuration object
- **Properties**:
  ```csharp
  // Output
  string OutputPath
  string? AssetsFolderPath
  AssetOrganizationMode AssetOrganizationMode

  // Behavior
  bool Overwrite
  bool ApplyLinting
  string? LintConfigPath
  bool PreserveDates
  DateMetadataMode DateMetadataMode

  // Selection
  bool ExportAll
  List<string>? NotebookNames
  List<string>? SectionPaths        // "Notebook/Section" format
  List<string>? PageIds

  // Diagnostics
  bool DryRun
  bool Verbose
  bool Quiet
  ```
- **Validation**: Ensures lint config exists if specified, prevents invalid option combinations

#### **CliHandler.cs** (Command-Line Interface)
- **Purpose**: CLI mode implementation using System.CommandLine 2.0.7
- **Mode Detection**: Checks for known flags (--all, --notebook, --output, etc.)
- **Supported Commands**:
  ```
  --config <path>                Path to JSON export config file
  --all                          Export all notebooks
  --notebook <name> ...          Specific notebook(s)
  --section <path> ...           Section paths (Notebook/Section)
  --page <id> ...                Page IDs
  --output, -o <path>           Output directory
  --assets-folder <path>         Custom assets location
  --asset-organization <mode>    centralized|notebook|section|page
  --overwrite                    Overwrite files
  --no-lint                      Disable markdownlint
  --lint-config <path>           Custom .markdownlint.json
  --no-preserve-dates            Skip timestamp preservation
  --date-metadata <mode>         none|yaml
  --list                         Show hierarchy without exporting
  --dry-run                      Preview export
  --verbose, -v                  Detailed output
  --quiet, -q                    Only errors
  --help, -h                     Show usage
  --version                      Show application version
  ```
- **Config File Integration**:
  - `--config` flag loads options from JSON file
  - `--config` without path argument uses default location: `%LOCALAPPDATA%/OneNoteMarkdownExporter/export-config.json`
  - `--config /path/to/config.json` loads from specified path
  - Explicit CLI arguments override config file values
  - Proper boolean flag detection prevents `--all` from being ignored after config merge
- **Flow**:
  1. Parse arguments
  2. Load config file if `--config` specified (or use defaults)
  3. Override config with explicit CLI arguments
  4. Build `ExportOptions` from merged values
  5. Validate selection and options
  6. List mode: Print hierarchy and exit
  7. Dry-run mode: Report what would be exported
  8. Normal mode: Call `ExportService.ExportAsync()`
  9. Print summary with exit code (0=success, 1=error, 130=Ctrl+C)

#### **MarkdownLintCliService.cs** (Linting)
- **Purpose**: Optional post-export formatting validation via markdownlint-cli2
- **Design**: Uses bundled Node.js + markdownlint-cli2.mjs (no external dependencies)
- **Process**:
  1. Check availability: `node.exe`, `markdownlint-cli2.mjs`, `.markdownlint.json` present
  2. Write markdown to temp file
  3. Execute: `node.exe markdownlint-cli2.mjs :/./<file> --config <path> --fix`
  4. Exit codes: 0=no errors, 1=errors fixed, 2=config error
  5. Read fixed content back; clean up temp file
- **Async**: Uses `Process.WaitForExitAsync()` for non-blocking I/O

#### **YamlFrontMatterService.cs** (Metadata Headers)
- **Purpose**: Adds optional YAML metadata header to markdown
- **Output Format**:
  ```yaml
  ---
  created: "2024-10-02 14:30 UTC"
  updated: "2024-10-03 09:15 UTC"
  ---
  ```
- **Condition**: Only added if `DateMetadataMode == Yaml` and timestamps available

#### **FileTimestampService.cs** (Timestamp Preservation)
- **Purpose**: Sets file `CreationTimeUtc` and `LastWriteTimeUtc` from OneNote metadata
- **Condition**: Only applied if `PreserveDates == true`
- **Benefit**: Exported files retain original creation/modification times from OneNote

#### **ExportSelectionHelper.cs** (Tree Navigation)
- **Purpose**: Utilities for traversing OneNote hierarchy with selection state
- **Methods**:
  - `CountPagesToExport()`: Recursive leaf count (only pages)
  - `CountItemsToExport()`: All items (containers + pages)
  - `HasSelectedDescendants()`: Check subtree for selections
- **Supports**: Implicit selection (parent selected → children included)

#### **ExportFailureFormatter.cs** (Error Logging)
- **Purpose**: Human-readable failure log formatting
- **Output Format**:
  ```
  Page: My Page
  Page ID: {guid}
  Target: /path/to/file.md
  Error: {exception message}
  ```

---

## Code Review

### Architecture Patterns & Observations

#### 1. **Shared Export Logic Pattern** ✓ (Well-Designed)
Both GUI and CLI use identical `ExportService`, avoiding duplicated behavior:

```csharp
// GUI (MainWindow.xaml.cs)
var result = await _exportService.ExportSelectedAsync(
    selectedItems,
    _exportOptions,
    exportProgress,
    cancellation
);

// CLI (CliHandler.cs)
var result = await _exportService.ExportAsync(
    options,
    cliProgress,
    cancellation
);
```

**Observation**: Excellent design choice. Single source of truth prevents divergent behavior between interfaces. Easy to add new UI modes (e.g., REST API) without duplicating logic.

---

#### 2. **COM Interop Error Handling** ✓ (Robust)
```csharp
public List<OneNoteItem> GetNotebookHierarchy()
{
    try
    {
        _oneNoteApp.GetHierarchy(null, HierarchyScope.hsPages, out string xmlString);
        // ... parse and return
    }
    catch (COMException ex)
    {
        throw new InvalidOperationException(
            "OneNote Desktop is not running or COM initialization failed.",
            ex
        );
    }
}
```

**Observation**: COM exceptions caught and re-wrapped with clear messaging. Fails immediately with no silent errors. Helps users understand why connection failed.

---

#### 3. **Path Collision Prevention** ✓ (Defensive)
```csharp
// ExportPathPlanner detects collisions pre-flight
var collisions = pages.GroupBy(p => SanitizeName(p.Name))
    .Where(g => g.Count() > 1);

foreach (var group in collisions)
{
    foreach (var page in group)
    {
        // Append stable hash: Page__a1b2c3d4
        fileName = $"{baseName}__{stableHash(page.Id)}";
    }
}
```

**Observation**: Pre-computation before any writes prevents silent overwrites. Hash is deterministic (same page ID always produces same filename), ensuring stable exports across re-runs. Excellent defensive design.

---

#### 4. **Local-First Logging** ✓ (Network-Resilient)
```csharp
// Write local log FIRST (always succeeds)
var localLog = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "OneNoteMarkdownExporter", "logs", logFileName
);
File.WriteAllText(localLog, logContent);

// TRY to archive to network (best-effort)
try
{
    File.Copy(localLog, Path.Combine(outputPath, "_export_logs", logFileName), overwrite: true);
}
catch
{
    // Silent failure; logs already written locally
}
```

**Observation**: Excellent resilience pattern. Local logs never fail export; network archives are bonus. Survives NAS outages, SMB timeouts. Critical for enterprise environments with network instability.

---

#### 5. **Asset Export Statistics Tracking** ✓ (Diagnostic-Rich)
```csharp
public interface IAssetExportStatisticsProvider
{
    int SourceImages { get; }
    int SuccessfulImageExports { get; }
    int FailedImageExports { get; }
    int UnprocessedImageObjects { get; }
    int RecoveredImageOcrFallbacks { get; }
    IReadOnlyList<string> UnprocessedImageDiagnostics { get; }
    IReadOnlyList<string> ImageFailureDiagnostics { get; }
}
```

**Observation**: Comprehensive tracking enables root-cause analysis. Users can see exactly which images failed and why (OCR fallback, printout suppression, etc.). Exceptional for debugging and user support.

---

#### 6. **XML to Markdown Pipeline** ✓ (Clean Separation)
```csharp
// OneNote XML → Intermediate HTML
var html = BuildHtmlFromXml(pageXml);

// HTML → Markdown (via third-party library)
var markdown = _markdownConverter.Convert(html);

// Final cleanup
markdown = NormalizeAnchors(markdown);
markdown = RemoveHtmlComments(markdown);
```

**Observation**: Each stage has a single responsibility. Easy to test independently. Easy to swap out `ReverseMarkdown` if needed (though it's a good choice).

---

#### 7. **Hyperlink Cache Persistence** ✓ (Performance-Conscious)
```csharp
// Cache expensive COM calls to JSON
// Survives application restarts
// Re-exported notebooks get fast hyperlink rewriting

var cache = JsonSerializer.Deserialize<Dictionary<string, string>>(
    File.ReadAllText(cacheFile)
);

if (cache.TryGetValue(pageId, out var portableLink))
{
    return portableLink;  // Cache hit: O(1) lookup
}

// Cache miss: Expensive COM call
var link = _oneNoteService.GetHyperlinkToObject(hierarchyId);
cache[pageId] = link;
```

**Observation**: Smart optimization for large notebooks. Second export much faster than first. Cache invalidation strategy: implicit (missing entries trigger refresh). Minimal maintenance burden.

---

### Potential Improvements & Recommendations

#### 1. **Nullable Handling** ⚠ (Minor)
**Observation**: `ExportOptions` has many nullable properties that require validation at multiple sites:

```csharp
public string? OutputPath { get; set; }
public string? LintConfigPath { get; set; }
public List<string>? NotebookNames { get; set; }
```

**Recommendation**: Consider a builder pattern or validation layer to enforce invariants early:
```csharp
// Before
if (options.OutputPath == null) throw new ArgumentException(...);

// After
var validatedOptions = ExportOptions.Validate(options)
    ?? throw new ArgumentException(...);
```

This reduces null-check boilerplate throughout the codebase.

---

#### 2. **Error Message Consistency** ⚠ (Minor)
**Observation**: Error formatting slightly varies across services:
- `ExportFailureFormatter` uses structured format
- `OneNoteService` throws exceptions with ad-hoc messages
- `CliHandler` prints summaries

**Recommendation**: Centralize error message formatting in a single, reusable `ErrorFormatter` class:
```csharp
public static class ErrorFormatter
{
    public static string FormatPageFailure(OneNoteItem page, Exception ex) { ... }
    public static string FormatExportSummary(ExportResult result) { ... }
}
```

---

#### 3. **Asset Fetch Retry Logic** ⚠ (Potential Enhancement)
**Observation**: Binary asset fetching via `GetBinaryPageContent()` has no retry logic:
```csharp
public string? GetBinaryPageContent(string pageId, string callbackId)
{
    // Single attempt; failure = null
    _oneNoteApp.GetBinaryContent(pageId, callbackId, out string base64);
    return base64;
}
```

**Recommendation**: Consider exponential backoff retry for transient OneNote API failures:
```csharp
var retry = new RetryPolicy { MaxAttempts = 3, DelayMs = 100 };
var base64 = await retry.ExecuteAsync(
    () => _oneNoteApp.GetBinaryContent(pageId, callbackId)
);
```

This improves resilience against intermittent OneNote COM timeouts.

---

#### 4. **CLI Argument Validation** ✓ (Good State)
**Observation**: CLI already validates options well:
```csharp
if (options.ExportAll && options.NotebookNames?.Any() == true)
{
    throw new InvalidOperationException(
        "Cannot specify both --all and --notebook"
    );
}
```

**Keep This**: Validation is defensive and helpful.

---

#### 5. **Dependency Injection Scope** ⚠ (Minor)
**Observation**: Services are instantiated manually in `MainWindow.xaml.cs`:
```csharp
var oneNoteService = new OneNoteService();
var xmlConverter = new OneNoteXmlToMarkdownConverter();
var exportService = new ExportService(oneNoteService, xmlConverter, ...);
```

**Recommendation** (if project grows): Consider a lightweight DI container:
```csharp
var services = new ServiceCollection();
services.AddSingleton<IOneNoteExportSource, OneNoteService>();
services.AddSingleton<IMarkdownContentConverter, OneNoteXmlToMarkdownConverter>();
services.AddScoped<ExportService>();
var provider = services.BuildServiceProvider();
```

This reduces constructor boilerplate and improves testability. Current approach is fine for current scale.

---

### Code Quality Observations

| Aspect | Status | Notes |
|--------|--------|-------|
| **Null Safety** | ✓ Good | Nullable reference types enabled; mostly well-annotated |
| **Error Handling** | ✓ Excellent | Graceful degradation; non-fatal failures logged |
| **Async/Await** | ✓ Good | Proper use of `async`/`await`; no blocking calls on UI thread |
| **Logging** | ✓ Good | Local logs always succeed; network archive best-effort |
| **Testability** | ✓ Excellent | Service interfaces enable comprehensive mocking |
| **Documentation** | ✓ Good | XML doc comments on public APIs; architecture clear |
| **Performance** | ✓ Good | Hierarchy cached; hyperlink caching; async I/O |
| **DLP Safety** | ✓ Excellent | Uses `GetPageContent()` not `Publish()` |

---

### Testing Strategy

The test suite (in `OneNoteMarkdownExporter.Tests/`) follows good practices:

**Test Categories**:
- **Unit Tests**: Individual service methods (path sanitization, YAML formatting, etc.)
- **Integration Tests**: Service combinations (full export pipeline with mocks)
- **CLI Tests**: Argument parsing and validation

**Example Test Pattern**:
```csharp
[Test]
public void ExportPathSanitizer_HandlesReservedNames()
{
    var result = ExportPathSanitizer.Sanitize("CON.md");
    Assert.That(result, Is.EqualTo("CON_a1b2c3d4.md"));  // Mangled
}

[Test]
public void OneNoteXmlToMarkdownConverter_PreservesInlineFormatting()
{
    var xml = "<T xml:space=\"preserve\"><B>bold</B> <I>italic</I></T>";
    var markdown = _converter.Convert(xml, ...);
    Assert.That(markdown, Contains.Substring("**bold**"));
    Assert.That(markdown, Contains.Substring("*italic*"));
}
```

**Observation**: Tests use mocks for `IOneNoteExportSource`, enabling offline testing without OneNote installation. Excellent for CI/CD.

---

## Summary

### Strengths
1. **Well-architected**: Clear separation of concerns; each service has single responsibility
2. **Resilient**: Local-first logging; graceful error handling; DLP-safe extraction
3. **Tested**: Comprehensive unit and integration tests; mockable dependencies
4. **User-Friendly**: Both GUI and CLI; progress reporting; detailed diagnostics
5. **Defensive**: Collision detection pre-flight; path sanitization; validation
6. **Performant**: Caching strategies; async I/O; no unnecessary reloads

### Areas for Refinement
1. Centralize error message formatting
2. Consider builder pattern for option validation
3. Add retry logic for transient binary asset failures
4. Optional: Add lightweight DI container if project scales

### Verdict
**Excellent production-ready codebase**. The architecture is simple, intentional, and avoids over-engineering. The emphasis on testability and resilience is commendable. Code is maintainable and extensible.

---

