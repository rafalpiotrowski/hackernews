using System.Net.Security;
using HackerNews;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss:fff";
});

builder.Host.ConfigureServices((hostContext, services) =>
{
    services.AddHttpClient(BestStoriesService.HACKER_NEWS_HTTP_CLIENT).ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new SocketsHttpHandler()
        {
            MaxConnectionsPerServer = 50, //TODO: we can make this configurable or find a best compromise
            SslOptions = new SslClientAuthenticationOptions
            {
                // not a production code, but a starting point to make SSL work
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            },
            
        };
    }).ConfigureHttpClient((sp, client) =>
    {
        client.BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/");
    });
    services.AddSingleton<StoriesCacheService>();
    services.AddSingleton<BestStoriesService>();
});

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

