using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace DiscoCartUtil
{

    public class COMPortInfo
    {
        public string? PortName { get; set; }
        public string? FriendlyName { get; set; }
    }

    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _vm;
        private readonly DispatcherTimer _portListUpdateTimer;
        private bool isClosing;

        private void UpdatePortListThread()
        {
            Thread thread = new(UpdatePortList);
            thread.Start();
        }

        private static void AvaloniaMessageBox(string? title, string message, ButtonEnum buttonType, MsBox.Avalonia.Enums.Icon iconType)
        {
            using var source = new CancellationTokenSource();
            _ = MsBox.Avalonia.MessageBoxManager
            .GetMessageBoxStandard(title ?? "Unknown Title Argument", message, buttonType, iconType)
                        .ShowAsync().ContinueWith(t => source.Cancel(), TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.UIThread.MainLoop(source.Token);
        }

        public MainWindow()
        {
            InitializeComponent();

            Closing += (s, e) =>
            {
                isClosing = true;
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var localDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local");

                if (!Directory.Exists(localDir))
                {
                    Directory.CreateDirectory(localDir);
                    var shareDir = Path.Join(localDir, "share");
                    if (!Directory.Exists(shareDir))
                    {
                        Directory.CreateDirectory(shareDir);
                    }   
                }
            }

            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            isClosing = false;
            _vm = new MainWindowViewModel();
            DataContext = _vm;


            _portListUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _portListUpdateTimer.Tick += (sender, e) => UpdatePortListThread();
            _portListUpdateTimer.Start();

            UpdatePortList();
            _vm.Ports.SelectIdFromText(Properties.Settings.Default.Port);
        }

        private readonly object updatePortLock = new();

        private void UpdatePortList()
        {

            if (!isClosing && Monitor.TryEnter(updatePortLock))
            {
                try
                {
                    List<string> arduinoPorts = SetupCOMPortInformation();

                    arduinoPorts.Sort();

                    string[] ports = arduinoPorts.ToArray<string>();

                    if (ports.Length == 0)
                    {
                        ports = new string[1];
                        ports[0] = "No Arduino/Teensy Found";
                        _vm.Ports.UpdateContents(ports);

                    }
                    else
                    {
                        _vm.Ports.UpdateContents(ports);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Closing the window can cause this due to a race condition
                }
                finally
                {
                    Monitor.Exit(updatePortLock);
                }
            }
        }

        private void ComPortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //((ComboBox)sender).SelectionChanged -= ComPortCombo_SelectionChanged;

            //UpdatePortList();
            //((ComboBox)sender).SelectionChanged += ComPortCombo_SelectionChanged;
        }

        private static string[] GetUSBCOMDevices()
        {
            List<string> list = new();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {

                ManagementObjectSearcher searcher2 = new("SELECT * FROM Win32_PnPEntity");
                foreach (ManagementObject mo2 in searcher2.Get().Cast<ManagementObject>())
                {
                    if (mo2["Name"] != null)
                    {
                        string? name = mo2["Name"].ToString();
                        // Name will have a substring like "(COM12)" in it.
                        if (name?.Contains("(COM") == true)
                        {
                            list.Add(name);
                        }
                    }
                }
                searcher2.Dispose();
            }

            // remove duplicates, sort alphabetically and convert to array
            string[] usbDevices = list.Distinct().OrderBy(s => s).ToArray();
            return usbDevices;
        }

        private static List<string> SetupCOMPortInformation()
        {
            List<COMPortInfo> comPortInformation = new();

            string[] portNames = SerialPort.GetPortNames();
            foreach (string s in portNames)
            {
                // s is like "COM14"
                COMPortInfo ci = new()
                {
                    PortName = s,
                    FriendlyName = s
                };
                comPortInformation.Add(ci);
            }

            string[] usbDevs = GetUSBCOMDevices();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (string s in usbDevs)
                {
                    // Name will be like "USB Bridge (COM14)"
                    int start = s.IndexOf("(COM", StringComparison.Ordinal) + 1;
                    if (start >= 0)
                    {
                        int end = s.IndexOf(")", start + 3, StringComparison.Ordinal);
                        if (end >= 0)
                        {
                            // cname is like "COM14"
                            string cname = s[start..end];
                            for (int i = 0; i < comPortInformation.Count; i++)
                            {
                                if (comPortInformation[i].PortName == cname)
                                {
                                    comPortInformation[i].FriendlyName = s.Remove(start - 1).TrimEnd();
                                }
                            }
                        }
                    }
                }
            }

            List<string> ports = new();
            foreach (COMPortInfo port in comPortInformation)
            {
                ports.Add(port.PortName ?? "COMX");
            }

            return ports;
        }

        private void ComPortCombo_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdatePortList();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            isClosing = true;
        }

        private void Window_Open(object sender, EventArgs e)
        {
            UpdatePortList();
        }

        private void DumpThread()
        {

            SerialPort? port = null;
            BinaryWriter? buffer = null;
            try
            {
                DateTime startTime = DateTime.Now;
                buffer = new(File.Open(FilenameTextText ?? string.Empty, FileMode.OpenOrCreate));
                
                port = new(ComPortComboSelectedValue, 4608000);
                port.Open();

                if (BankCombo.SelectedIndex == 0)
                    port.Write("BLOW%");
                else
                    port.Write("BHI%");

                int index = 0;
                string cmd = string.Empty;
                do
                {
                    if (isClosing)
                        return;

                    int cmdSent = 0;
                    do
                    {
                        cmd += string.Format("R${0:x}%", index);
                        cmdSent++;
                        if (index % 0xFFF == 0)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                progressBar.Value = (int)Math.Round((float)index / Limit * 100.0f);
                            });
                        }
                        if (index == Limit)
                            break;
                        
                        index++;
                    } while (index % 100 != 0);

                    port.Write(cmd);
                    cmd  = string.Empty;

                    while (cmdSent != 0)
                    {
                        while (port.BytesToRead < 5) { }

                        char[] num = new char[5];
                        port.Read(num, 0, 5);
                        --cmdSent;
                        string s = new(num);

                        buffer.Write(Convert.ToUInt16(s.Trim(), 16));
                    }

                } while (index != Limit);

                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaMessageBox("Disco-Cart Utility", string.Format("Dump completed in {0} seconds.", (DateTime.Now - startTime).TotalSeconds), ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info);
                });

            }
            catch (IOException)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaMessageBox("Disco-Cart Utility", string.Format("Could not connect to {0}.", ComPortComboSelectedValue), ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
                });
            }
            catch (ArgumentNullException)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaMessageBox("Disco-Cart Utility", string.Format("Could not dump rom to filename '{0}'.", FilenameTextText), ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaMessageBox("Disco-Cart Utility", ex.Message, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
                });
            }
            finally
            {
                port?.Close();
                buffer?.Close();

                Dispatcher.UIThread.Post(() =>
                {
                    ComPortCombo.IsEnabled = true;
                    BankCombo.IsEnabled = true;
                    LimitCombo.IsEnabled = true;
                    dumpButton.IsEnabled = true;
                    uploadButton.IsEnabled = true;
                    goButton.IsEnabled = true;
                    FilenameText.IsEnabled = true;
                    progressBar.Value = 0;
                });
            }
        }

        private void Dump_Click(object sender, RoutedEventArgs e)
        {
            ComPortCombo.IsEnabled = false;
            BankCombo.IsEnabled = false;
            LimitCombo.IsEnabled = false;
            dumpButton.IsEnabled = false;
            uploadButton.IsEnabled = false;
            goButton.IsEnabled = false;
            FilenameText.IsEnabled = false;

            ComPortComboSelectedValue = ComPortCombo.SelectedValue?.ToString();
            FilenameTextText = FilenameText?.Text;
            switch(LimitCombo.SelectedIndex)
            {
                case 0:
                    Limit = 0xFFFFF;
                    break;
                case 1:
                    Limit = 0x1FFFFF;
                    break;
                case 2:
                    Limit = 0x3FFFFF;
                    break;
            }
            Thread thread = new(DumpThread);
            thread.Start();

            Properties.Settings.Default.Port = _vm.Ports.SelectedItem;
            Properties.Settings.Default.Save();
        }

        string? ComPortComboSelectedValue;
        string? FilenameTextText;
        int Limit;

        private void UploadThread()
        {
            SerialPort? port = null;
            BinaryReader? rom = null;
            try
            {
                DateTime startTime = DateTime.Now;
                port = new(ComPortComboSelectedValue, 4608000);
                port.Open();
                byte[] bytes = File.ReadAllBytes(FilenameTextText ?? string.Empty);
                rom = new(File.Open(FilenameTextText ?? string.Empty, FileMode.Open));

                if (BankCombo.SelectedIndex == 0)
                    port.Write("BLOW%");
                else
                    port.Write("BHI%");

                port.Write("ERS%");

                int index = 0;
                string cmd = string.Empty;
                while (rom.BaseStream.Position != rom.BaseStream.Length)
                {
                    if (isClosing)
                        return;

                    if (index % 0xFFFF == 0)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            progressBar.Value = Math.Round(index / (bytes.Length / 2.0f) * 100.0f);
                        });
                    }

                    if (index % 100 == 0)
                    {
                        port.Write(cmd);
                        cmd = string.Empty;
                    }
                    cmd  += string.Format("W{0:x}:{1:x}%", index, rom.ReadUInt16());
                    index++;
                }
                port.Write(cmd);
                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaMessageBox("Disco-Cart Utility", string.Format("Uploaded completed in {0} seconds.", (DateTime.Now - startTime).TotalSeconds), ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info);
                });
            }
            catch (IOException)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaMessageBox("Disco-Cart Utility", string.Format("Could not connect to {0}.", ComPortComboSelectedValue), ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
                });
            }
            catch (ArgumentNullException)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaMessageBox("Disco-Cart Utility", string.Format("Could not upload rom with filename '{0}'.", FilenameTextText), ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaMessageBox("Disco-Cart Utility", ex.Message, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
                });
            }
            finally
            {
                rom?.Close();
                port?.Close();

                Dispatcher.UIThread.Post(() =>
                {
                    ComPortCombo.IsEnabled = true;
                    BankCombo.IsEnabled = true;
                    LimitCombo.IsEnabled = true;
                    dumpButton.IsEnabled = true;
                    uploadButton.IsEnabled = true;
                    goButton.IsEnabled = true;
                    FilenameText.IsEnabled = true;
                    progressBar.Value = 0;
                });
            }
        }

        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            ComPortCombo.IsEnabled = false;
            BankCombo.IsEnabled = false;
            LimitCombo.IsEnabled = false;
            dumpButton.IsEnabled = false;
            uploadButton.IsEnabled = false;
            goButton.IsEnabled = false;
            FilenameText.IsEnabled = false;

            ComPortComboSelectedValue = ComPortCombo.SelectedValue?.ToString();
            FilenameTextText = FilenameText?.Text;
            Thread thread = new(UploadThread);
            thread.Start();

            Properties.Settings.Default.Port = _vm.Ports.SelectedItem;
            Properties.Settings.Default.Save();
        }

        private async void SaveAs_Click(object sender, RoutedEventArgs e)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            SaveFileDialog FileBox = new()
            {
                Title = "ROM Filename...",
                ShowOverwritePrompt = false,
                DefaultExtension = "bin"
            };
#pragma warning restore CS0618 // Type or member is obsolete

            var saveFilename = await FileBox.ShowAsync(this);

            if (saveFilename != null)
            {
                FilenameText.Text = saveFilename;
            }
        }

    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public ListView<string> Ports { get; set; }

        public MainWindowViewModel()
        {
            Ports = new ListView<string>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

    }
}