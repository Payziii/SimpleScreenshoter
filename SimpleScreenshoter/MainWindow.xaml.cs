using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Windows.Threading;
using Drawing = System.Drawing; 
namespace SimpleScreenshoter
{
    public class ScreenshotInfo
    {
        public string Path { get; set; } = "";
        public string Created { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<ScreenshotInfo> Screenshots { get; set; } = new();


        private void LoadScreenshots()
        {
            Screenshots.Clear();
            string folder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "SimpleScreenshoter");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var files = Directory.GetFiles(folder, "*.png")
                                 .OrderByDescending(f => File.GetCreationTime(f));

            foreach (var file in files)
            {
                Screenshots.Add(new ScreenshotInfo
                {
                    Path = file,
                    Created = File.GetCreationTime(file).ToString("dd.MM.yyyy HH:mm")
                });
            }
        }

        private void Screenshot_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ScreenshotInfo info)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = info.Path,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть файл:\n{ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_4 = 0x34;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [DllImport("dwmapi.dll")]
        private static extern int DwmFlush();

        private HwndSource? _source;

        public MainWindow()
        {
            InitializeComponent();
            LoadScreenshots();
            ScreenshotsList.ItemsSource = Screenshots;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
                this.WindowState = WindowState.Maximized;
            else
                this.WindowState = WindowState.Normal;
        }
    

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source?.AddHook(HwndHook);

            if (!RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_4))
            {
                MessageBox.Show("Не удалось зарегистрировать горячую клавишу.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
            }
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            base.OnClosed(e);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID)
                {
                    OnHotKeyPressed();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void OnHotKeyPressed()
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;

            try
            {

                ShowWindow(hwnd, SW_HIDE);


                try { DwmFlush(); } catch {}

                this.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));

                Thread.Sleep(30);

                var overlay = new SelectionOverlay();
                overlay.Owner = null;
                overlay.ShowDialog();

                if (overlay.ResultRect.Width > 0 && overlay.ResultRect.Height > 0)
                {
                    var r = overlay.ResultRect;
                    var bmp = CaptureRegion((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                    var bs = ConvertToBitmapSource(bmp);
                    Clipboard.SetImage(bs);

                    string imagesFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
    "SimpleScreenshoter");
                    Directory.CreateDirectory(imagesFolder);

                    string file = Path.Combine(imagesFolder, $"screen_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);

                    MessageBox.Show($"Скриншот сохранён в буфер и в файл:\n{file}",
                        "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            finally
            {
                ShowWindow(hwnd, SW_SHOW);
                try { DwmFlush(); } catch { }
                this.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                LoadScreenshots();
            }
        }



        private static Drawing.Bitmap CaptureRegion(int x, int y, int width, int height)
        {
            int screenLeft = (int)SystemParameters.VirtualScreenLeft;
            int screenTop = (int)SystemParameters.VirtualScreenTop;
            int screenWidth = (int)SystemParameters.VirtualScreenWidth;
            int screenHeight = (int)SystemParameters.VirtualScreenHeight;

            int rx = Math.Max(x, screenLeft);
            int ry = Math.Max(y, screenTop);
            int rw = Math.Min(width, screenWidth - (rx - screenLeft));
            int rh = Math.Min(height, screenHeight - (ry - screenTop));

            if (rw <= 0 || rh <= 0)
                throw new ArgumentException("Область захвата пуста или выходит за границы экрана.");

            var bmp = new Drawing.Bitmap(rw, rh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(rx, ry, 0, 0, new Drawing.Size(rw, rh), Drawing.CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }



        private static BitmapSource ConvertToBitmapSource(Drawing.Bitmap src)
        {
            var hBitmap = src.GetHbitmap();
            try
            {
                var bs = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                bs.Freeze();
                return bs;
            }
            finally
            {
                DeleteObject(hBitmap); 
            }
        }

    }

    public class SelectionOverlay : Window
    {
        private System.Windows.Point _start;
        private System.Windows.Shapes.Rectangle _rect;
        private Canvas _canvas;
        public Rect ResultRect { get; private set; } = Rect.Empty;

        public SelectionOverlay()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            Topmost = true;
            Cursor = Cursors.Cross;
            ShowInTaskbar = false;

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            _canvas = new Canvas();
            Content = _canvas;

            _rect = new System.Windows.Shapes.Rectangle
            {
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0))
            };
            _canvas.Children.Add(_rect);

            MouseDown += SelectionOverlay_MouseDown;
            MouseMove += SelectionOverlay_MouseMove;
            MouseUp += SelectionOverlay_MouseUp;
            KeyDown += SelectionOverlay_KeyDown;
        }

        private void SelectionOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ResultRect = Rect.Empty;
                Close();
            }
        }

        private void SelectionOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _start = e.GetPosition(this);
            Canvas.SetLeft(_rect, _start.X);
            Canvas.SetTop(_rect, _start.Y);
            _rect.Width = 0;
            _rect.Height = 0;
            CaptureMouse();
        }

        private void SelectionOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseCaptured)
            {
                var p = e.GetPosition(this);
                double x = Math.Min(p.X, _start.X);
                double y = Math.Min(p.Y, _start.Y);
                double w = Math.Abs(p.X - _start.X);
                double h = Math.Abs(p.Y - _start.Y);
                Canvas.SetLeft(_rect, x);
                Canvas.SetTop(_rect, y);
                _rect.Width = w;
                _rect.Height = h;
            }
        }

        private void SelectionOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
                var left = Canvas.GetLeft(_rect);
                var top = Canvas.GetTop(_rect);
                var w = _rect.Width;
                var h = _rect.Height;

                var source = PresentationSource.FromVisual(this);
                double dpiX = 1.0, dpiY = 1.0;
                if (source != null)
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }

                var sx = left * dpiX + SystemParameters.VirtualScreenLeft;
                var sy = top * dpiY + SystemParameters.VirtualScreenTop;
                var sw = Math.Max(1, (int)Math.Round(w * dpiX));
                var sh = Math.Max(1, (int)Math.Round(h * dpiY));

                ResultRect = new Rect(sx, sy, sw, sh);
                Close();
            }
        }
    }
}
