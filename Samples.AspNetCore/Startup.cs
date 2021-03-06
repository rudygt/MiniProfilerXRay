﻿using Amazon.XRay.Recorder.Core.Sampling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniProfilerXRay;
using MiniProfilerXRay.EntityFrameworkCore;
using Samples.AspNetCore.Models;
using StackExchange.Profiling.Storage;

namespace Samples.AspNetCore
{
    public class Startup
    {
        public static string SqliteConnectionString { get; } = "Data Source=Samples; Mode=Memory; Cache=Shared";
        private static readonly SqliteConnection TrapConnection = new SqliteConnection(SqliteConnectionString);

        public Startup(IHostingEnvironment env)
        {
            TrapConnection.Open(); //Hold the in-memory SQLite database open

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddDbContext<SampleContext>();
            services.AddMvc();

            // extra, profile controller creation time
            services.AddSingleton<IControllerFactory, MiniprofilerControllerFactory>();

            // required to inject service provider later, to capture http context inside the storage provider
            var xrayStorage = new XRayMiniprofilerStorage("192.168.99.100:2000", "AspNetCoreTest");
            services.AddSingleton(xrayStorage);

            // optional use the XRay sampling strategy to profile or not
            var strategy = new DefaultSamplingStrategy();
            // Add MiniProfiler services
            // If using Entity Framework Core, add profiling for it as well (see the end)
            // Note .AddMiniProfiler() returns a IMiniProfilerBuilder for easy Intellisense
            services.AddMiniProfiler(options =>
            {
                // ALL of this is optional. You can simply call .AddMiniProfiler() for all defaults
                // Defaults: In-Memory for 30 minutes, everything profiled, every user can see

                // Path to use for profiler URLs, default is /mini-profiler-resources
                options.RouteBasePath = "/profiler";

                // Control storage - the default is 30 minutes
                //(options.Storage as MemoryCacheStorage).CacheDuration = TimeSpan.FromMinutes(60);
                options.Storage = new MultiStorageProvider(options.Storage, xrayStorage);

                // Control which SQL formatter to use, InlineFormatter is the default
                options.SqlFormatter = new StackExchange.Profiling.SqlFormatters.SqlServerFormatter();

                // To control authorization, you can use the Func<HttpRequest, bool> options:
                options.ResultsAuthorize = request => !Program.DisableProfilingResults;
                //options.ResultsListAuthorize = request => MyGetUserFunction(request).CanSeeMiniProfiler;

                // To control which requests are profiled, use the Func<HttpRequest, bool> option:
                //options.ShouldProfile = request => MyShouldThisBeProfiledFunction(request);

                options.ShouldProfile = request =>
                {
                    var serviceName = request.Host.Host;
                    var url = request.Path;
                    var method = request.Method;
                    var sampled = strategy.Sample(serviceName, url, method);
                    return sampled == SampleDecision.Sampled;
                };

                // Profiles are stored under a user ID, function to get it:
                //options.UserIdProvider =  request => MyGetUserIdFunction(request);

                // Optionally swap out the entire profiler provider, if you want
                // The default handles async and works fine for almost all applications
                //options.ProfilerProvider = new MyProfilerProvider();

                // Optionally disable "Connection Open()", "Connection Close()" (and async variants).
                //options.TrackConnectionOpenClose = false;
            }).AddXRayEntityFramework();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
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
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseMiniProfiler();

            // pass serviceprovider to xray storage
            var xrayStorage = app.ApplicationServices.GetRequiredService<XRayMiniprofilerStorage>();
            xrayStorage.SetServiceProvider(app.ApplicationServices);

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "areaRoute",
                    template: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            var serviceScopeFactory = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>();
            using (var serviceScope = serviceScopeFactory.CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetService<SampleContext>();
                dbContext.Database.EnsureCreated();
            }
            // For nesting test routes
            new SqliteStorage(SqliteConnectionString).WithSchemaCreation();
        }
    }
}
