using System;
using System.ComponentModel;
using System.Management;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Newtonsoft.Json;
using System.Net.NetworkInformation;

namespace SmartScale
{
    public enum MachineMode
    {
        Auto,
        Manual
    }

    public partial class frmTimbang : Form
    {
        private SerialPort comScale;
        private SerialPort comArduino;
        private DateTime emptyTimer;
        private MachineMode currentMode = MachineMode.Auto;
        private Timer timerSimulasi;
        private Timer timerSave;
        private bool isConnected = false;
        private IMqttClient mqttClient;
        private int currentShift = 0;
        private int setPoint = 100;
        private int peakWeight = 0;
        private bool cycleActive = false;
        private string nikValue = "";
        private readonly string[] nikDiizinkan = { "PRA00409", "PJG00669", "PIP00181" };

        private const int FINAL_DUMP_WEIGHT = 45;
        private const int FINAL_DUMP_TIME = 3;

        private bool waitingNextCycle = false;
        private DateTime emptyStartTime;
        private bool finalDumpActive = false;
        private DateTime finalDumpStart;

        private int lastWeight = -1;
        private DateTime lastChangeTime;
        private const int STABLE_SECONDS = 5;
        private bool dataSaved = false;
        private int savedWeight = 0;
        private int currentWeight = 0;
        private int lastWeightBeforeEmpty = 0;
        private const int EMPTY_THRESHOLD = 1;

        // ===== WARNA TEMA HIJAU INDUSTRIAL =====
        private readonly Color clrBgDark = ColorTranslator.FromHtml("#0a1208");
        private readonly Color clrBgMid = ColorTranslator.FromHtml("#0d1a0a");
        private readonly Color clrBgPanel = ColorTranslator.FromHtml("#0b150a");
        private readonly Color clrAccent = ColorTranslator.FromHtml("#5DCA85");
        private readonly Color clrTextMuted = ColorTranslator.FromHtml("#2e5035");
        private readonly Color clrTextMain = ColorTranslator.FromHtml("#a0e8b8");
        private readonly Color clrGreen = ColorTranslator.FromHtml("#1d9e55");
        private readonly Color clrRed = ColorTranslator.FromHtml("#2a1010");
        private readonly Color clrRedText = ColorTranslator.FromHtml("#ca5d5d");
        private readonly Color clrBorder = ColorTranslator.FromHtml("#1e3a1a");

        // =====================================================================

        private void ApplyTheme()
        {
            // === FORM ===
            this.BackColor = clrBgDark;

            // === MAIN CONTAINER ===
            groupBox2.BackColor = clrBgDark;
            groupBox2.BackgroundImage = null;
            groupBox2.ForeColor = clrTextMain;

            // === ANGKA TIMBANGAN ===
            labelData.BackColor = clrBgDark;
            labelData.ForeColor = clrAccent;
            labelData.TextAlign = ContentAlignment.MiddleCenter;

            // === LOG BOX ===
            rtbLog.BackColor = ColorTranslator.FromHtml("#060e05");
            rtbLog.ForeColor = clrAccent;

            // === DATETIME ===
            lblDateTime.ForeColor = clrTextMuted;
            lblDateTime.BackColor = Color.Transparent;

            // === GROUPBOX 1 — Serial Port, Arduino, Baud, NIK, Shift ===
            groupBox1.BackColor = clrBgPanel;
            groupBox1.ForeColor = clrTextMain;
            foreach (Control ctrl in groupBox1.Controls)
            {
                if (ctrl is Button btn)
                {
                    btn.BackColor = clrBgMid;
                    btn.ForeColor = clrTextMuted;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = clrBorder;
                }
                else if (ctrl is ComboBox cmb)
                {
                    cmb.BackColor = clrBgMid;
                    cmb.ForeColor = clrTextMain;
                }
                else if (ctrl is Label lbl)
                {
                    lbl.BackColor = clrBgPanel;
                    lbl.ForeColor = clrTextMuted;
                }
            }
            // NIK khusus sedikit lebih terang biar keliatan bisa diketik
            nikPengawas.BackColor = ColorTranslator.FromHtml("#112010");
            nikPengawas.ForeColor = clrTextMain;

            // === GROUPBOX 3 — Connect / Disconnect / Mode ===
            groupBox3.BackColor = clrBgPanel;
            groupBox3.ForeColor = clrTextMain;
            label6.BackColor = clrBgPanel; label6.ForeColor = clrTextMuted;
            label7.BackColor = clrBgPanel; label7.ForeColor = clrTextMuted;
            radioAuto.BackColor = clrBgPanel; radioAuto.ForeColor = clrAccent;
            radioManual.BackColor = clrBgPanel; radioManual.ForeColor = clrTextMain;
            btnConnect.BackColor = clrGreen;
            btnConnect.ForeColor = Color.White;
            btnDisconnect.BackColor = clrRed;
            btnDisconnect.ForeColor = clrRedText;

            // === GROUPBOX 4 — Auto Mode / SetPoint ===
            groupBox4.BackColor = clrBgPanel;
            groupBox4.ForeColor = clrTextMain;
            label4.BackColor = clrBgPanel; label4.ForeColor = clrTextMuted;
            label8.BackColor = clrBgPanel; label8.ForeColor = clrAccent;
            cmbInterval.BackColor = clrBgMid; cmbInterval.ForeColor = clrTextMain;

            // === GROUPBOX MANUAL ===
            groupManual.BackColor = clrBgPanel;
            groupManual.ForeColor = clrTextMain;
            label9.BackColor = clrBgPanel; label9.ForeColor = clrAccent;
            radioManualOpen.BackColor = clrBgPanel; radioManualOpen.ForeColor = clrAccent;
            radioManualClose.BackColor = clrBgPanel; radioManualClose.ForeColor = clrRedText;
            btnManualSend.BackColor = clrGreen;
            btnManualSend.ForeColor = Color.White;
            btnManualSend.FlatStyle = FlatStyle.Flat;
            btnManualSend.FlatAppearance.BorderColor = clrBorder;
        }

