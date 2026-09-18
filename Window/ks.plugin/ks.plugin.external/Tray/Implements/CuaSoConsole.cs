using ks.plugin.external.Tray.Interfaces;
using System.Runtime.InteropServices;

namespace ks.plugin.external.Tray.Implements
{
    public class CuaSoConsole : ICuaSoConsole
    {
        private const int SwHide = 0;
        private const int SwShow = 5;
        private const int SwRestore = 9;
        private const uint ScClose = 0xF060;
        private const uint MfByCommand = 0x0;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bRevert);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

        public void An()
        {
            var cuaSo = GetConsoleWindow();
            if (cuaSo == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(cuaSo, SwHide);
        }

        public void Hien()
        {
            var cuaSo = GetConsoleWindow();
            if (cuaSo == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(cuaSo, IsIconic(cuaSo) ? SwRestore : SwShow);
            SetForegroundWindow(cuaSo);
        }

        public void GoNutDong()
        {
            var cuaSo = GetConsoleWindow();
            if (cuaSo == IntPtr.Zero)
            {
                return;
            }

            var menu = GetSystemMenu(cuaSo, false);
            if (menu == IntPtr.Zero)
            {
                return;
            }

            DeleteMenu(menu, ScClose, MfByCommand);
        }
    }
}
