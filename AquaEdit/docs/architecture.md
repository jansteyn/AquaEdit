# 🚀 Stack for AquaEdit

Frontend: Avalonia 11 (.NET 8)

Use MVVM pattern with ReactiveUI

UI: AvaloniaEdit (syntax/text control)

Theming: FluentAvaloniaUI

Backend Core: Cross-platform .NET library

Handles file chunking, async I/O, undo/redo, search

Plugin System: MEF or reflection-based discovery

Build System: SDK-style projects; CI/CD via GitHub Actions

Packaging: dotnet publish + Avalonia.DesktopRuntime

💡 The Problem

AquaEdit must handle very large files (hundreds of MBs to GBs) without:

Loading the entire file into memory.

Freezing the UI when scrolling, searching, or editing.

Losing sync between disk and the visual buffer.

A naive File.ReadAllText() or StreamReader approach simply won’t scale.

We need random-access, paged file reading/writing — and that’s exactly what MemoryMappedFile provides.

⚙️ What Is MemoryMappedFile?

A MemoryMappedFile (MMF) allows you to:

Treat a region of a file as if it were an array in memory.

Access file data directly in virtual memory — without explicit read/write calls.

Create views (windows into the file) that can be paged in/out.

So you can efficiently access:

using var mmf = MemoryMappedFile.CreateFromFile("huge.log", FileMode.Open);
using var accessor = mmf.CreateViewAccessor(offset, length);
byte value = accessor.ReadByte(0);


Under the hood:

The OS handles paging, caching, and memory optimization.

You only load the parts of the file that are actually accessed.

🧱 AquaEdit Backend Design (with MMF)

Let’s sketch an architecture for the Core File Engine.

AquaEdit.Core
 ├── FileManager
 │     ├── Opens files using MemoryMappedFile
 │     ├── Creates sliding "windows" for portions of the file
 │     ├── Exposes async read/write APIs
 │
 ├── TextBuffer
 │     ├── Provides logical view of file lines/characters
 │     ├── Handles encoding translation (UTF-8, UTF-16, etc.)
 │     ├── Supports line indexing and position lookup
 │
 ├── ChangeManager
 │     ├── Tracks edits as patches (diffs)
 │     ├── Applies changes lazily (not immediately writing to file)
 │
 ├── SearchEngine
 │     ├── Incremental or async search within current window
 │     ├── Streams data in background
 │
 └── UndoRedoManager
       ├── Command pattern with diffs
       ├── Memory-efficient storage

🧠 MMF-Based Reading Strategy

You’ll typically create sliding windows into the file.
Example for a 10 GB log file:

Define a window size (say 16 MB).

When the user scrolls beyond the window, release the old view and create a new one at a new offset.

public class FileWindow : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _accessor;
    private long _offset;
    private readonly long _windowSize;

    public FileWindow(MemoryMappedFile mmf, long offset, long windowSize)
    {
        _mmf = mmf;
        _offset = offset;
        _windowSize = windowSize;
        _accessor = _mmf.CreateViewAccessor(_offset, _windowSize);
    }

    public byte ReadByte(long index)
    {
        return _accessor.ReadByte(index);
    }

    public void SlideTo(long newOffset)
    {
        _accessor.Dispose();
        _offset = newOffset;
        _accessor = _mmf.CreateViewAccessor(_offset, _windowSize);
    }

    public void Dispose() => _accessor.Dispose();
}

✅ Benefits

Memory footprint stays small.

Access is near-native speed.

The OS optimizes paging automatically.

⚠️ Challenges

You must manage:

Encoding (UTF-8 boundaries across windows)

Line splitting across window edges

Caching of line positions (for scrolling)

Thread safety (access from UI threads)

🧩 Line Indexing Layer

To display file content efficiently (e.g., line-by-line in a virtualized editor), you’ll need a line indexer that maps line numbers → file offsets.

Strategy:

On file open, scan file in background threads.

Build a line index table (offsets for each line start).

Store it in a lightweight structure (e.g., List<long> or memory-mapped index file).

