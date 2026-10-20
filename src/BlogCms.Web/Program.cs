using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Activity;
using BlogCms.Infrastructure.Captcha;
using BlogCms.Infrastructure.Comments;
using BlogCms.Infrastructure.Contact;
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
using BlogCms.Web.Models.Api;
using BlogCms.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Load local .env (KEY=VALUE) into process environment variables BEFORE the
// configuration is built, so flags like Features__MonetizationEnabled are picked
// up. Real environment variables (e.g. from Docker) keep precedence.
EnvFileLoader.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // Enums als Klartextwerte (z. B. "Published") — konsistent zur Persistenz und
    // gut lesbar für automatisierte API-Clients.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddMemoryCache();

// Management-API: statischer API-Key, Autorzuordnung, Swagger-UI-Sichtbarkeit und
// Anfragen-Drosselung (siehe plans/api-artikelverwaltung.md).
builder.Services.Configure<ApiOptions>(
    builder.Configuration.GetSection(ApiOptions.SectionName));

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

// Management-API: eigener API-Key-Authentifizierungsweg neben der Cookie-Auth der
// Razor Pages. Die Controller fordern das Schema explizit an
// ([Authorize(AuthenticationSchemes = "ApiKey")]) — die Cookie-Auth bleibt unberührt.
builder.Services
    .AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyDefaults.AuthenticationScheme, _ => { });

// ProblemDetails (RFC 7807) für die Fehlerausgabe der Management-API.
builder.Services.AddProblemDetails();

// OpenAPI-Dokumentation der Management-API: Spec-Generierung inkl. ApiKey-Schema,
// XML-Kommentaren und KI-Kurzleitfaden (siehe SwaggerSetup).
builder.Services.AddSwaggerGen(SwaggerSetup.ConfigureGen);

// Auch automatische Model-Binding-Fehler antworten im einheitlichen API-Format
// (ProblemDetails mit errors[] aus field/message).
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors
                .Select(error => new ApiFieldError(
                    entry.Key,
                    string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Ungültiger Wert."
                        : error.ErrorMessage)));
        return new BadRequestObjectResult(ApiProblems.Validation(errors));
    };
});

// Email: der Versand ist konfigurierbar. Ohne Zugangsdaten bleibt der
// Dev-Fallback aktiv (schreibt .html-Dateien), damit die App ohne Credentials
// startet — Tests, CI und der frische Dev-Container brauchen keine Mail.
// Siehe docs/04-mailversand.md.
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<IAppEmailSender>(serviceProvider =>
{
    var emailOptions = serviceProvider.GetRequiredService<IOptions<EmailOptions>>().Value;
    return emailOptions.IsConfigured
        ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(serviceProvider)
        : ActivatorUtilities.CreateInstance<DevEmailSender>(serviceProvider);
});

// Content: Markdown rendering (sanitized), slug generation and article logic.
builder.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
builder.Services.AddSingleton<ISlugGenerator, SlugGenerator>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<IArticlePostingService, ArticlePostingService>();

// Scheduled publication: background poller that flips due "Scheduled" articles (BR-032).
builder.Services.AddHostedService<ArticleSchedulerService>();

// Ratings: thumbs up/down for articles and comments (BR-070/BR-071/BR-072).
builder.Services.AddScoped<IRatingService, RatingService>();

// Link lists: admin-curated, ordered article lists (BR-090/BR-091/BR-092).
builder.Services.AddScoped<ILinkListService, LinkListService>();

// Activity log: unified recording of views, article/comment creation and ratings.
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

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

builder.Services.AddSingleton<IImageProcessor, MagickImageProcessor>();
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

// Kontaktformular: Zieladresse und Drosselung sind konfigurierbar. Die
// Drosselung zählt nur tatsächliche Sendeversuche pro IP-Adresse.
builder.Services.Configure<ContactOptions>(
    builder.Configuration.GetSection(ContactOptions.SectionName));
builder.Services.AddSingleton<IContactThrottle, MemoryCacheContactThrottle>();

// Drosselung der Passwort-Reset-Anfragen: verhindert Mail-Flooding und das
// schnelle Durchprobieren von E-Mail-Adressen über das Formular.
builder.Services.AddSingleton<IPasswordResetThrottle, MemoryCachePasswordResetThrottle>();

