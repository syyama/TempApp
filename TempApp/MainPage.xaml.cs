using System;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace TempApp
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const byte TEMP_I2C_ADDR = 0x48;
        private const byte TEMP_REG_MSB = 0x00;
        private const byte TEMP_REG_CONFIG = 0x03;

        private const int TEMP_INTERVAL = 500;
        private I2cDevice TEMP;

        private DispatcherTimer periodicTimer;
        private int samplingCount = 0;

        public MainPage()
        {
            this.InitializeComponent();

            Unloaded += Mainpage_Unloaded;

            InitI2C();
        }

        private void Mainpage_Unloaded(object sender, RoutedEventArgs e)
        {
            TEMP.Dispose();
        }

        private async void InitI2C()
        {
            string aqs = I2cDevice.GetDeviceSelector();
            var dis = await DeviceInformation.FindAllAsync(aqs);

            if (dis.Count == 0)
            {
                Text_Status.Text = "No I2C controllers were found on the system";
                return;
            }

            var settings = new I2cConnectionSettings(TEMP_I2C_ADDR);
            settings.BusSpeed = I2cBusSpeed.FastMode;
            TEMP = await I2cDevice.FromIdAsync(dis[0].Id, settings);
            if(TEMP == null)
            {
                Text_Status.Text = string.Format(
                    "Slave address {0} on I2C Controller {1} is currently in use by " +
                    "another application. Please ensure that no other applications are using I2c",
                    settings.SlaveAddress,
                    dis[0].Id);
                return;
            }

            // 16ビットモード
            byte[] WriteBufConfig = new byte[] { TEMP_REG_CONFIG, 0x80 };

            // コンフィグ書き込み
            try
            {
                TEMP.Write(WriteBufConfig);
            }
            catch (Exception ex)
            {
                Text_Status.Text = "Cannot configuration service: " + ex.Message;
                return;
            }

            // no ready to temprature. create a timer to read data.
            periodicTimer = new DispatcherTimer();
            periodicTimer.Interval = TimeSpan.FromMilliseconds(TEMP_INTERVAL);
            periodicTimer.Tick += Timer_Tick;
            periodicTimer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            double temp = readTemp();

            TempBox.Text = string.Format(" {0:f2}℃", temp);
            Text_Status.Text = "状態: サンプリング回数 = " + samplingCount + ")";
            samplingCount++;
        }

        private double readTemp()
        {
            // Register address to read
            byte[] RegAddrBuf = new byte[] { TEMP_REG_MSB };
            byte[] buffer = new byte[2];
            TEMP.WriteRead(RegAddrBuf, buffer);

            // エンコード
            double temprature;
            if ((buffer[0] & 0x80) == 0)
                temprature = (buffer[0] * 256.0 + buffer[1]) / 128.0;
            else
                temprature = (buffer[0] * 256.0 + buffer[1] - 65536.0) / 128.0;

            return Math.Round(temprature, 2);
        }
    }
}