public async Task BuildLineIndexAsync(CancellationToken token)
{
    using var stream = File.OpenRead(_path);
    long offset = 0;
    int b;
    while ((b = stream.ReadByte()) != -1)
    {
        if (b == '\n') _lineOffsets.Add(offset + 1);
        offset++;
        if (offset % 1_000_000 == 0) await Task.Yield(); // Keep UI responsive
    }
}


This allows:

Fast random line access (Goto line N).

Smooth virtual scrolling (render only visible lines).

🧰 Editing Model

You typically don’t modify the file directly through MMF while editing — instead, maintain an overlay of edits.

Layers:

Base file (read-only MMF)

Edit buffer / overlay (in-memory diff)

Composite view (UI merges base + overlay)

Then on save:

Stream through base + overlay to rebuild the final file.

This is similar to how editors like Sublime Text and VS Code manage giant files efficiently.

🧪 Example: Lazy Line Reader Using MMF
public IEnumerable<string> ReadLines(long startOffset, long length)
{
    using var accessor = _mmf.CreateViewAccessor(startOffset, length, MemoryMappedFileAccess.Read);
    var buffer = new byte[length];
    accessor.ReadArray(0, buffer, 0, buffer.Length);
    using var ms = new MemoryStream(buffer);
    using var reader = new StreamReader(ms, Encoding.UTF8);
    while (!reader.EndOfStream)
        yield return reader.ReadLine();
}


This could be integrated with:

Virtualized UI (e.g., Avalonia.VirtualizingStackPanel),

Asynchronous line loading,

Background indexing.

# 🔋 Performance Tips
Tip	Description
Use CreateFromFile with FileAccess.Read for stability	Avoids OS locks
Choose a window size that aligns with page boundaries (e.g. 4KB * N)	Improves paging efficiency
Maintain a small LRU cache of recent windows	Avoids frequent reallocation
Handle UTF-8 partial characters at window edges	Avoid splitting multibyte sequences
Index file in background	Don’t block UI
Use Span<byte> and Memory<byte> for high-performance text decoding	Reduces allocations
# 🧭 Summary
Concern	How MMF Helps	Notes
Large file support	OS handles paging	File virtually "in memory"
Random access	Direct seek + view	Fast scrolling
Memory footprint	Small, predictable	Only mapped region resident
Editing	Overlay system	Prevents direct mutation issues
Cross-platform	✅ Works in .NET 8 on Windows/macOS/Linux	Avalonia-compatible backend
# ✅ TL;DR
Use MemoryMappedFile as the foundation of AquaEdit’s File Engine, with:
- Sliding view windows
- Line indexing and caching
- Edit overlay layer
- Async background I/O

# Example Runtime Scenario: “Open and Search File”

User selects file via OpenCommand.

EditorViewModel → calls FileService.OpenFile(path).

FileManager loads file into MMF → builds LineIndexer asynchronously.

UI binds to TextBuffer → renders visible lines only.

User starts a search → SearchEngine.SearchAsync() streams file chunks.

Results flow back to UI → highlighted dynamically.

Plugin “WordCounter” subscribes to FileOpenedEvent → starts analysis in background.


# Reactive Design Principles
Principle	Description
Unidirectional data flow	Core → Reactive streams → ViewModels → Views
Declarative UI	Avalonia XAML + reactive bindings (no imperative UI updates)
Observable state	File content, cursor, search results exposed as IObservable<T>
Reactive commands	Editor actions (OpenFile, Search, Save) are ReactiveCommand<Unit, Unit>
Hot observables	Shared event streams for UI and plugins (MessageBus)
# Reactive Core Engine Components
Component	Reactive Pattern
TextBuffer	Exposes IObservable<TextChange> when edits occur
FileManager	Wraps async file operations with Task or IObservable results
SearchEngine	Emits SearchResult streams as results come in
UndoRedoManager	Emits state changes when stacks are updated
ConfigurationManager	Uses BehaviorSubject<EditorSettings> for live-updating preferences
EventBus	Built on ReactiveUI.MessageBus for decoupled communication

