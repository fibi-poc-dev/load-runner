using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS to allow LoadRunner to connect
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// JWT Token Generation Endpoint
app.MapPost("/jwt-server-si/token", ([FromBody] JwtRequest request) =>
{
    Console.WriteLine($"JWT Token Request received: {JsonSerializer.Serialize(request)}");
    
    // Generate a mock JWT token
    var jwt = GenerateMockJwt(request);
    
    var response = new JwtResponse { Jwt = jwt };
    
    Console.WriteLine($"Returning JWT: {jwt[..20]}...");
    return Results.Ok(response);
});

// OAuth Token Generation Endpoint
app.MapPost("/sso-portal/token", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    
    Console.WriteLine("OAuth Token Request received:");
    foreach (var field in form)
    {
        var value = field.Key == "password" ? "***REDACTED***" : field.Value.ToString();
        Console.WriteLine($"  {field.Key} = {value}");
    }
    
    // Validate required fields
    if (!form.ContainsKey("grant_type") || form["grant_type"] != "password")
    {
        return Results.BadRequest(new { error = "unsupported_grant_type" });
    }
    
    if (!form.ContainsKey("username") || !form.ContainsKey("password"))
    {
        return Results.BadRequest(new { error = "invalid_request" });
    }
    
    // Generate mock access token
    var accessToken = GenerateMockAccessToken(form["username"]!);
    
    var response = new TokenResponse
    {
        AccessToken = accessToken,
        TokenType = "Bearer",
        ExpiresIn = 3600,
        Scope = form["scope"]
    };
    
    Console.WriteLine($"Returning access token: {accessToken[..20]}...");
    return Results.Ok(response);
});

// Account Transactions Endpoint
app.MapGet("/bfb-ils-transactions-booked/api/v1/accountTransactions/{branchId}/{accountType}/{accountId}", 
    (string branchId, string accountType, string accountId, [FromQuery] string? startDate, [FromQuery] string? endDate, [FromQuery] int page = 0, [FromQuery] int rowsInPage = 200) =>
{
    Console.WriteLine($"Account Transactions Request: Branch={branchId}, Type={accountType}, Account={accountId}, Page={page}");
    
    var transactions = GenerateMockTransactions(accountId, page, rowsInPage);
    
    Console.WriteLine($"Returning {transactions.Transactions.Count} transactions");
    return Results.Ok(transactions);
});

// Account Type Endpoint  
app.MapGet("/bfb-ils-transactions-booked/api/v1/accountType/{branchId}/{accountId}",
    (string branchId, string accountId) =>
{
    Console.WriteLine($"Account Type Request: Branch={branchId}, Account={accountId}");
    
    var accountType = new AccountTypeResponse
    {
        BranchId = branchId,
        AccountId = accountId,
        AccountType = "CHECKING",
        Currency = "ILS",
        Status = "ACTIVE"
    };
    
    Console.WriteLine($"Returning account type: {accountType.AccountType}");
    return Results.Ok(accountType);
});

// Clean Redis Endpoint
app.MapDelete("/bfb-ils-transactions-booked/api/v1/cleanRedis", () =>
{
    Console.WriteLine("Redis Clean Request received");
    return Results.Ok(new { message = "Redis cache cleared successfully" });
});

// Token Introspection Endpoint
app.MapPost("/sso-portal/introspect", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var token = form["token"];
    
    Console.WriteLine($"Token Introspect Request for token: {token.ToString()[..20]}...");
    
    var response = new IntrospectResponse
    {
        Active = true,
        Scope = "ILS_Currency",
        ClientId = form["client_id"],
        Username = "I291",
        Exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
    };
    
    return Results.Ok(response);
});

Console.WriteLine("Mock Banking API starting on http://localhost:5001");
Console.WriteLine("Available endpoints:");
Console.WriteLine("  POST /jwt-server-si/token - JWT Token Generation");
Console.WriteLine("  POST /sso-portal/token - OAuth Token Generation");
Console.WriteLine("  GET  /bfb-ils-transactions-booked/api/v1/accountTransactions/{branchId}/{accountType}/{accountId} - Account Transactions");
Console.WriteLine("  GET  /bfb-ils-transactions-booked/api/v1/accountType/{branchId}/{accountId} - Account Type");
Console.WriteLine("  DELETE /bfb-ils-transactions-booked/api/v1/cleanRedis - Clean Redis");
Console.WriteLine("  POST /sso-portal/introspect - Token Introspection");

app.Run("http://localhost:5001");

// Helper methods and models
static string GenerateMockJwt(JwtRequest request)
{
    var payload = JsonSerializer.Serialize(new
    {
        iss = request.Iss,
        sub = request.Sub,
        aud = request.Aud,
        exp = DateTimeOffset.UtcNow.AddMinutes(request.Exp).ToUnixTimeSeconds(),
        userName = request.UserName,
        customerId = request.CustomerId,
        bankId = request.BankId,
        branchId = request.BranchId,
        accountId = request.AccountId,
        jwtIssuer = request.JwtIssuer,
        iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    });
    
    // Mock JWT format: header.payload.signature (base64 encoded)
    var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
    var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    var signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("mock-signature-" + Guid.NewGuid().ToString("N")[..8]));
    
    return $"{header}.{payloadBase64}.{signature}";
}

static string GenerateMockAccessToken(string username)
{
    var tokenData = $"{username}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{Guid.NewGuid():N}";
    return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData));
}

static TransactionsResponse GenerateMockTransactions(string accountId, int page, int rowsInPage)
{
    var transactions = new List<Transaction>();
    var random = new Random();
    
    for (int i = 0; i < Math.Min(rowsInPage, 10); i++)
    {
        transactions.Add(new Transaction
        {
            TransactionId = Guid.NewGuid().ToString(),
            Date = DateTime.UtcNow.AddDays(-random.Next(1, 30)).ToString("yyyy-MM-dd"),
            Amount = (random.NextDouble() * 1000 - 500).ToString("F2"),
            Currency = "ILS",
            Description = $"Transaction {i + 1 + (page * rowsInPage)}",
            Reference = $"REF{random.Next(100000, 999999)}"
        });
    }
    
    return new TransactionsResponse
    {
        AccountId = accountId,
        Page = page,
        TotalPages = 5,
        Transactions = transactions
    };
}

// Data Models
public record JwtRequest(
    string Iss,
    string Sub, 
    string Aud,
    int Exp,
    string UserName,
    string CustomerId,
    int BankId,
    int BranchId,
    int AccountId,
    string JwtIssuer
);

public record JwtResponse(string Jwt);

public record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string? Scope
);

public record AccountTypeResponse(
    string BranchId,
    string AccountId,
    string AccountType,
    string Currency,
    string Status
);

public record IntrospectResponse(
    bool Active,
    string? Scope,
    string? ClientId,
    string? Username,
    long Exp
);

public record TransactionsResponse(
    string AccountId,
    int Page,
    int TotalPages,
    List<Transaction> Transactions
);

public record Transaction(
    string TransactionId,
    string Date,
    string Amount,
    string Currency,
    string Description,
    string Reference
);
