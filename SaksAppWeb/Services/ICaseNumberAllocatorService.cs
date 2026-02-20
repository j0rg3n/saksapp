namespace SaksAppWeb.Services;

public interface ICaseNumberAllocator
{
    Task<int> AllocateNextAsync(CancellationToken ct = default);
}
