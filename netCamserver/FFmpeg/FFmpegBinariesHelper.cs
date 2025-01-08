using System;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace API.Wrapper.FFmpeg
{
    public static partial class FFmpeg
    {
        public static string input_format_string { get { return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dshow" 
                                                                    : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "v4l2"
                                                                    : "avfoundation"; } }
    }

    public class FFmpegBinariesHelper
    {
        internal static void RegisterFFmpegBinaries()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var current = Environment.CurrentDirectory;
                var probe = Path.Combine("FFmpeg", "plugins");

                while (current != null)
                {
                    var ffmpegBinaryPath = Path.Combine(current, probe);

                    if (Directory.Exists(ffmpegBinaryPath))
                    {
                        Console.WriteLine($"FFmpeg binaries found in: {ffmpegBinaryPath}");
                        //DynamicallyLoadedBindings.LibrariesPath = ffmpegBinaryPath;
                        ffmpeg.RootPath = ffmpegBinaryPath;
                        return;
                    }

                    current = Directory.GetParent(current)?.FullName;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
                    ffmpeg.RootPath = "/lib/x86_64-linux-gnu/";
                else
                    ffmpeg.RootPath = "/usr/lib/aarch64-linux-gnu/";

            }
            else
                throw new NotSupportedException(); // fell free add support for platform of your choose
        }
    }
}