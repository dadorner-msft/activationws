using System.Buffers;
using System.Globalization;
using System.Text;
using ActivationWs.Activation;
using ActivationWs.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ActivationWs.Api;

public static partial class ActivationEndpoints {
    /// <summary>Default page size used by the reporting endpoint when none is supplied.</summary>
    private const int DefaultPageSize = 20;

    /// <summary>Upper bound for the page size accepted by the reporting endpoint.</summary>
    private const int MaxPageSize = 100;

    private const string CsvContentType = "text/csv";

    /// <summary>Header row of the activation records CSV export.</summary>
    private const string CsvHeader =
        "Host,InstallationId,ExtendedProductId,ConfirmationId,RequestCount,CreatedAtUtc,LastRequestedAtUtc";

    /// <summary>Characters that force a CSV field to be quoted (RFC 4180).</summary>
    private static readonly SearchValues<char> CsvSpecialCharacters = SearchValues.Create(",\"\r\n");

    /// <summary>
    /// Leading characters a spreadsheet may interpret as the start of a formula. Values beginning with
    /// one of these are prefixed so tools such as Excel treat them as text (CSV injection defense).
    /// </summary>
    private static readonly SearchValues<char> CsvFormulaLeadingCharacters = SearchValues.Create("=+-@\t\r");

    public static RouteGroupBuilder MapActivationEndpoints(this IEndpointRouteBuilder builder) {
        RouteGroupBuilder group = builder
            .MapGroup("/api/v1/activationservice")
            .WithTags("Activation");

        group.MapPost("/confirmation-id", GetConfirmationIdAsync)
            .WithName("GetConfirmationId")
            .WithSummary("Requests a Confirmation ID (CID).")
            .WithDescription(
                "Submits the Installation ID and Extended Product ID to the Microsoft Activation Service " +
                "and returns the corresponding Confirmation ID.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        group.MapPost("/count", GetActivationCountAsync)
            .WithName("GetActivationCount")
            .WithSummary("Requests the remaining activation count.")
            .WithDescription(
                "Submits the Extended Product ID to the Microsoft Activation Service and returns " +
                "the number of available activations.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        group.MapGet("/records", GetActivationRecordsAsync)
            .WithName("GetActivationRecords")
            .WithSummary("Lists stored activation records.")
            .WithDescription(
                "Returns a page of stored activations, most recently requested first, optionally filtered by host name.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/records.csv", ExportActivationRecordsAsync)
            .WithName("ExportActivationRecords")
            .WithSummary("Exports stored activation records as CSV.")
            .WithDescription(
                "Streams every stored activation, optionally filtered by host name, as a CSV download.")
            .Produces(StatusCodes.Status200OK, contentType: CsvContentType)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<Results<Ok<ActivationRecordPageResponse>, ProblemHttpResult>> GetActivationRecordsAsync(
        IActivationStore store,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        string? host = null,
        int page = 1,
        int pageSize = DefaultPageSize) {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        ActivationRecordPage result;
        try {
            result = await store.QueryAsync(host, page, pageSize, cancellationToken);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            // The store is a best-effort cache. Surface a distinct 503 so the reporting page can tell
            // the user the database is unavailable rather than showing a generic error.
            loggerFactory.CreateLogger("ActivationWs.Api.Reporting")
                .LogError(ex, "Failed to read activation records from the store.");

            return TypedResults.Problem(
                title: "Database unavailable",
                detail: "The activation database is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        int totalPages = result.TotalCount == 0
            ? 1
            : (int)Math.Ceiling(result.TotalCount / (double)pageSize);

        IReadOnlyList<ActivationRecordResponse> items = result.Items.Select(ToResponse).ToList();

        return TypedResults.Ok(
            new ActivationRecordPageResponse(items, page, pageSize, result.TotalCount, totalPages));
    }

    private static async Task ExportActivationRecordsAsync(
        HttpContext httpContext,
        IActivationStore store,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        string? host = null) {
        // Open the store before the download headers are committed. The most common failure here is
        // the IIS app pool identity lacking read access to the database file: probing the first record
        // up front lets that surface as a meaningful problem response instead of a corrupt download.
        IAsyncEnumerator<StoredActivation> records =
            store.StreamAsync(host, cancellationToken).GetAsyncEnumerator(cancellationToken);
        bool hasRecord;
        try {
            hasRecord = await records.MoveNextAsync().ConfigureAwait(false);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            await records.DisposeAsync().ConfigureAwait(false);

            loggerFactory.CreateLogger("ActivationWs.Api.Reporting")
                .LogError(ex, "Failed to read activation records from the store for CSV export.");

            await TypedResults.Problem(
                title: "Database unavailable",
                detail: "The activation database is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable)
                .ExecuteAsync(httpContext);
            return;
        }

        try {
            string fileName = $"activation-records-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.csv";

            httpContext.Response.ContentType = $"{CsvContentType}; charset=utf-8";
            httpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

            // The byte order mark keeps the file readable when opened in Excel.
            await using var writer = new StreamWriter(
                httpContext.Response.Body,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)) {
                NewLine = "\r\n",
            };

            await writer.WriteLineAsync(CsvHeader.AsMemory(), cancellationToken);

            while (hasRecord) {
                StoredActivation record = records.Current;
                var line = new StringBuilder()
                    .Append(CsvField(record.HostName)).Append(',')
                    .Append(CsvField(record.InstallationId)).Append(',')
                    .Append(CsvField(record.ExtendedProductId)).Append(',')
                    .Append(CsvField(record.ConfirmationId)).Append(',')
                    .Append(record.RequestCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(CsvField(record.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))).Append(',')
                    .Append(CsvField(record.LastRequestedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

                await writer.WriteLineAsync(line, cancellationToken);

                hasRecord = await records.MoveNextAsync().ConfigureAwait(false);
            }
        } finally {
            await records.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static ActivationRecordResponse ToResponse(StoredActivation record) => new(
        record.InstallationId,
        record.HostName,
        record.ExtendedProductId,
        record.ConfirmationId,
        record.CreatedAtUtc,
        record.LastRequestedAtUtc,
        record.RequestCount);

    /// <summary>Escapes a value for CSV (RFC 4180): quote when it contains a comma, quote or newline.</summary>
    private static string CsvField(string value) {
        // Neutralize values a spreadsheet might evaluate as a formula by prefixing an apostrophe.
        if (value.Length > 0 && CsvFormulaLeadingCharacters.Contains(value[0])) {
            value = "'" + value;
        }

        if (value.AsSpan().IndexOfAny(CsvSpecialCharacters) < 0) {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static async Task<Ok<ConfirmationIdResponse>> GetConfirmationIdAsync(
        [FromBody] ConfirmationIdRequest request,
        IConfirmationIdService confirmationIdService,
        CancellationToken cancellationToken) {
        ConfirmationIdResult result = await confirmationIdService.GetOrCreateAsync(
            request.InstallationId,
            request.ExtendedProductId.Trim(),
            request.HostName.Trim(),
            cancellationToken);

        return TypedResults.Ok(new ConfirmationIdResponse(result.ConfirmationId, result.FromCache));
    }

    private static async Task<Ok<ActivationCountResponse>> GetActivationCountAsync(
        [FromBody] ActivationCountRequest request,
        IActivationServiceClient client,
        CancellationToken cancellationToken) {
        int remaining = await client.GetRemainingActivationsAsync(
            request.ExtendedProductId.Trim(),
            cancellationToken);

        return TypedResults.Ok(new ActivationCountResponse(remaining));
    }
}
