using ProductApi.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ProductStore>();
builder.Services.AddControllers();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();
app.Run();