# Reactive Data Flow (Diagram Description)
 ┌─────────────────────┐
 │     Avalonia UI     │
 │ ReactiveUserControl │
 └──────────┬──────────┘
            │ Bindings + ReactiveCommands
            ▼
 ┌─────────────────────┐
 │    ViewModels       │
 │ (ReactiveObjects)   │
 └──────────┬──────────┘
            │ IObservable<T> Streams
            ▼
 ┌─────────────────────┐
 │     Core Engine     │
 │ (TextBuffer, MMF)   │
 └──────────┬──────────┘
            │ MessageBus Events / Observables
            ▼
 ┌─────────────────────┐
 │   Plugin Host       │
 │ (Reactive Plugins)  │
 └─────────────────────┘

Direction:
Data always flows downward (Core → VM → UI), while user actions are expressed as reactive commands upward (UI → VM → Core).

# ✅ Benefits
Feature	Description
Reactive updates	UI automatically updates when file data, search results, or settings change.
Async-friendly	Non-blocking operations, great for large files.
Plugin extensibility	Plugins can listen to the same observable event streams.
Cross-platform	Works on Windows, macOS, Linux with Avalonia.
Highly testable	ViewModels and Core are easily unit-tested using ReactiveTestScheduler.

High-Level Layering
AquaEdit.Core
│
├── FileSystem Layer
│     └── FileManager
│           ├── FileWindow
│           └── MemoryMappedFile (System)
│
├── Text Layer
│     ├── TextBuffer
│     ├── LineIndexer
│     └── EncodingManager
│
├── Editing Layer
│     ├── EditOverlay
│     ├── UndoRedoManager
│     └── Patch
│
├── Search Layer
│     └── SearchEngine
│
└── Common Utilities
      └── LRUCache<T>

🧩 Class Design Diagram (UML-style)
+----------------------------------------------------+
|                  FileManager                       |
+----------------------------------------------------+
| - _mmf : MemoryMappedFile                          |
| - _filePath : string                               |
| - _fileSize : long                                 |
| - _windowCache : LRUCache<long, FileWindow>        |
|----------------------------------------------------|
| + OpenFile(path: string) : void                    |
| + GetWindow(offset: long, size: long) : FileWindow |
| + Close() : void                                   |
+----------------------------------------------------+
                 ▲
                 │
                 │ 1
                 │
                 │ *
+----------------------------------------------+
|                FileWindow                    |
+----------------------------------------------+
| - _accessor : MemoryMappedViewAccessor       |
| - _offset : long                             |
| - _windowSize : long                         |
|----------------------------------------------|
| + ReadByte(index: long) : byte               |
| + ReadRange(offset: long, count: int) : Span<byte> |
| + SlideTo(newOffset: long) : void            |
| + Dispose() : void                           |
+----------------------------------------------+

+----------------------------------------------------+
|                  TextBuffer                        |
+----------------------------------------------------+
| - _fileManager : FileManager                       |
| - _lineIndexer : LineIndexer                       |
| - _editOverlay : EditOverlay                       |
| - _encoding : Encoding                             |
|----------------------------------------------------|
| + ReadLine(lineNumber: int) : string               |
| + GetVisibleLines(range: LineRange) : IEnumerable<string> |
| + ApplyEdit(edit: EditOperation) : void            |
| + GetLineOffset(lineNumber: int) : long            |
+----------------------------------------------------+

+-----------------------------------------------+
|                LineIndexer                    |
+-----------------------------------------------+
| - _lineOffsets : List<long>                   |
| - _fileManager : FileManager                  |
|-----------------------------------------------|
| + BuildIndexAsync() : Task                    |
| + GetLineOffset(lineNumber: int) : long       |
| + GetLineCount() : int                        |
+-----------------------------------------------+

+----------------------------------------------------+
|                  EditOverlay                       |
+----------------------------------------------------+
| - _patches : List<Patch>                           |
|----------------------------------------------------|
| + AddPatch(patch: Patch) : void                    |
| + GetEffectiveText(offset: long, length: int) : string |
| + Clear() : void                                   |
+----------------------------------------------------+

+-----------------------------------------------+
|                   Patch                       |
+-----------------------------------------------+
| - StartOffset : long                          |
| - OriginalLength : int                        |
| - NewText : string                            |
|-----------------------------------------------|
| + Apply(baseText: string) : string            |
+-----------------------------------------------+

