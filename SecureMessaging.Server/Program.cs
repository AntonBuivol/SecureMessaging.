using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SecureMessaging.Server.Hubs;
using SecureMessaging.Server.Services;
using Supabase;

var builder = WebApplication.CreateBuilder(args);

// Добавьте CORS перед другими сервисами
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder => builder
        .WithOrigins(
            "https://f29f-145-14-21-133.ngrok-free.app"
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

builder.Services.AddSingleton(provider => new Client(supabaseUrl, supabaseKey, options));

// Add services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<ChatService>();

// Configure JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidateIssuer = false,
            ValidateAudience = false
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
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

app.Run();