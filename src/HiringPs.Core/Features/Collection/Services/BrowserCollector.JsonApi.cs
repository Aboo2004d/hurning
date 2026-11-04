namespace HiringPs.Core;

public partial class BrowserCollector
{
    private Task<List<(string Id, string Url)>> DiscoverApiAsync(Source source, CancellationToken ct) =>
        CollectorDiagnosticFiles.WithoutPageAsync(source, "Discovery", "ReadApi", source.Url, configuration,
            async () =>
            {
                var conf = JsonApiReader.Configuration(source);
                var api = conf.Api!;
                var reader = ApiReader;
                var completed = discovery?.Checkpoint.CompletedPages ?? 0;
                if (completed > 0 && discovery?.Checkpoint.NextUrl != JsonApiReader.PageCursor(api, completed + 1))
                    throw new InvalidOperationException("API checkpoint does not match its page template.");
                var result = new Dictionary<string, string>();
                for (var page = completed + 1; page <= conf.MaxPages; page++)
                {
                    ct.ThrowIfCancellationRequested();
                    var batch = await reader.ReadPageAsync(source, page, ct);
                    foreach (var item in batch.Items) result[item.Id] = item.Url;
                    await SaveDiscoveryPageAsync(new(batch.Items,
                        batch.HasNext ? JsonApiReader.PageCursor(api, page + 1) : null, page), ct);
                    if (!batch.HasNext) return result.Select(x => (x.Key, x.Value)).ToList();
                }
                throw new InvalidOperationException("API pagination limit reached; source needs review. Increase MaxPages before resuming.");
            }, ct);
}
