// An engine that gives way (IGivesWay, Microsoft Office): when it fails on a file, the next best route converts it.

using TsuFiles.Core.Conversion;
using TsuFiles.Core.Presets;

namespace TsuFiles.Core.Tests;

public class GivesWayTests
{
    /// <summary>Fails like Office on a workbook it cannot print in time, and gives way.</summary>
    private sealed class Office(params ConversionEdge[] edges) : IConverter, IGivesWay
    {
        public int Calls;
        public string Id => "msoffice";
        public string DisplayName => "Microsoft Office";
        public IReadOnlyList<ConversionEdge> Edges => edges;
        public int MaxParallelism => 1;
        public EngineStatus GetStatus() => EngineStatus.Available();

        public Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            throw new TimeoutException("Microsoft Office did not finish within 5 minutes.");
        }
    }

    [Fact]
    public async Task A_file_office_fails_on_is_converted_by_the_next_best_route()
    {
        using var dir = new TempDir();
        var xlsx = dir.File("big.xlsx");
        var office = new Office(new ConversionEdge("xlsx", "pdf", 0));
        var libreOffice = new FakeConverter("libreoffice", new ConversionEdge("xlsx", "pdf", 10));
        await using var queue = new JobQueue(new ConverterCatalog([office, libreOffice]));

        var job = new ConversionJob([xlsx], new Preset { TargetFormat = "pdf", Output = { Location = OutputLocation.SameFolder } }, "PDF");
        await queue.RunAsync(job);

        Assert.Equal(1, office.Calls);
        Assert.Equal(JobState.Completed, job.State);
        Assert.Single(job.Outputs);
    }

    [Fact]
    public async Task Without_another_route_the_file_fails_for_the_engines_reason()
    {
        using var dir = new TempDir();
        var xlsx = dir.File("big.xlsx");
        await using var queue = new JobQueue(new ConverterCatalog([new Office(new ConversionEdge("xlsx", "pdf", 0))]));

        var job = new ConversionJob([xlsx], new Preset { TargetFormat = "pdf", Output = { Location = OutputLocation.SameFolder } }, "PDF");
        await queue.RunAsync(job);

        var file = Assert.Single(job.Files);
        Assert.Equal(FileState.Failed, file.State);
        Assert.Equal("error.conversion_failed", file.ErrorKey);
        Assert.Contains("did not finish", file.ErrorDetail);
    }
}
