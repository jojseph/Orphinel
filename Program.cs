var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<orphinel.LexorRuntime.LexorExecutionService>();
builder.Services.AddScoped<orphinel.LexorRuntime.InterpreterWebSocketHandler>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

app.UseHttpsRedirection();

app.UseAuthorization();

app.Map("/ws/interpreter", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var handler = context.RequestServices.GetRequiredService<orphinel.LexorRuntime.InterpreterWebSocketHandler>();
    await handler.HandleAsync(socket, context.RequestAborted);
});

app.MapControllers();

app.Run();
