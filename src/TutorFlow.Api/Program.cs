using TutorFlow.Api;
using TutorFlow.Api.Http;
using TutorFlow.Infrastructure.DependencyInjection;
using TutorFlow.Infrastructure.Retrieval;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddTutorFlowCore(builder.Configuration);
builder.Services.AddTutorFlowIa(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<AuditoriaMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapTutorFlow();
app.Run();

public partial class Program
{
}
