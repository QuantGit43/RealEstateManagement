using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using RealEstate.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("RealEstateDb")
    ?? throw new InvalidOperationException("Не задано ConnectionStrings:RealEstateDb.");
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, message) = error switch
    {
        SqlException { Number: 50404 } e => (StatusCodes.Status404NotFound, e.Message),
        SqlException { Number: 50409 } e => (StatusCodes.Status409Conflict, e.Message),
        SqlException { Number: >= 50000 } e => (StatusCodes.Status400BadRequest, e.Message),
        SqlException { Number: 547 } => (StatusCodes.Status400BadRequest, "Порушено зв'язок або обмеження (FK/CHECK)."),
        SqlException { Number: 2601 or 2627 } => (StatusCodes.Status409Conflict, "Запис з таким унікальним значенням уже існує."),
        _ => (StatusCodes.Status500InternalServerError, "Внутрішня помилка сервера.")
    };
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new { error = message });
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "RealEstate API"));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
