using ebookStore.Models;
using ebookStore.Services; // For DbContext class
using Microsoft.EntityFrameworkCore;  // For Entity Framework Core

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();
//Add DbContext configuration
builder.Services.AddDbContext<EbookContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .EnableSensitiveDataLogging() // Enable detailed logging
        .LogTo(Console.WriteLine, LogLevel.Information));   // Uses connection string from appsettings.json

    
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHttpsRedirection();
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name:"signup_route",
    pattern:"{controller = Account} / {action = SignUp}/{id?}");
app.MapControllerRoute(
    name: "login_route",
    pattern: "{controller=Account} / {action=LogIn}/{id?}");
app.MapControllerRoute(
    name: "admin",
    pattern: "{controller=Admin}/{action=AddBook}/{id?}");
app.MapControllerRoute(
    name: "ManageBooks",
    pattern: "{controller=Admin}/{action=ManageBooks}/{id?}");
app.MapControllerRoute(
    name: "EditBook",
    pattern: "{controller=Admin}/{action=EditBook}/{id?}");
app.MapControllerRoute(
    name: "Checkout",
    pattern: "{controller=Cart}/{action = Checkout}/{id?}");

app.Run();