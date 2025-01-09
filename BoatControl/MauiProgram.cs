using BoatControl.Communication;
using BoatControl.Communication.Connections.Tcp.Searching;
using BoatControl.Communication.Storage;
using BoatControl.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace BoatControl
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });


            builder.Services.AddSingleton<IBroadcastSearch, ReplaceMeWithATcpListener>();
            builder.Services.AddSingleton<BoatControlCommunication>();
            builder.Services.TryAddSingleton<IPersistedStorage, MauiStorage>();


#if DEBUG
        builder.Services.AddHybridWebViewDeveloperTools();
        builder.Logging.AddDebug();            
#endif

            return builder.Build();
        }
    }
}
