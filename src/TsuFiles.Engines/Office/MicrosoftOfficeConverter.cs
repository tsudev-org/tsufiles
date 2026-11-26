// Microsoft Office (Windows, only where Word, Excel or PowerPoint is installed): the documents of Vietnamese offices are
// written in Office, so Office itself lays them out to PDF exactly as it prints them (fonts, page breaks, print areas,
// scaling to fit). It wins over LibreOffice and the built-in route for what it can do; without Office nothing changes.
//
// Decided by the project owner on 26/11/2026 (phiếu TG1): this reverses settings v5's "TsuFiles never uses software
// installed on the system" for Office only. Safety: macros are never run (AutomationSecurity = force disable), links
// to other files are not updated, a password-protected file fails at once instead of waiting on a hidden dialog, the
// file is opened read-only, and each conversion runs its own Office process on an STA thread, ended afterwards; a
// conversion that hangs is stopped after a timeout by ending only the Office processes it started. A file Office can't
// do (a workbook whose print area spans thousands of pages, a damaged file) goes on through the next best route
// (IGivesWay: LibreOffice or the built-in engines).

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TsuFiles.Core.Conversion;
using TsuFiles.Engines.Infrastructure;

namespace TsuFiles.Engines.Office;

/// <summary>Converts Word, Excel and PowerPoint files with the Microsoft Office installed on Windows.</summary>
public sealed class MicrosoftOfficeConverter : IConverter, IGivesWay
{
    private enum App { Word, Excel, PowerPoint }

    private static readonly (App App, string ProgId, string Process, string[] Sources, string[] Targets)[] Apps =
    [
        (App.Word, "Word.Application", "WINWORD", ["docx", "doc", "docm", "dotx", "dot", "rtf", "odt"], ["pdf", "docx"]),
        (App.Excel, "Excel.Application", "EXCEL", ["xlsx", "xls", "xlsm", "xlsb", "xltx", "ods"], ["pdf", "xlsx"]),
        (App.PowerPoint, "PowerPoint.Application", "POWERPNT", ["pptx", "ppt", "pptm", "ppsx", "pps", "odp"], ["pdf", "pptx"]),
    ];

    /// <summary>A password no real file has: a protected file then fails instead of asking for its password.</summary>
    private const string NoPassword = "\u0001tsufiles\u0001";

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private readonly Func<string, bool> _installed;

    public MicrosoftOfficeConverter() : this(IsRegistered)
    {
    }

    /// <param name="installed">Whether the COM server of a ProgID is registered (tests pretend).</param>
    internal MicrosoftOfficeConverter(Func<string, bool> installed)
    {
        _installed = installed;
        Edges = [.. Apps.Where(a => OperatingSystem.IsWindows() && _installed(a.ProgId))
            .SelectMany(a => a.Sources.SelectMany(s => a.Targets.Where(t => t != s).Select(t => new ConversionEdge(s, t, Cost))))];
    }

    /// <summary>Cheaper than every other route (LibreOffice 10-25, built-in about 31): Office lays out its own files.</summary>
    private const int Cost = 0;

    public string Id => "msoffice";
    public string DisplayName => "Microsoft Office";

    /// <summary>One Office process at a time: COM servers do not like company, and memory stays low.</summary>
    public int MaxParallelism => 1;

    public IReadOnlyList<ConversionEdge> Edges { get; }

    public EngineStatus GetStatus()
    {
        if (!OperatingSystem.IsWindows())
            return EngineStatus.Unavailable("engine.reason.unsupported_platform");
        var found = Apps.Where(a => _installed(a.ProgId)).Select(a => a.App.ToString()).ToList();
        return found.Count == 0
            ? EngineStatus.Unavailable("engine.reason.not_installed", "Microsoft Office (Word, Excel, PowerPoint)")
            : EngineStatus.Available(string.Join(", ", found) + " → PDF");
    }

    private static bool IsRegistered(string progId) => OperatingSystem.IsWindows() && Type.GetTypeFromProgID(progId) is not null;