        private void SetMode(MachineMode mode)
        {
            currentMode = mode;

            if (mode == MachineMode.Auto)
            {
                radioAuto.Checked = true;
                radioManual.Checked = false;
                groupManual.Enabled = false;
                AddLog("Auto Mode");
            }
            else if (mode == MachineMode.Manual)
            {
                radioAuto.Checked = false;
                radioManual.Checked = true;
                groupManual.Enabled = true;
                AddLog("Manual Mode");
            }

            if (comArduino != null && comArduino.IsOpen)
                SendToArduino("CLOSE");
        }

        public frmTimbang()
        {
            InitializeComponent();
            PopulateCombobox();
            UpdateConnectButton();
            timerSave = new Timer();
            timerSimulasi = new Timer();
            timerSave.Interval = 3000;
            timerSave.Tick += TimerSave_Tick;
        }

        private void TimerSave_Tick(object sender, EventArgs e) { }

        private void radioAuto_CheckedChanged(object sender, EventArgs e) { }
        private void radioManual_CheckedChanged(object sender, EventArgs e) { }

        private void ConnectToArduino()
        {
            try
            {
                if (comArduino != null && comArduino.IsOpen)
                {
                    AddLog("Arduino sudah connect");
                    return;
                }

                if (cmbStopBits.SelectedItem == null)
                {
                    AddLog("Pilih Arduino Port dulu!");
                    return;
                }

                string portName = cmbStopBits.SelectedItem.ToString();
                comArduino = new SerialPort(portName, 9600);
                comArduino.ReadTimeout = 2000;
                comArduino.WriteTimeout = 2000;
                comArduino.DtrEnable = true;
                comArduino.RtsEnable = true;
                comArduino.Open();

                System.Threading.Thread.Sleep(2000);
                AddLog("Arduino connected di " + portName);

                SendToArduino("CLOSE");
                System.Threading.Thread.Sleep(200);
                SendToArduino("CLOSE");
                AddLog("Gate default CLOSE");

                cycleActive = false;
                finalDumpActive = false;
                peakWeight = 0;
            }
            catch (Exception ex)
            {
                AddLog("Arduino connect error: " + ex.Message);
            }
        }

        private void SetShift(int shift)
        {
            currentShift = shift;
        }

        private async Task ConnectMqtt()
        {
            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer("192.168.12.226", 1883)
                .WithClientId("smartscale")
                .WithCleanSession()
                .Build();

            try
            {
                await mqttClient.ConnectAsync(options);
            }
            catch (Exception ex)
            {
                AddLog("MQTT ERROR: " + ex.Message);
            }
        }

