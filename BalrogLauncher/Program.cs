using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BalrogLauncher
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((ctx, services) =>
                {
                    services.AddSingleton<Services.GameDataService>();
                    services.AddSingleton<Services.UpdateService>();
                    services.AddSingleton<ViewModels.MainWindowViewModel>();
                    services.AddSingleton<Views.MainWindow>();
                }).Build();

            var app = new App();
            app.InitializeComponent();
            app.Run(host.Services.GetRequiredService<Views.MainWindow>());
        }
    }
}
