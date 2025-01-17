using ebookStore.BackgroundServices;
using ebookStore.Models;
using ebookStore.Services; 
using Microsoft.EntityFrameworkCore; 

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache(); 
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); 
    options.Cookie.HttpOnly = true; 
    options.Cookie.IsEssential = true;

});
builder.Services.AddControllersWithViews();
builder.Services.AddTransient<CartCleanupService>();
builder.Services.AddHostedService<CartCleanupHostedService>();


builder.Services.AddHostedService<BorrowedBooksCleanupHostedService>();
builder.Services.AddTransient<BorrowedBooksCleanupService>();


builder.Services.AddHostedService<DiscountCleanupHostedService>();
builder.Services.AddTransient<DiscountCleanupService>();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHttpsRedirection();
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection(); 
app.UseStaticFiles();
app.UseSession();
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
    name: "addBook",
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
app.MapControllerRoute(
    name: "profile",
    pattern: "User/Profile/{username?}",
    defaults: new { controller = "User", action = "Profile" });

app.Run();