    public async Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Microsoft Office runs on Windows only.");
        var app = Apps.First(a => a.Sources.Contains(step.From) && a.Targets.Contains(step.To));
        var output = step.Output.Allocate(step.To);
        if (output is null)
            return [];
        progress?.Report(0.1);
        var input = Path.GetFullPath(step.InputPath);
        var pdfA = step.Preset.Document.PdfA;
        // Office writes into a folder of its own first: Excel leaves mso*.tmp files beside what it exports.
        var scratch = Path.Combine(step.WorkDirectory, "office-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(scratch);
        var written = Path.Combine(scratch, "out." + step.To);
        try
        {
#pragma warning disable CA1416 // guarded by the OperatingSystem.IsWindows() check above; the analyzer does not see into lambdas
            using (ProgressEstimate.Start(progress, TimeSpan.FromSeconds(8)))
                await RunAsync(app.Process, () => Convert(app.App, app.ProgId, input, written, step.To, pdfA), cancellationToken);
#pragma warning restore CA1416
            File.Move(written, output, overwrite: true);
        }
        finally
        {
            try
            {
                Directory.Delete(scratch, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file Office still holds goes with the work directory.
            }
        }
        progress?.Report(1);
        return [output];
    }

    /// <summary>
    /// Runs <paramref name="work"/> on an STA thread; on a timeout, a cancel or an error, ends the Office processes
    /// that appeared while it ran (and only those: a Word the user opens meanwhile is a process that existed or appears
    /// later, see <see cref="Started"/>).
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static async Task RunAsync(string process, Action work, CancellationToken cancellationToken)
    {
        var before = Ids(process);
        var started = new HashSet<int>();
        var task = Sta(() =>
        {
            work();
        }, () => started.UnionWith(Ids(process).Except(before)), cancellationToken);
        try
        {
            var finished = await Task.WhenAny(task, Task.Delay(Timeout, cancellationToken));
            if (finished != task)
            {
                End(started);
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Microsoft Office did not finish within {Timeout.TotalMinutes:0} minutes.");
            }
            await task;
        }
        catch
        {
            End(started);
            throw;
        }
        // Quit() ends the server: wait for it (the command line may exit right after), then end what is still ours.
        for (var waited = 0; waited < 40 && started.Any(Running); waited++)
            await Task.Delay(250, CancellationToken.None);
        End(started);
    }

    private static bool Running(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Runs on a new STA thread; <paramref name="started"/> is called right after the Office server was created (see
    /// <see cref="Started"/>), so the processes it notes are the ones this conversion started.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static Task Sta(Action work, Action started, CancellationToken cancellationToken)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Started.Value = started;
                work();
                done.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                done.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                done.TrySetException(ex);
            }
        })
        { IsBackground = true, Name = "TsuFiles Office worker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    /// <summary>The callback of the running conversion that notes the processes its Office server started.</summary>
    private static readonly ThreadLocal<Action?> Started = new();

    private static HashSet<int> Ids(string process)
    {
        var processes = Process.GetProcessesByName(process);
        var ids = processes.Select(p => p.Id).ToHashSet();
        foreach (var p in processes)
            p.Dispose();
        return ids;
    }

    private static void End(IEnumerable<int> ids)
    {
        foreach (var id in ids.ToList())
        {
            try
            {
                using var process = Process.GetProcessById(id);
                process.Kill();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // Already gone.
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Convert(App app, string progId, string input, string output, string to, bool pdfA)
    {
        var type = Type.GetTypeFromProgID(progId) ?? throw new InvalidOperationException($"{progId} is not installed.");
        dynamic? server = null;
        dynamic? file = null;
        try
        {
            server = Activator.CreateInstance(type)!;
            Started.Value?.Invoke();
            // msoAutomationSecurityForceDisable: no macro of the file ever runs.
            server.AutomationSecurity = 3;
            server.DisplayAlerts = app == App.PowerPoint ? 1 : (object)(app == App.Word ? 0 : false); // ppAlertsNone, wdAlertsNone, False
            if (app != App.PowerPoint)
                server.Visible = false;
            switch (app)
            {
                case App.Word:
                    file = server.Documents.Open(FileName: input, ConfirmConversions: false, ReadOnly: true, AddToRecentFiles: false,
                        PasswordDocument: NoPassword, Revert: false, Visible: false, NoEncodingDialog: true);
                    if (to == "pdf")
                    {
                        // wdExportFormatPDF, print quality, whole document, document with markup off, headings as bookmarks.
                        file.ExportAsFixedFormat(OutputFileName: output, ExportFormat: 17, OpenAfterExport: false, OptimizeFor: 0,
                            Range: 0, Item: 0, IncludeDocProps: true, KeepIRM: true, CreateBookmarks: 1, DocStructureTags: true,
                            BitmapMissingFonts: true, UseISO19005_1: pdfA);
                    }
                    else
                    {
                        file.SaveAs2(FileName: output, FileFormat: 16); // wdFormatDocumentDefault (DOCX)
                    }
                    break;
                case App.Excel:
                    file = server.Workbooks.Open(Filename: input, UpdateLinks: 0, ReadOnly: true, Password: NoPassword,
                        WriteResPassword: NoPassword, IgnoreReadOnlyRecommended: true, AddToMru: false, Notify: false);
                    if (to == "pdf")
                    {
                        // The whole workbook, each sheet with its own print area, page breaks and fit to page.
                        file.ExportAsFixedFormat(Type: 0, Filename: output, Quality: 0, IncludeDocProperties: true,
                            IgnorePrintAreas: false, OpenAfterPublish: false);
                    }
                    else
                    {
                        file.SaveAs(Filename: output, FileFormat: 51); // xlOpenXMLWorkbook
                    }
                    break;
                case App.PowerPoint:
                    // ReadOnly, not Untitled, no window.
                    file = server.Presentations.Open(FileName: input, ReadOnly: -1, Untitled: 0, WithWindow: 0);
                    file.SaveAs(FileName: output, FileFormat: to == "pdf" ? 32 : 24); // ppSaveAsPDF, ppSaveAsOpenXMLPresentation
                    break;
            }
            if (!File.Exists(output) || new FileInfo(output).Length == 0)
                throw new InvalidOperationException($"Microsoft {app} wrote nothing for this file.");
        }
        finally
        {
            try
            {
                if (file is not null)
                {
                    if (app == App.PowerPoint)
                        file.Close();
                    else
                        file.Close(false);
                }
            }
            catch (COMException)
            {
            }
            try
            {
                server?.Quit();
            }
            catch (COMException)
            {
            }
            if (file is not null)
                Marshal.FinalReleaseComObject(file);
            if (server is not null)
                Marshal.FinalReleaseComObject(server);
        }
    }
}
