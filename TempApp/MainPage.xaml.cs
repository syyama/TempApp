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

        /// <summary>
        /// I2Cの初期処理
        /// </summary>
        private async void InitI2C()
        {
            // I2Cコントローラのオブジェクトを取得するための文字列をセット
            string aqs = I2cDevice.GetDeviceSelector();

            // デバイスに登録されているすべての周辺機器を列挙する
            var dis = await DeviceInformation.FindAllAsync(aqs);

            // I2Cコントローラが見つからない場合はエラーを返す
            if (dis.Count == 0)
            {
                Text_Status.Text = "No I2C controllers were found on the system";
                return;
            }

            // I2Cの接続設定を管理するオブジェクトの作成
            var settings = new I2cConnectionSettings(TEMP_I2C_ADDR);

            // 通信速度の設定
            settings.BusSpeed = I2cBusSpeed.FastMode;

            // I2Cに接続したデバイスのインスタンスを生成
            TEMP = await I2cDevice.FromIdAsync(dis[0].Id, settings);

            // デバイスが見つからない場合はエラーを返す
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

        /// <summary>
        /// DispatcherTimerオブジェクトperiodicTimerの
        /// Intervalプロパティに設定した周期に呼ばれるメソッド
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, object e)
        {
            double temp = readTemp();

            TempBox.Text = string.Format(" {0:f2}℃", temp);
            Text_Status.Text = "状態: サンプリング回数 = " + samplingCount + ")";
            samplingCount++;
        }

        /// <summary>
        /// I2cDeviceオブジェクトのWriteReadメソッドを使用し
        /// 温度センサーから温度を取り出すメソッド
        /// </summary>
        /// <returns></returns>
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
