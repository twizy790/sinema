using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using CinemaSite.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys")));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<CinemaStore>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "CinemaSite.Auth";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnValidatePrincipal = context =>
    {
        var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = userId is null ? null : context.HttpContext.RequestServices.GetRequiredService<CinemaStore>().Read(data => data.Users.FirstOrDefault(x => x.Id == userId && x.IsActive));
        if (user is null)
        {
            context.RejectPrincipal();
            return context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        var identity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
        if (identity is not null && (identity.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value != user.Role || identity.Name != user.Name))
        {
            identity.RemoveClaim(identity.FindFirst(System.Security.Claims.ClaimTypes.Role)!);
            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role));
            identity.RemoveClaim(identity.FindFirst(System.Security.Claims.ClaimTypes.Name)!);
            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.Name));
            context.ShouldRenew = true;
        }
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".avif"] = "image/avif";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

app.UseRouting();
app.UseRateLimiter();

app.Use(async (context, next) =>
{
    context.Items["SiteSettings"] = context.RequestServices.GetRequiredService<CinemaStore>().Read(data => data.Settings);
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
