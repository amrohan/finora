using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Finora.Application.Features.Accounts;
using Finora.Application.Features.Identity;
using Finora.Application.Features.Transactions;
using Finora.Domain.Enums;
using Xunit;

namespace Finora.Integration.Tests;

public class TransactionImportTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public TransactionImportTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string token, Guid householdId)> RegisterAdminAsync(string email)
    {
        var req = new RegisterInitialAdminRequest(
            "Import Admin", email, "ComplexPass123!", "Import Household",
            "dev_initial_setup_token_finora_2026");

        var resp = await _client.PostAsJsonAsync("/api/v1/auth/register-initial", req);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;
        var householdId = Guid.Parse(body.GetProperty("activeHouseholdId").GetString()!);
        return (token, householdId);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token, Guid householdId)
    {
        var msg = new HttpRequestMessage(method, url);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        msg.Headers.Add("X-Household-Id", householdId.ToString());
        return msg;
    }

    [Fact]
    public async Task ParseImportFile_ValidCsv_ReturnsColumnsAndRows()
    {
        var (token, householdId) = await RegisterAdminAsync($"csv_parse_{Guid.NewGuid()}@test.com");

        var csvContent = "Date,Narration,Withdrawal,Deposit,Balance\n" +
                         "2026-10-01,\"Swiggy Order\",450.00,,49550.00\n" +
                         "2026-10-02,\"Salary Credit\",,85000.00,134550.00\n";

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "statement.csv");

        var msg = Authorized(HttpMethod.Post, "/api/v1/transactions/import/parse", token, householdId);
        msg.Content = content;

        var resp = await _client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = await resp.Content.ReadFromJsonAsync<ParsedFileDto>(JsonOpts);
        Assert.NotNull(result);
        Assert.Equal("statement.csv", result.FileName);
        Assert.Equal(2, result.TotalRows);
        Assert.Contains("Date", result.Columns);
        Assert.Contains("Narration", result.Columns);
        Assert.Contains("Withdrawal", result.Columns);
        Assert.Contains("Deposit", result.Columns);
        Assert.Equal("Swiggy Order", result.AllRows[0]["Narration"]);
    }

    [Fact]
    public async Task ParseImportFile_ValidExcel_ReturnsColumnsAndRows()
    {
        var (token, householdId) = await RegisterAdminAsync($"xlsx_parse_{Guid.NewGuid()}@test.com");

        // Generate in-memory valid XLSX zip file
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            // [Content_Types].xml
            var ct = archive.CreateEntry("[Content_Types].xml");
            using (var s = ct.Open())
            {
                var xct = new XDocument(
                    new XElement(XName.Get("Types", "http://schemas.openxmlformats.org/package/2006/content-types"),
                        new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"),
                            new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                        new XElement(XName.Get("Override", "http://schemas.openxmlformats.org/package/2006/content-types"),
                            new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                            new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                        new XElement(XName.Get("Override", "http://schemas.openxmlformats.org/package/2006/content-types"),
                            new XAttribute("PartName", "/xl/sharedStrings.xml"),
                            new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"))
                    )
                );
                xct.Save(s);
            }

            // xl/sharedStrings.xml
            var sst = archive.CreateEntry("xl/sharedStrings.xml");
            using (var s = sst.Open())
            {
                var ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                var strings = new[] { "Date", "Description", "Amount", "2026-10-15", "Amazon India", "Uber Ride" };
                var xsst = new XDocument(
                    new XElement(XName.Get("sst", ns),
                        strings.Select(str => new XElement(XName.Get("si", ns), new XElement(XName.Get("t", ns), str))))
                );
                xsst.Save(s);
            }

            // xl/worksheets/sheet1.xml
            var ws = archive.CreateEntry("xl/worksheets/sheet1.xml");
            using (var s = ws.Open())
            {
                var ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                var xws = new XDocument(
                    new XElement(XName.Get("worksheet", ns),
                        new XElement(XName.Get("sheetData", ns),
                            // Row 1: Headers (Date, Description, Amount)
                            new XElement(XName.Get("row", ns), new XAttribute("r", "1"),
                                new XElement(XName.Get("c", ns), new XAttribute("r", "A1"), new XAttribute("t", "s"), new XElement(XName.Get("v", ns), "0")),
                                new XElement(XName.Get("c", ns), new XAttribute("r", "B1"), new XAttribute("t", "s"), new XElement(XName.Get("v", ns), "1")),
                                new XElement(XName.Get("c", ns), new XAttribute("r", "C1"), new XAttribute("t", "s"), new XElement(XName.Get("v", ns), "2"))
                            ),
                            // Row 2: 2026-10-15, Amazon India, 1299
                            new XElement(XName.Get("row", ns), new XAttribute("r", "2"),
                                new XElement(XName.Get("c", ns), new XAttribute("r", "A2"), new XAttribute("t", "s"), new XElement(XName.Get("v", ns), "3")),
                                new XElement(XName.Get("c", ns), new XAttribute("r", "B2"), new XAttribute("t", "s"), new XElement(XName.Get("v", ns), "4")),
                                new XElement(XName.Get("c", ns), new XAttribute("r", "C2"), new XElement(XName.Get("v", ns), "1299"))
                            )
                        )
                    )
                );
                xws.Save(s);
            }
        }

        var xlsxBytes = ms.ToArray();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(xlsxBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "bank_statement.xlsx");

        var msg = Authorized(HttpMethod.Post, "/api/v1/transactions/import/parse", token, householdId);
        msg.Content = content;

        var resp = await _client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = await resp.Content.ReadFromJsonAsync<ParsedFileDto>(JsonOpts);
        Assert.NotNull(result);
        Assert.Equal("bank_statement.xlsx", result.FileName);
        Assert.Equal(1, result.TotalRows);
        Assert.Equal("Amazon India", result.AllRows[0]["Description"]);
        Assert.Equal("1299", result.AllRows[0]["Amount"]);
    }

    [Fact]
    public async Task ExecuteImport_ValidTransactions_CreatesRecordsAndUpdatesBalance()
    {
        var (token, householdId) = await RegisterAdminAsync($"exec_import_{Guid.NewGuid()}@test.com");

        // 1. Create Account with 10,000 INR (1,000,000 paise)
        var createAccReq = new CreateAccountRequest(
            "HDFC Salary Account", AccountType.Savings, 1_000_000, "INR", "••••1234", VisibilityLevel.Household);
        var accMsg = Authorized(HttpMethod.Post, "/api/v1/accounts", token, householdId);
        accMsg.Content = JsonContent.Create(createAccReq);
        var accResp = await _client.SendAsync(accMsg);
        accResp.EnsureSuccessStatusCode();
        var acc = await accResp.Content.ReadFromJsonAsync<AccountDto>(JsonOpts);
        Assert.NotNull(acc);

        // 2. Import 3 items:
        // - Expense: 200 INR (20,000 paise)
        // - Expense: 500 INR (50,000 paise)
        // - Income: 1000 INR (100,000 paise)
        // Net balance change: -20,000 - 50,000 + 100,000 = +30,000 paise -> Final: 1,030,000 paise
        var importItems = new List<ImportTransactionItemDto>
        {
            new(new DateOnly(2026, 10, 1), "Grocery Store", 20_000, TransactionType.Expense),
            new(new DateOnly(2026, 10, 2), "Restaurant Dinner", 50_000, TransactionType.Expense),
            new(new DateOnly(2026, 10, 3), "Freelance Bonus", 100_000, TransactionType.Income)
        };

        var execReq = new ExecuteImportRequest(acc.Id, importItems, SkipDuplicates: true);
        var execMsg = Authorized(HttpMethod.Post, "/api/v1/transactions/import/execute", token, householdId);
        execMsg.Content = JsonContent.Create(execReq);
        var execResp = await _client.SendAsync(execMsg);
        Assert.Equal(HttpStatusCode.OK, execResp.StatusCode);

        var execResult = await execResp.Content.ReadFromJsonAsync<ExecuteImportResponse>(JsonOpts);
        Assert.NotNull(execResult);
        Assert.Equal(3, execResult.ImportedCount);
        Assert.Equal(0, execResult.SkippedCount);
        Assert.Equal(1_030_000, execResult.NewAccountBalancePaise);

        // 3. Re-import the same transactions with SkipDuplicates=true -> all 3 should be skipped
        var dupMsg = Authorized(HttpMethod.Post, "/api/v1/transactions/import/execute", token, householdId);
        dupMsg.Content = JsonContent.Create(execReq);
        var dupResp = await _client.SendAsync(dupMsg);
        Assert.Equal(HttpStatusCode.OK, dupResp.StatusCode);

        var dupResult = await dupResp.Content.ReadFromJsonAsync<ExecuteImportResponse>(JsonOpts);
        Assert.NotNull(dupResult);
        Assert.Equal(0, dupResult.ImportedCount);
        Assert.Equal(3, dupResult.SkippedCount);
        Assert.Equal(1_030_000, dupResult.NewAccountBalancePaise);
    }
}