+----------------------------------------------------+
|                UndoRedoManager                    |
+----------------------------------------------------+
| - _undoStack : Stack<Patch>                       |
| - _redoStack : Stack<Patch>                       |
|----------------------------------------------------|
| + Undo() : Patch?                                 |
| + Redo() : Patch?                                 |
| + Record(patch: Patch) : void                     |
+----------------------------------------------------+

+----------------------------------------------------+
|                  SearchEngine                     |
+----------------------------------------------------+
| - _textBuffer : TextBuffer                        |
|----------------------------------------------------|
| + SearchAsync(pattern: string) : Task<IEnumerable<SearchResult>> |
| + FindNext(fromOffset: long) : SearchResult?      |
+----------------------------------------------------+

+-----------------------------------------------+
|                   LRUCache<K,V>               |
+-----------------------------------------------+
| - _capacity : int                             |
| - _cache : Dictionary<K,LinkedListNode<V>>    |
| - _order : LinkedList<V>                      |
|-----------------------------------------------|
| + TryGet(key: K, out value: V) : bool         |
| + Add(key: K, value: V) : void                |
| + Remove(key: K) : void                       |
+-----------------------------------------------+

🧠 How It Works Together

FileManager

Opens file using MemoryMappedFile.CreateFromFile()

Serves FileWindow objects for small regions (sliding window)

Caches recently used windows (via LRUCache)

TextBuffer

Provides the logical view of the file (line-based)

Uses FileManager to read bytes from the appropriate window

Converts bytes → text via EncodingManager

Applies in-memory patches from EditOverlay to form the effective document view

LineIndexer

Asynchronously scans the file to build a map of line start offsets

Enables instant navigation to “line N”

EditOverlay

Tracks edits (insertions/deletions/replacements)

Doesn’t modify the file directly — applies changes virtually

UndoRedoManager

Keeps patch history

Reapplies or reverts changes through EditOverlay

SearchEngine

Scans TextBuffer efficiently

Supports async background search

LRUCache

Keeps only the most recently used memory windows resident

Prevents exhausting address space for very large files

🧩 Example Flow: Reading a Line

UI requests line 1,200,000.

TextBuffer → asks LineIndexer for file offset of that line.

FileManager → fetches the appropriate FileWindow (may be cached).

FileWindow → reads byte range covering the line.

TextBuffer → decodes bytes using the correct encoding.

EditOverlay → applies any edits overlapping that range.

The result is returned to the UI for rendering.

⚡ Example Flow: Editing

User types into the editor (insert "ABC" at offset 1,245,678).

EditOverlay.AddPatch(new Patch(...)) adds a new diff.

UndoRedoManager.Record(patch) pushes it to history.

The UI refreshes affected lines from TextBuffer (which merges base + overlay).

When saving, FileManager streams the base file + overlay patches into a new file (or in-place if safe).

🔮 Future Extensions

You can later add:

SyntaxHighlighter (async tokenization layer on top of TextBuffer)

BookmarkManager or AnnotationLayer

SearchIndex (for fast full-text search)

PluginHost for external analyzers

🧩 High-Level Architecture Overview
AquaEdit
│
├── AquaEdit.UI.Avalonia       (Presentation Layer)
│     ├── ViewModels            ← MVVM ViewModels
│     ├── Views                 ← Avalonia XAML Views
│     ├── Controls              ← Custom editor, status bar, etc.
│     ├── Services              ← UI services (dialogs, themes, etc.)
│     └── Themes                ← Fluent/Material styling
│
├── AquaEdit.Core               (Application Logic Layer)
│     ├── FileSystem            ← MemoryMappedFile handling
│     ├── TextEngine            ← TextBuffer, EditOverlay, UndoRedo
│     ├── Search                ← Async search layer
│     ├── Plugins               ← Plugin discovery, interfaces
│     ├── Configuration         ← User settings, recent files, etc.
│     └── Events                ← Event bus or mediator pattern
│
├── AquaEdit.Plugins            (Extensibility Layer)
│     ├── Interfaces            ← IPlugin, ICommand, IAnalyzer, etc.
│     ├── Host                  ← Loads plugins via reflection / MEF
│     └── Samples               ← Example: JSONHighlighter, WordCount
│
└── AquaEdit.Tests              (Unit + Integration Tests)
      ├── Core.Tests
      ├── UI.Tests
      └── Plugins.Tests

