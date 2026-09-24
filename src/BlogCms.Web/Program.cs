using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Captcha;
using BlogCms.Infrastructure.Comments;
using BlogCms.Infrastructure.Content;
using BlogCms.Infrastructure.Data;
using BlogCms.Infrastructure.Email;
using BlogCms.Infrastructure.LinkLists;
using BlogCms.Infrastructure.Media;
using BlogCms.Infrastructure.Newsletter;
using BlogCms.Infrastructure.Payments;
using BlogCms.Infrastructure.Ratings;
using BlogCms.Web.Authorization;
using BlogCms.Web.Configuration;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Load local .env (KEY=VALUE) into process environment variables BEFORE the
// configuration is built, so flags like Features__MonetizationEnabled are picked
// up. Real environment variables (e.g. from Docker) keep precedence.
EnvFileLoader.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// Feature flags: steuern die UI-Sichtbarkeit von Monetarisierung und Login
// (Sektion "Features" bzw. z. B. Features__MonetizationEnabled / Features__LoginEnabled).
builder.Services.Configure<FeatureOptions>(
    builder.Configuration.GetSection(FeatureOptions.SectionName));

// Database: PostgreSQL via EF Core. The connection string is supplied through
// configuration (appsettings.Development.json, environment variables such as
// ConnectionStrings__DefaultConnection, or user secrets) — never hardcoded.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found. Set it via appsettings.Development.json, " +
        "the ConnectionStrings__DefaultConnection environment variable, or user secrets.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Identity: custom User (Guid) + role store backed by the same AppDbContext.
builder.Services
    .AddIdentity<User, IdentityRole<Guid>>(options =>
    {
        // E-mail confirmation is mandatory before commenting / subscribing.
        options.SignIn.RequireConfirmedEmail = true;
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
});

// Email: abstraction with a local dev implementation (writes .html files).
builder.Services.AddScoped<IAppEmailSender, DevEmailSender>();

// Content: Markdown rendering (sanitized), slug generation and article logic.
builder.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
builder.Services.AddSingleton<ISlugGenerator, SlugGenerator>();
builder.Services.AddScoped<IArticleService, ArticleService>();

// Scheduled publication: background poller that flips due "Scheduled" articles (BR-032).
builder.Services.AddHostedService<ArticleSchedulerService>();

// Ratings: thumbs up/down for articles and comments (BR-070/BR-071/BR-072).
builder.Services.AddScoped<IRatingService, RatingService>();

// Link lists: admin-curated, ordered article lists (BR-090/BR-091/BR-092).
builder.Services.AddScoped<ILinkListService, LinkListService>();

// Comments: moderation mode, blocklist filter and rate limiting are configurable.
builder.Services.Configure<CommentOptions>(
    builder.Configuration.GetSection(CommentOptions.SectionName));
builder.Services.AddScoped<ICommentService, CommentService>();

// Media: storage abstraction (Local dev / S3-compatible), image processing and oEmbed.
builder.Services.Configure<MediaOptions>(options =>
{
    builder.Configuration.GetSection(MediaOptions.SectionName).Bind(options);

    // Local dev storage defaults to wwwroot/uploads so files are served statically.
    if (string.IsNullOrWhiteSpace(options.LocalRootPath))
    {
        options.LocalRootPath = Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "uploads");
    }
});

builder.Services.AddSingleton<IImageProcessor, ImageSharpImageProcessor>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddSingleton<IOEmbedService, OEmbedService>();
builder.Services.AddHttpClient("oembed", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BlogCms/1.0");
});

// Storage backend is chosen by configuration; Local is the credential-free default.
builder.Services.AddSingleton<IStorageService>(serviceProvider =>
{
    var mediaOptions = serviceProvider.GetRequiredService<IOptions<MediaOptions>>().Value;
    return string.Equals(mediaOptions.Provider, "S3", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<S3StorageService>(serviceProvider)
        : ActivatorUtilities.CreateInstance<LocalDiskStorageService>(serviceProvider);
});

// Payments: Stripe abstraction. Without a secret key the dev fake is used, so
// checkout/webhooks run locally; the webhook signature check stays real.
builder.Services.Configure<PaymentOptions>(
    builder.Configuration.GetSection(PaymentOptions.SectionName));
builder.Services.AddHttpClient("stripe", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<IStripeService>(serviceProvider =>
{
    var paymentOptions = serviceProvider.GetRequiredService<IOptions<PaymentOptions>>().Value;
    return paymentOptions.IsConfigured
        ? ActivatorUtilities.CreateInstance<StripeService>(serviceProvider)
        : ActivatorUtilities.CreateInstance<FakeStripeService>(serviceProvider);
});
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

// Newsletter: double opt-in with token-based unsubscribe and cleanup job.
builder.Services.Configure<NewsletterOptions>(
    builder.Configuration.GetSection(NewsletterOptions.SectionName));
builder.Services.AddScoped<INewsletterService, NewsletterService>();
builder.Services.AddHostedService<NewsletterCleanupService>();

// Authorization: role-based moderation/admin plus subscription-driven premium access.
builder.Services.AddAuthorization(options =>
{
    // Authors manage their own articles; admins manage everything (FeatureFix1 BR-013/BR-014).
    options.AddPolicy(Policies.RequireAuthor,
        policy => policy.RequireRole(nameof(UserRole.Author), nameof(UserRole.Admin)));
    options.AddPolicy(Policies.RequireModerator,
        policy => policy.RequireRole(nameof(UserRole.Moderator), nameof(UserRole.Admin)));
    options.AddPolicy(Policies.RequireAdmin,
        policy => policy.RequireRole(nameof(UserRole.Admin)));
    options.AddPolicy(Policies.PremiumAccess,
        policy => policy.Requirements.Add(new PremiumRequirement()));
    // Registered access: any authenticated user (FeatureFix1 BR-110).
    options.AddPolicy(Policies.RegisteredAccess,
        policy => policy.RequireAuthenticatedUser());
});
builder.Services.AddScoped<IAuthorizationHandler, PremiumAuthorizationHandler>();

// Captcha für die Registrierung: Aufgabe + signierter Token (Data Protection).
builder.Services.Configure<CaptchaOptions>(builder.Configuration.GetSection(CaptchaOptions.SectionName));
builder.Services.AddScoped<ICaptchaService, DataProtectionCaptchaService>();

// Drosselung gegen Massenregistrierungen: zählt nur die tatsächlichen
// Registrierungsversuche (POST) pro IP-Adresse — nicht die Seitenaufrufe.
builder.Services.AddSingleton<IRegistrationThrottle, MemoryCacheRegistrationThrottle>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serves runtime-uploaded media from wwwroot/uploads in local dev.
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorPages()
   .WithStaticAssets();

// Die frühere Akten-Liste (/Articles) ging in der Suchseite auf: dauerhafte
// Weiterleitung mit Erhalt aller Filter-Parameter (die Namen sind identisch).
app.MapGet("/Articles", (HttpRequest request) =>
    Results.Redirect($"/Suche{request.QueryString.Value}", permanent: true));

// Ensure all application roles exist (idempotent).
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var roleName in Enum.GetNames<UserRole>())
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }
    }
}

app.Run();
