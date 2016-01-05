using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using NativeWifi;
using System.Threading;
using System.Text;

namespace WiFiScanner.ViewModel
{
  /// <summary>
  /// Window command types (or state)
  /// </summary>
  public enum WindowCommandType
  {
    MinimizeWindow,
    MaximizeWindow,
    CloseWindow
  }

  /// <summary>
  /// Class that represents Network details
  /// </summary>
  public class NetworkDetails
  {
    /// <summary>
    /// Indicates wheter network should be shown on graph (canvas)
    /// </summary>
    public bool Show { get; set; }
    /// <summary>
    /// Security type of network (WEP, WPA2, Open etc.)
    /// </summary>
    public string Security { get; set; }
    /// <summary>
    /// MAC address of router (phisical address)
    /// </summary>
    public string NetworkBSSID { get; set; }
    /// <summary>
    /// Radio type of network (802.11g, 802.11n etc.)
    /// </summary>
    public string Mode { get; set; }            
    /// <summary>
    /// it's not a good idea, but it works (I don't know why, but with MultiBinding doesn't work!)
    /// Set of information (why? explained above):
    /// - NetworkName: SSID of network
    /// - Channel: channel that network is taking (1-13)
    /// - Signal: signal strength in percentage
    /// - Color: assigned color for displaying in DataGrid and on Graph (canvas)
    /// - CanvasWidth: actual canvas width (must be somehow sent, I am not proud of this solution)
    /// - CanvasHeight: actual canvas height
    /// </summary>
    public Tuple<string, int, int, string, double, double> NetworkNameChannelSignalColorCanvasWidthAndHeight { get; set; }    
  }

  /// <summary>
  /// Application ViewModel
  /// </summary>
  class AppViewModel : DependencyObject
  {
    /// <summary>
    /// Empty constructor
    /// </summary>
    public AppViewModel()
    {
   
    }

    #region Fields

    /// <summary>
    /// Array of colors for networks
    /// </summary>
    public string[] _colors = { "#FF80FF93", "#FFFFB0CA", "#FFE7FF9E", "#FF8A80FF", "#FFFFBF80", "#FF80FFF6", "#FF808080", "#FFDC73FF", "#FFCC546E", "#FFA0CC54", "#FFCC54CC", "#FFFF0000" };

    #endregion

    #region Properties    

    public bool IsScanning
    {
        get { return (bool)GetValue(IsScanningProperty); }
        set { SetValue(IsScanningProperty, value); }
    }

    // Using a DependencyProperty as the backing store for IsScanning.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsScanningProperty =
        DependencyProperty.Register("IsScanning", typeof(bool), typeof(AppViewModel), new PropertyMetadata(false));

    

    public bool ShowStartHint
    {
        get { return (bool)GetValue(ShowStartHintProperty); }
        set { SetValue(ShowStartHintProperty, value); }
    }

    // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ShowStartHintProperty =
        DependencyProperty.Register("ShowStartHint", typeof(bool), typeof(AppViewModel), new PropertyMetadata(true));


    /// <summary>
    /// Graph Canvas width (set in MainWindow.xaml.cs)
    /// </summary>
    public double CanvasWidth
    {
      get { return (double)GetValue(CanvasWidthProperty); }
      set { SetValue(CanvasWidthProperty, value); }
    }
    
    public static readonly DependencyProperty CanvasWidthProperty =
        DependencyProperty.Register("CanvasWidth", typeof(double), typeof(AppViewModel), new PropertyMetadata(0.0, new PropertyChangedCallback(OnCanvasWidthChanged)));

    public static void OnCanvasWidthChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
      // when width of canvas change recalculate the paths
      // done in MainWindow.caml.cs... (workaround) I don't know how to do it from ViewModel
    }

    /// <summary>
    /// Graph Canvas height (set in MainWindow.xaml.cs)
    /// </summary>
    public double CanvasHeight
    {
      get { return (double)GetValue(CanvasHeightProperty); }
      set { SetValue(CanvasHeightProperty, value); }
    }
    
