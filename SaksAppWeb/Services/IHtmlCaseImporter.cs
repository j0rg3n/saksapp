namespace SaksAppWeb.Services;

public interface IHtmlCaseImporter
{
    Task<ImportResult> ImportAsync(string html, CancellationToken ct = default);
}
