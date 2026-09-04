var builder = WebApplication.CreateBuilder(args);


// =========================================================
// SERVICES
// =========================================================

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<PaddyMISWeb.Data.DatabaseHelper>();


// =========================================================
// LISTEN ON LAN
// =========================================================

builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP - accessible from computer and phone
    options.ListenAnyIP(5241);
});


var app = builder.Build();


// =========================================================
// HTTP REQUEST PIPELINE
// =========================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}


// =========================================================
// HTTPS REDIRECTION
// =========================================================
// Disabled during Development so phone can use HTTP directly.
// This avoids localhost certificate problems while testing LAN.

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}


app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();