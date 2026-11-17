using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Windows.Devices.Bluetooth.Advertisement;

namespace BluetoothRadar;

public partial class MainWindow : Window
{
    private readonly BluetoothLEAdvertisementWatcher _watcher = new();
    private readonly ObservableCollection<DeviceInfo> _devices = new();
    private bool _scanning;

    public MainWindow()
    {
        InitializeComponent();

        _watcher.Received += Watcher_Received;
        _watcher.Stopped += (_, _) => Dispatcher.Invoke(() =>
        {
            if (_scanning)
                StatusText.Text = "Scanner stopped";
        });
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_scanning)
        {
            _devices.Clear();
            DeviceLayer.Children.Clear();

            try
            {
                _watcher.Start();
                _scanning = true;
                ScanButton.Content = "Stop scanning";
                StatusText.Text = "Scanning for BLE advertisements…";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not start scanner: {ex.Message}";
            }
        }
        else
        {
            _watcher.Stop();
            _scanning = false;
            ScanButton.Content = "Start scanning";
            StatusText.Text = $"Stopped • {_devices.Count} device(s)";
        }
    }

    private void Watcher_Received(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        Dispatcher.Invoke(() =>
        {
            string name = string.IsNullOrWhiteSpace(args.Advertisement.LocalName)
                ? "Unknown device"
                : args.Advertisement.LocalName;

            int existingIndex = -1;

            for (int i = 0; i < _devices.Count; i++)
            {
                if (_devices[i].Address == args.BluetoothAddress)
                {
                    existingIndex = i;
                    break;
                }
            }

            var device = new DeviceInfo(
                args.BluetoothAddress,
                name,
                args.RawSignalStrengthInDBm);

            if (existingIndex >= 0)
                _devices[existingIndex] = device;
            else
                _devices.Add(device);

            StatusText.Text = $"Scanning • {_devices.Count} device(s)";
            RenderDevices();
        });
    }

    private void RenderDevices()
    {
        DeviceLayer.Children.Clear();

        double width = RadarCanvas.ActualWidth;
        double height = RadarCanvas.ActualHeight;

        if (width < 10 || height < 10)
            return;

        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) * 0.42;

        for (int i = 0; i < _devices.Count; i++)
        {
            DeviceInfo device = _devices[i];

            // Signal strength is only used for visual placement.
            // It is not a reliable measurement of physical distance.
            double strength = Math.Clamp((device.Rssi + 100.0) / 65.0, 0.08, 0.95);
            double dotRadius = radius * (1.0 - strength);
            double angle = i * 2.399963 + (device.Address % 1000) * 0.001;

            var dot = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Color.FromRgb(77, 224, 168)),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                ToolTip = $"{device.Name}\nType: {GuessDeviceType(device.Name)}\nRSSI: {device.Rssi} dBm"
            };

            Canvas.SetLeft(dot, centerX + Math.Cos(angle) * dotRadius - 7);
            Canvas.SetTop(dot, centerY + Math.Sin(angle) * dotRadius - 7);
            DeviceLayer.Children.Add(dot);
        }
    }

    private static string GuessDeviceType(string name)
    {
        string text = name.ToLowerInvariant();

        if (text.Contains("head") || text.Contains("earbud") || text.Contains("buds"))
            return "Headphones / audio (name-based guess)";
        if (text.Contains("watch") || text.Contains("band"))
            return "Wearable (name-based guess)";
        if (text.Contains("mouse") || text.Contains("keyboard"))
            return "Computer accessory (name-based guess)";
        if (text.Contains("phone") || text.Contains("iphone"))
            return "Phone (name-based guess)";
        if (text.Contains("speaker"))
            return "Speaker (name-based guess)";

        return "Unknown";
    }

    private void RadarCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double centerX = e.NewSize.Width / 2;
        double centerY = e.NewSize.Height / 2;
        double radius = Math.Min(e.NewSize.Width, e.NewSize.Height) * 0.42;

        PositionRing(OuterRing, centerX, centerY, radius);
        PositionRing(MiddleRing, centerX, centerY, radius * 0.66);
        PositionRing(InnerRing, centerX, centerY, radius * 0.33);

        HorizontalLine.X1 = centerX - radius;
        HorizontalLine.X2 = centerX + radius;
        HorizontalLine.Y1 = centerY;
        HorizontalLine.Y2 = centerY;

        VerticalLine.X1 = centerX;
        VerticalLine.X2 = centerX;
        VerticalLine.Y1 = centerY - radius;
        VerticalLine.Y2 = centerY + radius;

        Canvas.SetLeft(CenterDot, centerX - CenterDot.Width / 2);
        Canvas.SetTop(CenterDot, centerY - CenterDot.Height / 2);

        RenderDevices();
    }

    private static void PositionRing(
        Ellipse ring, double centerX, double centerY, double radius)
    {
        ring.Width = radius * 2;
        ring.Height = radius * 2;
        Canvas.SetLeft(ring, centerX - radius);
        Canvas.SetTop(ring, centerY - radius);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_scanning)
            _watcher.Stop();

        base.OnClosed(e);
    }

    private sealed record DeviceInfo(ulong Address, string Name, short Rssi);
}
