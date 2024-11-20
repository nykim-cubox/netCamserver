using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CameraServer
{
    public class WindowUtils
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("Kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("User32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow([In] IntPtr windowHandle, [In] int showCommand);

        private const int SW_MINIMIZE = 6;

        public static CancellationTokenSource cts = new CancellationTokenSource();

        public static bool IsCancellationRequested { get { return cts.IsCancellationRequested; } }

        public static void HideConsole()
        {
            IntPtr consoleHandle = GetStdHandle(-10); //STD_INPUT_HANDLE
            UInt32 consoleMode;
            GetConsoleMode(consoleHandle, out consoleMode);
            consoleMode &= ~((uint)0x0040);
            SetConsoleMode(consoleHandle, consoleMode);

            IntPtr consoleWindowHandle = GetConsoleWindow();
            ShowWindow(consoleWindowHandle, SW_MINIMIZE);
        }

        public static void SetCancelOnConsole()
        {
            Console.CancelKeyPress += (s, ea) =>
            {
                ea.Cancel = true;
                cts.Cancel();
            };
        }

        public static void WaitConsoleApplication()
        {
            while (true)
            {
                if (IsCancellationRequested)
                    break;
            }
        }
    }
}
