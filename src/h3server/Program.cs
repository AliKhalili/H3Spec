using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace H3Server;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel((context, options) =>
        {
            options.ListenAnyIP(6001, listenOptions =>
            {
                listenOptions.UseConnectionLogging();
                listenOptions.Protocols = HttpProtocols.Http3;
                listenOptions.UseHttps();
                listenOptions.Use((context, next) =>
                {
                    var streamId = context.Features.Get<IStreamIdFeature>()?.StreamId;
                    Console.WriteLine("Stream ID: " + streamId ?? "null");
                    var tlsFeature = context.Features.Get<ITlsHandshakeFeature>()!;
                    Console.WriteLine("Cipher: " + tlsFeature.CipherAlgorithm);
                    return next();
                });
            });
        });

        // Add services to the container.

        var app = builder.Build();

        // Configure the HTTP request pipeline.

        app.UseHttpsRedirection();

        app.MapGet("/", () =>
        {
            Console.WriteLine("GET REQUEST RECEIVED!");
            return Results.Ok("Hello World!" + $";Date: {DateTime.UtcNow}");
        });

        app.MapPost("/", async (context) =>
        {
            var data = await context.Request.ReadFormAsync();
            Console.WriteLine("POST REQUEST RECEIVED!");
            context.Response.ContentType = "text/plain";
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync(data.Select(i => $"({i.Key}:{i.Value})").Aggregate((c, n) => $"{c}, {n}") + $";Date: {DateTime.UtcNow}");
        });

        app.Run();
    }
}