        private async void PublishWeightToMQTT(double beratKg, int shift)
        {
            try
            {
                if (mqttClient == null || !mqttClient.IsConnected)
                {
                    AddLog("MQTT reconnect...");
                    await ConnectMqtt();
                    if (mqttClient == null || !mqttClient.IsConnected)
                    {
                        AddLog("MQTT gagal reconnect");
                        return;
                    }
                }

                var payload = new
                {
                    shift = shift,
                    berat = $"{beratKg} kg",
                    nik_pengawas = nikValue,
                    waktu = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string json = JsonConvert.SerializeObject(payload);

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic("smartscale/ROPMP1")
                    .WithPayload(json)
                    .WithAtLeastOnceQoS()
                    .Build();

                await mqttClient.PublishAsync(message);
                AddLog($"Terkirim | {beratKg} KG | Shift {shift} | NIK: {nikValue}");
            }
            catch (Exception ex)
            {
                AddLog("MQTT ERROR: " + ex.Message);
            }
        }

        private int CleanWeight(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;

            string numberPart = "";
            foreach (char c in raw)
            {
                if (char.IsDigit(c))
                    numberPart += c;
                else if (numberPart.Length > 0)
                    break;
            }

            if (string.IsNullOrEmpty(numberPart)) return 0;
            int.TryParse(numberPart, out int result);
            return result;
        }

        public void ConnectToScale(string portName, int baudRate)
        {
            comScale = new SerialPort();
            comScale.PortName = portName;
            comScale.BaudRate = baudRate;
            comScale.StopBits = StopBits.One;
            comScale.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

            try
            {
                comScale.Open();
                isConnected = true;
                UpdateConnectButton();
                this.Invoke(new Action(() => AddLog("Log : Connected ...")));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() => AddLog("Log : " + ex.Message)));
                isConnected = false;
                UpdateConnectButton();
            }
        }

        private void PopulateCombobox()
        {
            string[] portNames = SerialPort.GetPortNames();

            cmbPort.Items.Clear();
            cmbBaudRate.Items.Clear();
            cmbStopBits.Items.Clear();
            cmbInterval.Items.Clear();

            foreach (string p in portNames) { cmbPort.Items.Add(p); cmbStopBits.Items.Add(p); }
            if (cmbPort.Items.Count > 0) cmbPort.SelectedIndex = 0;

            foreach (string b in new[] { "9600", "19200", "38400", "57600", "115200" })
                cmbBaudRate.Items.Add(b);
            cmbBaudRate.SelectedItem = "9600";

            foreach (string sp in new[] { "100", "150", "200" })
                cmbInterval.Items.Add(sp);
            cmbInterval.SelectedItem = "100";
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadLine();
            UpdateTextBox(indata);
        }

        private void UpdateTextBox(string data)
        {
            if (labelData.InvokeRequired)
            {
                labelData.Invoke(new Action<string>(UpdateTextBox), data);
                return;
            }

            int weight = CleanWeight(data);
            labelData.Text = weight.ToString();
            currentWeight = weight;

            if (currentWeight > EMPTY_THRESHOLD)
                lastWeightBeforeEmpty = currentWeight;

            if (cycleActive && currentWeight > peakWeight)
                peakWeight = currentWeight;

            if (currentMode != MachineMode.Auto) return;

            if (waitingNextCycle)
            {
                if ((DateTime.Now - emptyStartTime).TotalSeconds < FINAL_DUMP_TIME) return;
                waitingNextCycle = false;
            }

            if (currentWeight >= setPoint && !cycleActive)
            {
                cycleActive = true;
                finalDumpActive = false;
                peakWeight = currentWeight;
                SendToArduino("START");
            }

            if (cycleActive && currentWeight > peakWeight)
                peakWeight = currentWeight;

            if (cycleActive && !finalDumpActive && currentWeight <= FINAL_DUMP_WEIGHT)
            {
                finalDumpActive = true;
                SendToArduino("OPEN");
                finalDumpStart = DateTime.Now;
            }

            if (finalDumpActive)
            {
                if ((DateTime.Now - finalDumpStart).TotalSeconds >= FINAL_DUMP_TIME)
                {
                    SendToArduino("CLOSE");
                    finalDumpActive = false;
                }
            }

            if (cycleActive && currentWeight <= EMPTY_THRESHOLD)
            {
                SendToArduino("STOP");

                cycleActive = false;
                finalDumpActive = false;
                waitingNextCycle = true;
                emptyStartTime = DateTime.Now;

                nikValue = nikPengawas.Text.Trim().ToUpper();

                double beratKg = peakWeight * 1.0;
                PublishWeightToMQTT(beratKg, currentShift);
                peakWeight = 0;
            }
        }

