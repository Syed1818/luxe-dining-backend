using Microsoft.EntityFrameworkCore;
using QRMenuAPI.Data;
using QRMenuAPI.Hubs;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// 1. MONGODB ATLAS CONNECTION
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoConnection");
var mongoClient = new MongoClient(mongoConnectionString);
builder.Services.AddDbContext<RestaurantContext>(options =>
    options.UseMongoDB(mongoClient, "LuxeDiningDb")
);

builder.Services.AddSignalR();

// 2. PRODUCTION CORS (Update "https://your-vercel-url.vercel.app" later!)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b => b.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll"); // Applied before controllers/hubs!
app.UseAuthorization();
app.MapControllers();
app.MapHub<OrderHub>("/orderHub");

app.Run();