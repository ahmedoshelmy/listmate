using System.Net.Http.Headers;
using System.Net.Http.Json;
using Listmate.Api.Hubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Listmate.IntegrationTests;

/// <summary>The API in-process (TestServer) against a real Postgres in a container, migrated on startup like in compose.</summary>
public sealed class ListmateApp : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("listmate").Build();

    public WebApplicationFactory<ListsHub> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new ApiFactory(_postgres.GetConnectionString());
        _ = Factory.Server;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Registers a fresh account through the Identity endpoints and returns a client carrying its bearer token.</summary>
    public async Task<User> SignUpAsync(string name)
    {
        var email = $"{name}-{Guid.NewGuid():N}@example.com";
        var credentials = new { email, password = "Demo-pass-1" };
        var http = Factory.CreateClient();
        (await http.PostAsJsonAsync("/api/auth/register", credentials)).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("/api/auth/login", credentials);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new User(email, token, http);
    }

    public HubConnection Hub(User user) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, "/hubs/lists"), o =>
            {
                // TestServer has no WebSockets; long polling exercises the same hub and auth.
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(user.Token);
            })
            .Build();

    public async Task<long> ScalarAsync(string sql, Guid listId)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("list", listId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class ApiFactory(string connectionString) : WebApplicationFactory<ListsHub>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
        }
    }

    private sealed record LoginResponse(string AccessToken);
}

public sealed record User(string Email, string Token, HttpClient Http);

[CollectionDefinition(Name)]
public sealed class ListmateFixtureGroup : ICollectionFixture<ListmateApp>
{
    public const string Name = "listmate";
}