    public static readonly DependencyProperty CanvasHeightProperty =
        DependencyProperty.Register("CanvasHeight", typeof(double), typeof(AppViewModel), new PropertyMetadata(0.0, new PropertyChangedCallback(OnCanvasHeightChanged)));

    public static void OnCanvasHeightChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
      // when height of canvas change recalculate the paths
      // done in MainWindow.caml.cs... (workaround) I don't know how to do it from ViewModel 
    }

    /// <summary>
    /// Information about Wi-Fi networks
    /// </summary>
    public List<NetworkDetails> NetworkValues
    {
      get { return (List<NetworkDetails>)GetValue(NetworkValuesProperty); }
      set { SetValue(NetworkValuesProperty, value); }
    }
    
    public static readonly DependencyProperty NetworkValuesProperty =
        DependencyProperty.Register("NetworkValues", typeof(List<NetworkDetails>), typeof(AppViewModel), new PropertyMetadata(null));

    #endregion

    #region Commands

    /// <summary>
    /// Close window command
    /// </summary>
    private ICommand _closeCommand;
    public ICommand CloseCommand
    {
        get
        {
            return _closeCommand ?? (_closeCommand = new WindowCommand(WindowCommandType.CloseWindow));
        }
    }

    /// <summary>
    /// Minimize window command
    /// </summary>
    private ICommand _minimizeCommand;
    public ICommand MinimizeCommand
    {
      get
      {
        return _minimizeCommand ?? (_minimizeCommand = new WindowCommand(WindowCommandType.MinimizeWindow));
      }
    }

    /// <summary>
    /// Maximize window command
    /// </summary>
    private ICommand _maximizeCommand;
    public ICommand MaximizeCommand
    {
      get
      {
        return _maximizeCommand ?? (_maximizeCommand = new WindowCommand(WindowCommandType.MaximizeWindow));
      }
    }

    /// <summary>
    /// Start scanning networks
    /// </summary>
    private ICommand _startScanningCommand;
    public ICommand StartScanningCommand
    {
      get
      {
        return _startScanningCommand ?? (_startScanningCommand = new ScanningCommand(() => MyAction()));
      }
    }

    /// <summary>
    /// Execute Netsh command, parse output, set NetworkValues property
    /// </summary>
    public void MyAction()
    {
      ShowStartHint = false;
      IsScanning = true;
      NetworkValues = ParseNetshOutput(StartWifiScanning());
      //IsScanning = false;
    }
    #endregion

    #region Methods

    /// <summary>
    /// Using Netsh "wlan show networks mode=bssid" command, display Wi-Fi networks and their details
    /// </summary>
    /// <returns>Result of Netsh command - Wi-Fi networks</returns>
    public string StartScanningNetsh()
    {
      Process proc = new Process();
      proc.StartInfo.CreateNoWindow = true;
      proc.StartInfo.FileName = "netsh";
      proc.StartInfo.Arguments = "wlan show networks mode=bssid";
      proc.StartInfo.RedirectStandardOutput = true;
      proc.StartInfo.UseShellExecute = false;
      proc.Start();
      var output = proc.StandardOutput.ReadToEnd();
      proc.WaitForExit();

      return output;
    }
    static string GetStringForSSID(Wlan.Dot11Ssid ssid)
    {
        return Encoding.ASCII.GetString(ssid.SSID, 0, (int)ssid.SSIDLength);
    }
    static string GetStringForBSSID(Wlan.WlanBssEntry BssEntry)
    {
        //return Encoding.ASCII.GetString(BssEntry.dot11Bssid, 0, (int)BssEntry.dot11Bssid.Length);
        StringBuilder sb = new StringBuilder();
        foreach (byte value in BssEntry.dot11Bssid)
            sb.AppendFormat("{0:x2}" + ":", value);
        return sb.ToString().Substring(0, sb.ToString().Length - 1);
    }
    /// <summary>
    /// Using managedWifi APIs to scan Wi-Fi networks and their details
    /// </summary>
    /// <returns>Result of managedWifi APIs scaned - Wi-Fi networks</returns>
    public string StartWifiScanning()
    {
        WlanClient client = new WlanClient();
        WlanClient.WlanInterface wlanIface = client.Interfaces[0];
        wlanIface.Scan();
        Thread.Sleep(10000);
        Wlan.WlanBssEntry[] WlanBssValues = wlanIface.GetNetworkBssList();
        Wlan.WlanAvailableNetwork[] networks = wlanIface.GetAvailableNetworkList(0);
        String APList = "";
        APList += "Wireless Card : " + wlanIface.InterfaceDescription + "\r\n";
        APList += "接口名称 : " + wlanIface.InterfaceName + "\r\n";
        APList += "当前有 " + WlanBssValues.Length.ToString() + "网络可见" + "\r\n";
        APList += "\r\n";
        int i = 1;
        foreach (Wlan.WlanBssEntry WlanBssValue in WlanBssValues)
        {
            APList += "SSID " + i.ToString() + ":" + GetStringForSSID(WlanBssValue.dot11Ssid) + "\r\n";
            APList += "Network type                :  结构" + "\r\n";
            String channel = Convert.ToString(((WlanBssValue.chCenterFrequency - 2412000) / 1000 / 5 + 1));
            String securetype = "Unknow";
            String securety = "Unknow";
            String SignalQuality = "Unknow";
            int j = 0;
            for (j = 0; j < networks.Length; j++)
            {
                if (GetStringForSSID(WlanBssValue.dot11Ssid).Equals(GetStringForSSID(networks[j].dot11Ssid)))
                {
                    switch (networks[j].dot11DefaultCipherAlgorithm)
                    {
                        case Wlan.Dot11CipherAlgorithm.WEP:
                        case Wlan.Dot11CipherAlgorithm.WEP104:
                        case Wlan.Dot11CipherAlgorithm.WEP40:
                            securety = "WEP";
                            break;
                        case Wlan.Dot11CipherAlgorithm.None:
                            securety = "开放";
                            break;
                        case Wlan.Dot11CipherAlgorithm.CCMP:
                            securety = "CCMP";
                            break;
                        case Wlan.Dot11CipherAlgorithm.TKIP:
                            securety = "TKIP";
                            break;
                        default:
                            securety = "unknow";
                            break;
                    }
                    switch (networks[j].dot11DefaultAuthAlgorithm)
                    {
                        case Wlan.Dot11AuthAlgorithm.RSNA:
                            securetype = "WPA2 - 企业";
                            break;
                        case Wlan.Dot11AuthAlgorithm.RSNA_PSK:
                            securetype = "WPA2 - 个人";
                            break;
                        case Wlan.Dot11AuthAlgorithm.IEEE80211_SharedKey:
                            securetype = "IEEE80211_SharedKey";
                            break;
                        case Wlan.Dot11AuthAlgorithm.IEEE80211_Open:
                            securetype = "开放";
                            break;
                        case Wlan.Dot11AuthAlgorithm.WPA:
                            securetype = "WPA";
                            break;
                        case Wlan.Dot11AuthAlgorithm.WPA_None:
                            securetype = "WPA_None";
                            break;
                        case Wlan.Dot11AuthAlgorithm.WPA_PSK:
                            securetype = "WPA - 个人";
                            break;
                        default:
                            securety = "unknow";
                            break;
                    }
                    SignalQuality = networks[j].wlanSignalQuality.ToString() + "%";
                    break;
                }
            }
            APList += "身份验证                : " + securetype + "\r\n";
            APList += "加密                    : " + securety + "\r\n";
            APList += "BSSID 1                 : " + GetStringForBSSID(WlanBssValue) + "\r\n";
            APList += "信号                    : " + SignalQuality + "\r\n";
            APList += "无线电类型              :      802.11n\r\n";
            APList += "频道                    : " + channel + "\r\n";
            //StringBuilder sb = new StringBuilder();
            //foreach (ushort value in WlanBssValue.wlanRateSet.Rates)
            //sb.AppendFormat("{0:d}" + " ", value);
            APList += "基本速率(Mbps)          :          1 2 5.5 11\r\n";
            APList += "其他速率(Mbps)          :          28 24 36 48 54\r\n";
            APList += "RSSI                    : " + WlanBssValue.rssi.ToString() + "\r\n";
            APList += "\r\n";

            //System.IO.File.Delete("testlog.txt");
            //System.IO.File.AppendAllText("testlog.txt", APList);
        }

        return APList;
    }
    /// <summary>
    /// Parsing Netsh output
    /// </summary>
    /// <param name="netshOutput">Output from netsh command</param>
    /// <returns>List of networks details</returns>
    public List<NetworkDetails> ParseNetshOutput(string netshOutput)
    {
      List<NetworkDetails> _returnList = new List<NetworkDetails>();
      string[] netshLines = Regex.Split(netshOutput, "\r\n");

      int j = 0; // color counter

      for (int i = 0; i < netshLines.Length; i++)
      {
        var currentLine = netshLines[i];

        if (currentLine.StartsWith("SSID")) // found new network
        {
          string networkSSID = currentLine.Split(new char[] { ':' }, 2)[1].Trim();         
          string authentication = netshLines[i + 2].Split(new char[] { ':' }, 2)[1].Trim();
          string encryption = netshLines[i + 3].Split(new char[] { ':' }, 2)[1].Trim();
          if (encryption == "WEP")
            authentication = "WEP";
          string networkBSSID = netshLines[i + 4].Split(new char[] { ':' }, 2)[1].Trim();
          string signalStrengthPercent = netshLines[i + 5].Split(new char[] { ':' }, 2)[1].Trim();
          int signalStrength = Int32.Parse(signalStrengthPercent.Substring(0, signalStrengthPercent.Length - 1));
          string radioType = netshLines[i + 6].Split(new char[] { ':' }, 2)[1].Trim();
          int channel = Int32.Parse(netshLines[i + 7].Split(new char[] { ':' }, 2)[1].Trim());          
          string colorString = _colors[j % _colors.Length];
          j++;

          _returnList.Add(new NetworkDetails() { Show = true,
                                            Security = authentication,
                                            NetworkBSSID = networkBSSID,                                            
                                            Mode = radioType,
                                            NetworkNameChannelSignalColorCanvasWidthAndHeight = new Tuple<string, int, int, string, double, double>(networkSSID, channel, signalStrength, colorString, CanvasWidth, CanvasHeight)
          });

          i += 10;          
          continue;
        }
      }

      return _returnList;
    }

    #endregion
  }

  public class WindowCommand : ICommand
  {
    WindowCommandType _commandType;

    public WindowCommand(WindowCommandType commandType)
    {
      _commandType = commandType;
    }

    public bool CanExecute(object parameter)
    {
      return true;
    }

    public event EventHandler CanExecuteChanged;

    public void Execute(object parameter)
    {
      // Logic goes here
      if (_commandType == WindowCommandType.CloseWindow)
        ((Window)parameter).Close();
      else if (_commandType == WindowCommandType.MinimizeWindow)
        ((Window)parameter).WindowState = WindowState.Minimized;
      else if (_commandType == WindowCommandType.MaximizeWindow)
      {
        if (((Window)parameter).WindowState == WindowState.Normal)
          ((Window)parameter).WindowState = WindowState.Maximized;
        else if (((Window)parameter).WindowState == WindowState.Maximized)
          ((Window)parameter).WindowState = WindowState.Normal;
      }
    }
  }

  public class ScanningCommand : ICommand
  {
    private Action _action;

    public ScanningCommand(Action action)
    {
      _action = action;
    }

    public bool CanExecute(object parameter)
    {
      return true;
    }

    public event EventHandler CanExecuteChanged;

    public void Execute(object parameter)
    {
      // Logic goes here
      _action();      
    }    
  }  
}
