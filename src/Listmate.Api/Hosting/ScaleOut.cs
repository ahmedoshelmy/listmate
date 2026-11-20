using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Listmate.Api.Hosting;

/// <summary>
/// Running more than one API replica needs two shared things: SignalR group messages (a hub on replica A must reach
/// clients connected to replica B) and the Data Protection key ring (a bearer token issued by A must be readable by B).
/// Both go to Redis when ConnectionStrings:Redis is set; a single instance needs neither.
/// </summary>
internal static class ScaleOut
{
    public static WebApplicationBuilder AddScaleOut(this WebApplicationBuilder builder, ISignalRServerBuilder signalR)
    {
        var redis = builder.Configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redis))
        {
            return builder;
        }

        var multiplexer = ConnectionMultiplexer.Connect(redis);
        builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        signalR.AddStackExchangeRedis(o =>
        {
            o.Configuration.ChannelPrefix = RedisChannel.Literal("listmate");
            o.ConnectionFactory = _ => Task.FromResult<IConnectionMultiplexer>(multiplexer);
        });
        builder.Services.AddDataProtection().SetApplicationName("listmate").PersistKeysToStackExchangeRedis(multiplexer, "listmate:data-protection-keys");
        return builder;
    }
}
