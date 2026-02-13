using FinanceManager.Api.Services;
using FinanceManager.Application;
using FinanceManager.Application.Options;
using FinanceManager.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using ServiceDefaults;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();


builder.Services
    .AddSingleton(typeof(IOptionsSnapshot<>), typeof(OptionsManager<>))
    .AddSingleton(typeof(IOptionsFactory<>), typeof(OptionsFactory<>))
    .AddOpenApi("v1", options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    })
    .AddDatabase(builder.Configuration)
    .AddApplicationApi()
    .AddInfrastructureApi()
    .AddControllers();


builder.Services.Configure<JwtAuthOptions>(builder.Configuration.GetSection("JwtConfig"));
builder.Services.Configure<StockApiOptions>(builder.Configuration.GetSection("StockApi"));


builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCorsPolicy",
        builder =>
        {
            builder.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
}).AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["JwtConfig:Issuer"],
        ValidAudience = builder.Configuration["JwtConfig:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtConfig:Key"]!)),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
    };
});

builder.Services.AddMemoryCache();
builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtTokenGenerator>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.AddHttpAuthentication("Bearer", bearer =>
        {
            bearer.Token = "your-bearer-token";
        });
    });
}

app.UseHttpsRedirection();

app.UseCors("ApiCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();