using Serilog;
using XSockets.Logger;

namespace WLNetwork.Utils
{
    public class MyLogger : XLogger
    {
        public MyLogger()
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
                .WriteTo.ColoredConsole()
                .CreateLogger();
        }
    }
}