using Serilog;
using System;

namespace CameraServer
{
    public enum LogLevel
    {
        Verbose,
        Debug,
        Information,
        Warning,
        Error,
        Fatal
    };
    public class LogControl
    {
        private static LogControl? logControl;
        private Serilog.Core.LoggingLevelSwitch levelSwitch = new Serilog.Core.LoggingLevelSwitch();

        private LogControl(string path, string prefix)
        {
            var log = new LoggerConfiguration()
                        .MinimumLevel.ControlledBy(levelSwitch)
                        .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message}{NewLine}{Exception}")
                        .WriteTo.File(
                            path + @"\" + prefix + @"_.log", rollingInterval: RollingInterval.Day,
                            outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message}{NewLine}{Exception}", retainedFileCountLimit: 14
                         )
                        .CreateLogger();

            Log.Logger = log;
        }

        public static void CreateLogControl(string path, string prefix, LogLevel log_level=LogLevel.Debug)
        {
            logControl = new LogControl(path, prefix);
            logControl.set_log_level(Convert.ToInt32(log_level));
        }

        private void set_log_level(int logLevel)
        {
            switch (logLevel)
            {
                case 0:
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;
                    break;
                case 1:
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
                    break;
                case 2:
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
                    break;
                case 3:
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Warning;
                    break;
                case 4:
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Error;
                    break;
                case 5:
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Fatal;
                    break;
                default:
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
                    break;
            }
        }

        public static void WriteLog(LogLevel logLevel, string message)
        {
            switch (logLevel)
            {
                case LogLevel.Verbose:
                    Log.Verbose(message);
                    break;
                case LogLevel.Debug:
                    Log.Debug(message);
                    break;
                case LogLevel.Information:
                    Log.Information(message);
                    break;
                case LogLevel.Warning:
                    Log.Warning(message);
                    break;
                case LogLevel.Error:
                    Log.Error(message);
                    break;
                case LogLevel.Fatal:
                    Log.Fatal(message);
                    break;
                default:
                    Log.Information(message);
                    break;
            }
        }
    }
}