// Drosselung der Management-API: Fixed-Window pro Client-IP (Standard 60/min).
var apiRateLimitPerMinute = Math.Max(
    1, builder.Configuration.GetValue<int?>("Api:RateLimitPerMinute") ?? 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ApiOptions.RateLimitPolicy, httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = apiRateLimitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            await ApiProblems.WriteAsync(
                context.HttpContext.Response, ApiProblems.RateLimitExceeded(), cancellationToken);
        }
    };
});

var app = builder.Build();

// Welcher Mailversand tatsächlich aktiv ist, gehört ins Log: nur der Providername
// und das IsConfigured-Flag, niemals Host mit Zugangsdaten.
{
    var mailOptions = app.Services.GetRequiredService<IOptions<EmailOptions>>().Value;

    app.Logger.LogInformation(
        "E-Mail-Versand aktiv: {Provider} (konfiguriert: {Configured})",
        mailOptions.Provider,
        mailOptions.IsConfigured);

    if (!mailOptions.IsConfigured && !app.Environment.IsDevelopment())
    {
        // Auf Produktion ohne konfigurierten Versand landen keine E-Mails an.
        app.Logger.LogWarning(
            "E-Mail-Versand ist NICHT konfiguriert (Provider '{Provider}'). Registrierungs- " +
            "und Newsletter-Bestätigungen werden nicht zugestellt. Bitte Email__Provider, " +
            "Email__Host, Email__UserName, Email__Password und Email__FromAddress setzen.",
            mailOptions.Provider);
    }
}

// Management-API: Start-Hinweise. Die API ist ohne konfigurierten Key bewusst
// geschlossen (Fail-Closed); ein fehlender Autor blockiert nur das Anlegen.
var apiOptions = app.Services.GetRequiredService<IOptions<ApiOptions>>().Value;
if (!apiOptions.IsConfigured)
{
    app.Logger.LogWarning(
        "Management-API: kein Api:Key konfiguriert — /api/v1 bleibt für jeden Zugriff " +
        "geschlossen (Fail-Closed). Für die Nutzung Api__Key in .env setzen.");
}
else if (apiOptions.Key.Length < 32)
{
    app.Logger.LogWarning(
        "Management-API: der konfigurierte Api:Key ist kürzer als 32 Zeichen — bitte " +
        "einen längeren Zufallskey verwenden (z. B. openssl rand -hex 32).");
}

if (apiOptions.AuthorId is null)
{
    app.Logger.LogWarning(
        "Management-API: Api:AuthorId ist nicht konfiguriert — das Anlegen von Artikeln " +
        "über die API wird mit 503 abgelehnt.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // Fehlerbehandlung: API-Aufrufe erhalten JSON-ProblemDetails, die
    // Razor-Pages-Oberfläche weiterhin die HTML-Fehlerseite /Error.
    app.UseExceptionHandler(exceptionHandlerApp =>
    {
        exceptionHandlerApp.Run(async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await ApiProblems.WriteAsync(context.Response, ApiProblems.Internal());
            }
            else
            {
                context.Response.Redirect("/Error");
            }
        });
    });
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serves runtime-uploaded media from wwwroot/uploads in local dev.
app.UseStaticFiles();

app.UseRouting();

// Anfragen-Drosselung der Management-API (Policy "api", pro Client-IP).
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// OpenAPI-Dokumentation der Management-API: Die maschinenlesbare Spec bleibt immer
// erreichbar (Interface für KI-Clients); die interaktive UI ist zusätzlich in der
// Entwicklung aktiv und in Produktion über Api:SwaggerUiEnabled zuschaltbar.
app.UseSwagger();
if (apiOptions.SwaggerUiEnabled || app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(c =>
        c.SwaggerEndpoint($"/swagger/{SwaggerSetup.DocumentName}/swagger.json", SwaggerSetup.Title));
}

app.MapStaticAssets();
app.MapControllers();
app.MapRazorPages()
   .WithStaticAssets();

// Die frühere Akten-Liste (/Articles) ging in der Suchseite auf: dauerhafte
// Weiterleitung mit Erhalt aller Filter-Parameter (die Namen sind identisch).
app.MapGet("/Articles", (HttpRequest request) =>
    Results.Redirect($"/Suche{request.QueryString.Value}", permanent: true));

// Ensure database is created/migrated, roles exist, and standard categories are seeded (idempotent).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

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

// Explizite Deklaration für Integrationstests (WebApplicationFactory).
public partial class Program { }