🧱 Layer Breakdown
1. Presentation Layer — AquaEdit.UI.Avalonia

Purpose: Display and interact with text data from the Core.

Tech: Avalonia 11, MVVM pattern.

Responsibilities:

User interactions (scrolling, typing, search UI)

Virtualized text rendering (via AvaloniaEdit or custom control)

Command binding (Save, Open, Find, etc.)

Theming (Fluent / Material)

Example structure:

UI/
 ├── Views/
 │     ├── MainWindow.axaml
 │     └── EditorView.axaml
 ├── ViewModels/
 │     ├── MainViewModel.cs
 │     └── EditorViewModel.cs
 └── Controls/
       └── TextEditorControl.cs


Dependencies:
→ References AquaEdit.Core for document logic
→ Uses DI to inject IFileService, ISearchService, etc.

2. Core Engine — AquaEdit.Core

Purpose: Provide performant text and file operations abstracted from the UI.

Key Components:

Component	Purpose
FileManager	Opens files via MemoryMappedFile, provides windowed access
TextBuffer	Logical line-based view of file
LineIndexer	Maps line numbers to file offsets
EditOverlay	Tracks edits (insert/delete/replace diffs)
UndoRedoManager	Command-based undo/redo stack
SearchEngine	Async text searching
PluginManager	Discovers and manages external plugins

Design principles:

Cross-platform (pure .NET)

No UI dependencies

Thread-safe, async I/O where possible

Observable events for the UI layer to subscribe to

3. Extensibility Layer — AquaEdit.Plugins

Goal: Allow the community to add features (syntax highlighting, analyzers, formatting tools, etc.)
without modifying the core codebase.

Key Interfaces:

public interface IPlugin
{
    string Name { get; }
    string Description { get; }
    void Initialize(IPluginHost host);
}

public interface IPluginHost
{
    ITextBuffer GetActiveBuffer();
    void RegisterCommand(string name, Action action);
}


Plugin Discovery:

Plugins are .dll assemblies in /Plugins folder.

Loaded via reflection or MEF at startup.

Metadata extracted via attributes or JSON manifest.

Example:

Plugins/
 ├── SyntaxHighlighterPlugin.dll
 ├── WordCounterPlugin.dll
 └── JsonFormatterPlugin.dll

4. Communication Pattern — Event / Mediator System

Use an event bus or mediator to decouple UI from core logic:

public class EventBus
{
    public void Publish<T>(T @event);
    public void Subscribe<T>(Action<T> handler);
}


Examples:

UI publishes FileOpenedEvent

Core subscribes to trigger LineIndexer build

Plugins subscribe to DocumentChangedEvent

5. Configuration and Persistence

AquaEdit.Core.Configuration handles:

Editor preferences (font, tab size)

Theme settings

Recent file list

Plugin enable/disable states
→ Serialized to JSON in %AppData%/AquaEdit/config.json

🔄 Data Flow (Simplified)
        ┌───────────────────────┐
        │      User Input       │
        │   (Avalonia UI)       │
        └──────────┬────────────┘
                   │ Commands / MVVM Binding
                   ▼
        ┌───────────────────────┐
        │    AquaEdit.Core      │
        │   (File + Text Engine)│
        ├───────────────────────┤
        │ FileManager (MMF)     │
        │ TextBuffer / Overlay  │
        │ SearchEngine          │
        └──────────┬────────────┘
                   │ Events / Callbacks
                   ▼
        ┌───────────────────────┐
        │    Plugin Host        │
        │  (Extensions, Tools)  │
        └───────────────────────┘

🧠 Example Runtime Scenario: “Open and Search File”

User selects file via OpenCommand.

EditorViewModel → calls FileService.OpenFile(path).

FileManager loads file into MMF → builds LineIndexer asynchronously.

UI binds to TextBuffer → renders visible lines only.

User starts a search → SearchEngine.SearchAsync() streams file chunks.

Results flow back to UI → highlighted dynamically.

Plugin “WordCounter” subscribes to FileOpenedEvent → starts analysis in background.

🧩 Future-Proofing

The design supports:

✅ Cross-platform UI (Avalonia)

✅ Extensible plugins

✅ Performance on large files

✅ Isolation of concerns

✅ Async background processing