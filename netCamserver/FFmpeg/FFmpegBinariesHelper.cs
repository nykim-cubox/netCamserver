using System;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace API.Wrapper.FFmpeg
{
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
				// 공용 라이브러리 기본 경로 지정
				var defaultLinuxPath = "/usr/lib/aarch64-linux-gnu";

				if (Directory.Exists(defaultLinuxPath))
				{
					Console.WriteLine($"Using default Linux FFmpeg path: {defaultLinuxPath}");
					ffmpeg.RootPath = defaultLinuxPath;
				}
				else
				{
					throw new DirectoryNotFoundException("FFmpeg libraries not found in the default system library path. Please ensure FFmpeg is installed.");
				}
			}
            else
                throw new NotSupportedException(); // fell free add support for platform of your choose
        }
    }
}