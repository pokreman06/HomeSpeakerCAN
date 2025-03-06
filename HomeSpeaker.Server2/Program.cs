using HomeSpeaker.Server;
using HomeSpeaker.Server.Data;
using HomeSpeaker.Server2;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;

const string LocalCorsPolicy = nameof(LocalCorsPolicy);

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: LocalCorsPolicy,
                      policy =>
                      {
                          policy.WithOrigins("http://example.com",
                                              "http://www.contoso.com");
                      });
});
builder.Services.AddRazorPages();
builder.Services.AddGrpc();
builder.Services.AddHostedService<MigrationApplier>();
builder.Services.AddScoped<PlaylistService>();
builder.Services.AddDbContext<MusicContext>(options => options.UseSqlite(builder.Configuration["SqliteConnectionString"]));
builder.Services.AddSingleton<IDataStore, OnDiskDataStore>();
builder.Services.AddSingleton<IFileSource>(_ => new DefaultFileSource(builder.Configuration[ConfigKeys.MediaFolder] ?? throw new MissingConfigException(ConfigKeys.MediaFolder)));
builder.Services.AddSingleton<ITagParser, DefaultTagParser>();
builder.Services.AddSingleton<YoutubeService>();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddSingleton<WindowsMusicPlayer>();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    builder.Services.AddSingleton<MacMusicPlayer>();
}
else
{
    builder.Services.AddSingleton<LinuxSoxMusicPlayer>();
}


builder.Services.AddSingleton<IMusicPlayer>(services =>
{
    IMusicPlayer actualPlayer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? services.GetRequiredService<WindowsMusicPlayer>()
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? services.GetRequiredService<MacMusicPlayer>()
            : services.GetRequiredService<LinuxSoxMusicPlayer>();

    return new ChattyMusicPlayer(actualPlayer);
});

builder.Services.AddSingleton<Mp3Library>();
builder.Services.AddHostedService<LifecycleEvents>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.Logger.LogInformation("Starting HomeSpeaker.Server2");

app.UseResponseCompression();
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
//app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();
app.UseCors(LocalCorsPolicy);
app.MapRazorPages();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<HomeSpeakerService>();
app.MapGet("/ns", (IConfiguration config) => config["NIGHTSCOUT_URL"] ?? string.Empty);
app.MapPost("/files/add", async (IFormFile file, Mp3Library lib) =>
{
    using (var fileStream = new StreamWriter(File.Open($"/HomeSpeakerMedia/Mp3Uploads/{file.Name}.mp3", FileMode.Create)))
    {
        fileStream.Write(file);
    }
    //song.Path = $"/HomeSpeakerMedia/Mp3Uploads/{file.Name}.mp3";
    //lib.Songs.Append(song);

}).DisableAntiforgery();


app.MapFallbackToFile("index.html");

app.Run();
