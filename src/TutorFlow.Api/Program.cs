using TutorFlow.Api;
using TutorFlow.Api.Http;
using TutorFlow.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddTutorFlowCore(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapTutorFlow();
app.Run();

public partial class Program
{
}
