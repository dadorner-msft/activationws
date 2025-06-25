using ActivationWs.Data;
using ActivationWs.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var databasePath = Path.Combine(AppContext.BaseDirectory, "ActivationWs.db");
if (!File.Exists(databasePath)) {
    // DB does not exist.
}


builder.Services.AddScoped<ActivationProcessor>();

builder.Services.AddDbContext<ActivationDbContext>(options =>
 //options.UseSqlite($"Data Source={databasePath}"));
 options.UseSqlite("Data Source=ActivationWs.db"));


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.MapRazorPages();

app.Run();
