using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NextAZ.Harness.Api.Configuration;
using NextAZ.Harness.Infrastructure.Persistence;
using NextAZ.Harness.Api.Security;
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddHarnessKeyVault();
builder.Logging.ClearProviders().AddJsonConsole();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 65536);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddHarnessSecurity(builder.Configuration, builder.Environment);
builder.Services.AddHarnessFoundation(builder.Configuration, builder.Environment);
builder.Services.AddEmployeeDrafts(builder.Configuration);
var app = builder.Build();
_ = app.Services.GetRequiredService<NextAZ.Harness.Runtime.Abstractions.IAgentRegistry>();
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = 500;
    await Results.Problem(statusCode: 500, title: "Harness request failed.").ExecuteAsync(context);
}));
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGet("/health/ready", async (HarnessRuntimeDescriptor runtime, IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    var ready = true;
    if (runtime.UsesPostgres)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HarnessDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        try
        {
            ready = await db.Database.CanConnectAsync(cancellationToken) &&
                !(await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
        }
        catch { ready = false; }
    }
    var body = new { status = ready ? "Healthy" : "Unhealthy", mode = runtime.Mode,
        persistence = runtime.Persistence, provider = runtime.Provider, messaging = runtime.Messaging };
    return ready ? Results.Ok(body) : Results.Json(body, statusCode: 503);
}).AllowAnonymous();
app.MapControllers();
app.Run();
public partial class Program { }
