using DerivAI.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient("DerivAIAgent", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AgentSettings:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<IDerivAIAgentService, DerivAIAgentService>();
builder.Services.AddSingleton<ITradeDataService, TradeDataService>();
builder.Services.AddSingleton<IAuditTrailService, InMemoryAuditTrailService>();
builder.Services.AddSession(options => { options.IdleTimeout = TimeSpan.FromMinutes(30); });

var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=DerivAI}/{action=Index}/{id?}");
app.Run();
