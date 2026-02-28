using backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register DatabaseHelper as a scoped service.
// The connection string is resolved from IConfiguration:
//   - Locally: reads "ConnectionStrings:DefaultConnection" from appsettings.json
//   - Azure App Service: reads the environment variable ConnectionStrings__DefaultConnection
builder.Services.AddScoped<DatabaseHelper>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
