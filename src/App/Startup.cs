using App.Linebot;
using App.Linebot.Conversations.Core;
using App.Linebot.Persons;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.ProjectOxford.Face;
using System.IO;
using Yamac.LineMessagingApi.AspNetCore.Middleware;

namespace App
{
    public class Startup
    {
        private readonly bool _isAzureEnviroment;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: false)
                .AddEnvironmentVariables();
            if (env.IsDevelopment() || env.IsEnvironment("AzureDevelopment"))
            {
                builder.AddUserSecrets();
            }
            Configuration = builder.Build();
            _isAzureEnviroment = env.IsEnvironment("AzureDevelopment") || env.IsEnvironment("AzureProduction");
        }

        public IConfigurationRoot Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // データベース
            if (_isAzureEnviroment)
            {
                // Azure では Microsoft SQL Server
                services.AddDbContext<MainDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("MainDatabase")));
            }
            else
            {
                // Linux では MySQL
                services.AddDbContext<MainDbContext>(options => options.UseMySql(Configuration.GetConnectionString("MainDatabase")));
            }

            // オプション
            services.AddOptions();
            services.Configure<LineMessagingMiddlewareOptions>(Configuration.GetSection("Line"));
            services.Configure<LinebotOptions>(Configuration.GetSection("Linebot"));

            // LINE Messaging Service
            services.AddSingleton<ILineMessagingRequestHandler, LineMessagingRequestHandler>();
            services.AddSingleton<ILineMessagingService, LineMessagingService>();

            // 会話サービス
            services.AddConversation();

            // Microsoft Cognitive Service
            services.AddSingleton(new FaceServiceClient(Configuration["Microsoft:CognitiveService:Face:SubscriptionKey"]));

            // Person サービス
            services.AddSingleton<IPersonService, PersonService>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler();
            }

            // 作業用ディレクトリの作成
            if (!Directory.Exists(Configuration.GetSection("Linebot")["WorkingFileStoreRoot"]))
            {
                Directory.CreateDirectory(Configuration.GetSection("Linebot")["WorkingFileStoreRoot"]);
            }
            if (!Directory.Exists(Configuration.GetSection("Linebot")["GeneratedFileStoreRoot"]))
            {
                Directory.CreateDirectory(Configuration.GetSection("Linebot")["GeneratedFileStoreRoot"]);
            }

            // スタティックファイルを使う
            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        Configuration.GetSection("Linebot")["GeneratedFileStoreRoot"])),
                RequestPath = new PathString(
                    @"/" + Configuration.GetSection("Linebot")["GeneratedFilePublicUrlPath"])
            });

            // LINE Messaging を使う
            app.UseLineMessaging(new LineMessagingMiddlewareOptions
            {
                ChannelId = Configuration.GetSection("Line")["ChannelId"],
                ChannelSecret = Configuration.GetSection("Line")["ChannelSecret"],
                ChannelAccessToken = Configuration.GetSection("Line")["ChannelAccessToken"],
                WebhookPath = Configuration.GetSection("Line")["WebhookPath"],
            });
        }
    }
}
