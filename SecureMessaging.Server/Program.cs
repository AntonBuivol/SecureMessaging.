using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SecureMessaging.Server.Hubs;
using SecureMessaging.Server.Models;
using SecureMessaging.Server.Services;
using Supabase;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Добавьте CORS перед другими сервисами
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder => builder
        .WithOrigins(
            "https://7342-145-14-21-133.ngrok-free.app"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowedToAllowWildcardSubdomains());
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Configure Supabase
var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:Key"];

var options = new SupabaseOptions
{
    AutoConnectRealtime = true
};
var supabase = new Client(supabaseUrl, supabaseKey, options);

// Initialize the connection
await supabase.InitializeAsync();

builder.Services.AddSingleton(supabase);
builder.Services.AddSingleton(provider => new Client(supabaseUrl, supabaseKey, options));

// Add services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<ChatService>();

// In Program.cs, update the JWT configuration:
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/authHub")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

// Порядок middleware критически важен!
app.UseHttpsRedirection();
app.UseCors("CorsPolicy"); // После UseHttpsRedirection, перед UseAuthentication
app.UseAuthentication();
app.UseAuthorization();

// Настройка SignalR с явным указанием транспорта
app.MapHub<ChatHub>("/chatHub", options =>
{
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
});

app.MapHub<AuthHub>("/authHub", options =>
{
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
});

app.MapGet("/test-supabase", async (Client supabase) =>
{
    try
    {
        var result = await supabase.From<Device>().Get();
        return Results.Ok(new
        {
            Success = result.ResponseMessage.IsSuccessStatusCode,
            Count = result.Models.Count
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
});

app.Run();