        private void UpdateConnectButton()
        {
            if (isConnected)
            {
                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;
                btnConnect.BackColor = clrGreen;
                btnDisconnect.BackColor = clrRed;
                btnConnect.ForeColor = Color.White;
                btnDisconnect.ForeColor = clrRedText;
            }
            else
            {
                btnConnect.Enabled = true;
                btnDisconnect.Enabled = false;
                btnConnect.BackColor = clrGreen;
                btnDisconnect.BackColor = clrRed;
                btnConnect.ForeColor = Color.White;
                btnDisconnect.ForeColor = clrRedText;
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (isConnected) return;

            nikValue = nikPengawas.Text.Trim().ToUpper();

            if (string.IsNullOrEmpty(nikValue))
            {
                MessageBox.Show("NIK Pengawas belum diisi!", "Peringatan",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nikPengawas.Focus();
                return;
            }

            if (!Array.Exists(nikDiizinkan, nik => nik == nikValue))
            {
                MessageBox.Show($"NIK '{nikValue}' tidak diizinkan!\nHubungi ITD.",
                    "Akses Ditolak", MessageBoxButtons.OK, MessageBoxIcon.Error);
                nikPengawas.Text = "";
                nikPengawas.Focus();
                return;
            }

            if (cmbPort.SelectedItem == null)
            {
                MessageBox.Show("Pilih PORT timbangan dulu!");
                return;
            }

            if (cmbBaudRate.SelectedItem == null)
            {
                MessageBox.Show("Pilih Baudrate dulu!");
                return;
            }

            string scalePort = cmbPort.SelectedItem.ToString();
            int baudRate = int.Parse(cmbBaudRate.SelectedItem.ToString());

            AddLog($"NIK {nikValue} diizinkan - Connecting...");
            ConnectToArduino();
            ConnectToScale(scalePort, baudRate);
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (comScale != null && comScale.IsOpen)
                {
                    comScale.DataReceived -= DataReceivedHandler;
                    comScale.Close();
                }
            }
            catch { }

            isConnected = false;
            timerSimulasi.Stop();
            timerSave.Stop();
            labelData.Text = "0";
            AddLog("Log : Disconnected");
            UpdateConnectButton();

            if (comArduino != null && comArduino.IsOpen)
                comArduino.Close();
        }

        private void SendToArduino(string command)
        {
            if (comArduino == null || !comArduino.IsOpen)
            {
                AddLog("Arduino belum connect, coba connect ulang...");
                ConnectToArduino();
                if (comArduino == null || !comArduino.IsOpen)
                {
                    AddLog("Gagal connect ke Arduino");
                    return;
                }
            }

            try
            {
                comArduino.WriteLine(command);
            }
            catch (Exception ex)
            {
                AddLog("Error kirim ke Arduino: " + ex.Message);
            }
        }

        private void cmbInterval_SelectionChangeCommitted(object sender, EventArgs e)
        {
            setPoint = int.Parse(cmbInterval.SelectedItem.ToString());
            SendToArduino("CLOSE");
        }

        private void labelData_Click(object sender, EventArgs e) { }
        private void label5_Click(object sender, EventArgs e) { }

        private async void frmTimbang_Load(object sender, EventArgs e)
        {
            ApplyTheme();
            SetMode(MachineMode.Auto);
            await ConnectMqtt();

            if (isConnected)
            {
                timerSimulasi.Start();
                timerSave.Start();
            }
        }

        private async void SaveWeight(double beratKg)
        {
            if (mqttClient == null || !mqttClient.IsConnected)
            {
                AddLog("Gagal save: MQTT belum connect");
                return;
            }

            var payload = new
            {
                berat = $"{beratKg} kg",
                waktu = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                shift = currentShift
            };

            string json = JsonConvert.SerializeObject(payload);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("smartscale/ROPMP1")
                .WithPayload(json)
                .WithAtLeastOnceQoS()
                .Build();

            await mqttClient.PublishAsync(message);
            AddLog($"DATA DISIMPAN : {beratKg} kg (SHIFT {currentShift})");
        }

        private async void SendSavedDataToMQTT()
        {
            if (!dataSaved) { AddLog("Belum ada data tersimpan"); return; }
            if (mqttClient == null || !mqttClient.IsConnected) { AddLog("MQTT belum terhubung"); return; }

            double beratKg = savedWeight / 1000.0;
            var payload = new { berat = $"{beratKg} kg", shift = currentShift, waktu = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            string json = JsonConvert.SerializeObject(payload);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("smartscale/ROPMP1")
                .WithPayload(json)
                .WithAtLeastOnceQoS()
                .Build();

            await mqttClient.PublishAsync(message);
            AddLog($"DATA TERKIRIM : {beratKg} kg | SHIFT {currentShift}");
            dataSaved = false;
        }

        private void AddLog(string message)
        {
            if (rtbLog.InvokeRequired)
            {
                rtbLog.Invoke(new Action<string>(AddLog), message);
                return;
            }

            string logText = DateTime.Now.ToString("HH:mm:ss") + " - " + message + Environment.NewLine;
            rtbLog.AppendText(logText);
            rtbLog.SelectionStart = rtbLog.Text.Length;
            rtbLog.ScrollToCaret();
        }

        private void label3_Click(object sender, EventArgs e) { }
        private void label1_Click(object sender, EventArgs e) { }
        private void label5_Click_1(object sender, EventArgs e) { }

        private void timer1_Tick(object sender, EventArgs e)
        {
            lblDateTime.Text = DateTime.Now.ToString("dd MMMM yyyy | HH:mm:ss");
        }

        private void pictureBox2_Click(object sender, EventArgs e) { }
        private void pictureBox3_Click(object sender, EventArgs e) { }

        private void button1_Click(object sender, EventArgs e) { SetShift(1); AddLog("Shift 1 aktif"); }
        private void button2_Click(object sender, EventArgs e) { SetShift(2); AddLog("Shift 2 aktif"); }
        private void button3_Click(object sender, EventArgs e) { SetShift(3); AddLog("Shift 3 aktif"); }

        private void groupBox1_Enter(object sender, EventArgs e) { }
        private void button4_Click(object sender, EventArgs e) { }
        private void vScrollBar1_Scroll(object sender, ScrollEventArgs e) { }
        private void label6_Click(object sender, EventArgs e) { }
        private void panel1_Paint(object sender, PaintEventArgs e) { }

        private void button5_Click(object sender, EventArgs e)
        {
            if (currentMode != MachineMode.Manual)
            {
                AddLog("Mode bukan MANUAL!");
                return;
            }

            if (currentShift == 0)
            {
                AddLog("Gagal kirim: SHIFT belum dipilih");
                return;
            }

            nikValue = nikPengawas.Text.Trim().ToUpper();

            int beratGram = currentWeight;
            if (beratGram <= 0)
            {
                AddLog("Gagal kirim: berat = 0");
                return;
            }

            double beratKg = beratGram * 1.0;
            PublishWeightToMQTT(beratKg, currentShift);
            AddLog($"DATA DIKIRIM MANUAL: {beratKg} kg | SHIFT {currentShift} | NIK: {nikValue}");
        }

        private void pictureBox3_Click_1(object sender, EventArgs e) { }

        private void Clear_Click(object sender, EventArgs e)
        {
            rtbLog.Clear();
        }

        private void cmbInterval_SelectedIndexChanged(object sender, EventArgs e) { }
        private void groupBox3_Enter(object sender, EventArgs e) { }
        private void label6_Click_1(object sender, EventArgs e) { }
        private void label8_Click(object sender, EventArgs e) { }

        private void radioAuto_CheckedChanged_1(object sender, EventArgs e)
        {
            if (!radioAuto.Checked) return;
            currentMode = MachineMode.Auto;
            groupBox4.Enabled = true;
            groupManual.Enabled = false;
            AddLog("Mode AUTO aktif");
        }

        private void radioManual_CheckedChanged_1(object sender, EventArgs e)
        {
            if (!radioManual.Checked) return;
            currentMode = MachineMode.Manual;
            groupBox4.Enabled = false;
            groupManual.Enabled = true;
            AddLog("Mode MANUAL aktif");
        }

        private void pictureBox2_Click_1(object sender, EventArgs e) { }

        private void radioManualOpen_Click(object sender, EventArgs e)
        {
            if (!radioManualOpen.Checked) return;
            if (currentMode != MachineMode.Manual) { AddLog("Mode bukan MANUAL"); return; }
            SendToArduino("OPEN");
            AddLog("Gate Terbuka");
        }

        private void radioManualClose_Click(object sender, EventArgs e)
        {
            if (!radioManualClose.Checked) return;
            if (currentMode != MachineMode.Manual) { AddLog("Mode bukan MANUAL"); return; }
            SendToArduino("CLOSE");
            AddLog("Gate Tertutup");
        }

        private void label10_Click(object sender, EventArgs e) { }
        private void txtnikPengawas_SelectedIndexChanged(object sender, EventArgs e) { }
    }
}