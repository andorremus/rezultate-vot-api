using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ElectionResults.Core.Elections;
using ElectionResults.Core.Extensions;
using ElectionResults.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace ElectionResults.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDefaultIdentity<IdentityUser>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = false;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddControllersWithViews();
            services.AddRazorPages();

            services
                .AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Latest)

                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.IgnoreNullValues = true;
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(new SnakeCaseNamingPolicy()));
                }); ;
            RegisterDependencies(services, Configuration);
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Rezultate Vot API", Version = "v2" });
            });
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseMySQL(Configuration["ConnectionStrings:DefaultConnection"]);
            });

          
            services.AddLazyCache();
            services.AddCors(options =>
            {
                options.AddPolicy("origins",
                    builder =>
                    {
                        builder.WithOrigins("*");
                    });
            });
        }

        private static void RegisterDependencies(IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<IResultsAggregator, ResultsAggregator>();
            services.AddTransient<IArticleRepository, ArticleRepository>();
            services.AddTransient<IElectionRepository, ElectionRepository>();
            services.AddTransient<IPicturesRepository, PicturesRepository>();
            services.AddTransient<IAuthorsRepository, AuthorsRepository>();

            services.Configure<AWSS3Settings>(configuration.GetSection("S3Bucket"));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ApplicationDbContext context)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            MigrateDatabase(context);
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Rezultate Vot API V2");
            });
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCors("origins");

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
                if (env.IsProduction())
                {
                    endpoints.MapGet("/Identity/Account/Register", context => Task.Factory.StartNew(() => context.Response.Redirect("/Identity/Account/Login", true)));
                    endpoints.MapPost("/Identity/Account/Register", context => Task.Factory.StartNew(() => context.Response.Redirect("/Identity/Account/Login", true)));
                }
            });
        }

        private static void MigrateDatabase(ApplicationDbContext context)
        {
            try
            {
                context.Database.Migrate();
            }
            catch (Exception e)
            {
                //Log.LogError(e);
            }
        }
    }
}
