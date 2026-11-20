using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using RogueGameServer.Data;
using RogueGameServer.Services;

var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("MySql")
                          ?? throw new InvalidOperationException("缺少连接串 ConnectionStrings:MySql");

// ServerVersion 写死为本机 8.0.41，避免 AutoDetect 在数据库未启动时就把服务拖垮
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.Create(new Version(8, 0, 41), ServerType.MySql))
           // SELECT ... FOR UPDATE 的行锁查询必须 FirstOrDefault，EF 在没有 OrderBy 时会告警；
           // 这里是主键等值匹配，结果本来就唯一，明确忽略该告警
           .ConfigureWarnings(w => w.Ignore(CoreEventId.FirstWithoutOrderByAndFilterWarning)));

builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<LoginThrottle>();

string jwtKey = builder.Configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("缺少 Jwt:Key 配置");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// Unity 用 http://localhost:5000 直连；将来如果要给 WebGL 用再收紧白名单
builder.Services.AddCors(options =>
    options.AddPolicy("unity", policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()));

// 契约统一 camelCase：Unity 侧只有 JsonUtility（字段名要与 JSON 完全一致），
// 固定命名策略后客户端字段名才是确定的
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// 让 /healthz 不经过鉴权，方便快速判断服务是否活着
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("unity");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();
