namespace Finora.Application.Features.Transactions;

public interface ITransactionImportService
{
    Task<ParsedFileDto> ParseFileAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}

