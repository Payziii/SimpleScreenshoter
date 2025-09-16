using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace SimpleScreenshoter
{
    public static class Hotkeys
    {
        // Импорт функций из user32.dll
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Константы для модификаторов клавиш
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        // Константы для виртуальных кодов клавиш (пример)
        public const uint VK_F4 = 0x73; // F4
        public const uint VK_NUMPAD4 = 0x64;
    }
}
