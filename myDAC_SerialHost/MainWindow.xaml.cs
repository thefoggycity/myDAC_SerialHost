using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.IO;
using System.IO.Ports;
using Microsoft.Win32;

namespace myDAC_SerialHost
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        private bool IsConnected, IsSending;
        SerialPort spBoard;
        ThreadStart thrSendFunc;
        Thread thrSend;
        enum WaveForm
        {
            Sqr, Saw, Tri, Sin, Wav
        }
        WaveForm wfMode;
        byte[] SignalData;
        FileInfo WavFile;

        public MainWindow()
        {
            InitializeComponent();

            // Init Controls
            btnConn.IsEnabled = false;
            btnApply.IsEnabled = false;
            IsSending = false;
            cmbPortSel.ItemsSource = SerialPort.GetPortNames();
            optSqr.IsChecked = true;

            // Init Parameters
            IsConnected = false;
            IsSending = false;
            wfMode = WaveForm.Sqr;

            // Init Thread
            thrSendFunc = new ThreadStart(ContinuousSerialSend);
        }

        private void btnConn_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnected)
            {
                spBoard = new SerialPort(cmbPortSel.SelectedValue.ToString(), 115200, Parity.None, 8, StopBits.One);
                try
                {
                    spBoard.Open();
                    // TODO: INSERT HANDSHAKE CODE HERE
                    IsConnected = true;
                    btnApply.IsEnabled = true;
                    btnConn.Content = "Disconnect";
                    lblStatus.Content = "Connected to board on " + spBoard.PortName + ".";
                }
                catch
                {
                    lblStatus.Content = "Board connection failed.";
                }
            }
            else
            {
                spBoard.Close();
                spBoard.Dispose();
                spBoard = null;
                IsConnected = false;
                IsSending = false;
                btnApply.IsEnabled = false;
                btnConn.Content = "Connect";
                lblStatus.Content = "Disconnected from board.";
            }
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            bool Updated;
            try
            {
                if (thrSend != null)
                {
                    if (thrSend.IsAlive) thrSend.Abort();
                    thrSend = null;
                }
                thrSend = new Thread(thrSendFunc);
                Updated = UpdateData();      // Detect whether input has error
                IsSending = true;
                thrSend.Start();
                lblStatus.Content = Updated ? ( (wfMode == WaveForm.Wav) ?
                                    String.Format("Waveform updated to wave file {0:s}", WavFile.Name) : // WavFile will not be null if reach here
                                    String.Format("Waveform updated to {0:s} [{1:s},{2:s},{3:s}].",
                                    wfMode.ToString(), txtGrov.Text, txtPeak.Text, txtSamp.Text) ) :
                                    "Invaild waveform coefficients.";
            }
            catch
            {
                lblStatus.Content = "Fail to update wave";
                IsSending = (thrSend == null) ? false : thrSend.IsAlive;
            }
        }

        private void optSqrSawTriSin_Checked(object sender, RoutedEventArgs e)
        {
            if (optSqr.IsChecked == true) wfMode = WaveForm.Sqr;
            else if (optSaw.IsChecked == true) wfMode = WaveForm.Saw;
            else if (optTri.IsChecked == true) wfMode = WaveForm.Tri;
            else if (optSin.IsChecked == true) wfMode = WaveForm.Sin;
            else wfMode = WaveForm.Wav;
            stkAmp.IsEnabled = true;
            stkLen.IsEnabled = true;
        }

        private void optWav_Checked(object sender, RoutedEventArgs e)
        {
            stkAmp.IsEnabled = false;
            stkLen.IsEnabled = false;
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open wave (.wav) file";
            ofd.InitialDirectory = Environment.CurrentDirectory;
            ofd.CheckFileExists = true;
            ofd.Multiselect = false;
            ofd.Filter = "Wave file|*.wav|All file|*.*";
            if (ofd.ShowDialog(this) == true)
            {
                WavFile = new FileInfo(ofd.FileName);
                wfMode = WaveForm.Wav;
            }
            else
            {
                switch (wfMode)
                {
                    case WaveForm.Sqr: optSqr.IsChecked = true; break;
                    case WaveForm.Saw: optSaw.IsChecked = true; break;
                    case WaveForm.Tri: optTri.IsChecked = true; break;
                    case WaveForm.Sin: optSin.IsChecked = true; break;
                    default: optSqr.IsChecked = true; break;
                }
            }
        }

        private void cmbPortSel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPortSel.SelectedItem != null)
                btnConn.IsEnabled = true;
        }

        private void ContinuousSerialSend()
        {
            if (SignalData == null || spBoard == null || !spBoard.IsOpen ) return;
            while (true)
            {
                for (int i = 0; i < SignalData.Count(); i++)
                    spBoard.Write(SignalData, i, 1);
            }
        }

        private bool UpdateData()
        {
            if (wfMode == WaveForm.Wav)
            {
                // TODO: READ FROM FILE
                // DUMMY CODE BELOW
                SignalData = new byte[] { 0xff, 0x00 };
                return true;
            }
            else
            {
                byte Max, Min; 
                uint Length;
                if (!byte.TryParse(txtPeak.Text, out Max) || !byte.TryParse(txtGrov.Text, out Min) ||
                    !uint.TryParse(txtSamp.Text, out Length)) return false;
                if (Min > Max || Length == 0) return false;
                SignalData = new byte[Length];
                Single NormalizedT;
                for (int i = 0; i < Length; i++)
                {
                    NormalizedT = (Single)i / (Single)Length;
                    switch (wfMode)
                    {
                        case WaveForm.Sqr:
                            SignalData[i] = (NormalizedT >= 0.5) ? Max : Min;
                            break;
                        case WaveForm.Saw:
                            SignalData[i] = (byte)((Max - Min) * NormalizedT + Min);
                            break;
                        case WaveForm.Tri:
                            if (NormalizedT < 0.5)
                                SignalData[i] = (byte)((Max - Min) * NormalizedT * 2 + Min);
                            else
                                SignalData[i] = (byte)((Max - Min) * (1 - NormalizedT) * 2 + Min);
                            break;
                        case WaveForm.Sin:
                            SignalData[i] = (byte)(Math.Sin(NormalizedT * Math.PI * 2) * (Max - Min) * 0.5 + (Max + Min) * 0.5);
                            break;
                    }
                }
                return true;
            }
        }
    }
}
