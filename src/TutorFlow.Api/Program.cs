using TutorFlow.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTutorFlowCore(builder.Configuration);

var app = builder.Build();
app.Run();

public partial class Program
{
}
