using System;
using System.Windows.Forms;

namespace DataTransferSecure.Views
{
    public partial class SettingsForm : Form
    {
        public int LocalUdpPort { get; set; }
        public int DefaultUdpServerPort { get; set; }
        public int TcpServerPort { get; set; }
        public bool UseEncryption { get; set; }
        public bool UseCertificates { get; set; }
        public bool UseChecksum { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; } // New property for certificate password

        private TextBox localUdpPortTextBox;
        private TextBox defaultUdpServerPortTextBox;
        private TextBox tcpServerPortTextBox;
        private TextBox certificatePathTextBox;
        private TextBox certificatePasswordTextBox; // New TextBox for password
        private OpenFileDialog certificatePathDialog;
        private CheckBox encryptionCheckBox;
        private CheckBox certificatesCheckBox;
        private CheckBox checksumCheckBox;

        public SettingsForm()
        {
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            this.Text = "Einstellungen";
            this.Size = new System.Drawing.Size(400, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            var localUdpPortLabel = new Label { Text = "Lokaler UDP Port:", Left = 20, Top = 20, AutoSize = true };
            localUdpPortTextBox = new TextBox { Left = 150, Top = 20, Width = 200, Text = LocalUdpPort.ToString() };

            var defaultUdpServerPortLabel = new Label { Text = "Standart UDP Server Port:", Left = 20, Top = 60, AutoSize = true };
            defaultUdpServerPortTextBox = new TextBox { Left = 150, Top = 60, Width = 200, Text = DefaultUdpServerPort.ToString() };

            var tcpServerPortLabel = new Label { Text = "TCP Server Port:", Left = 20, Top = 100, AutoSize = true };
            tcpServerPortTextBox = new TextBox { Left = 150, Top = 100, Width = 200, Text = TcpServerPort.ToString() };

            var encryptionGroupBox = new GroupBox { Text = "Verschlüsselungstechnologie", Left = 20, Top = 140, Width = 350, Height = 120 };
            encryptionCheckBox = new CheckBox { Text = "AES", Left = 20, Top = 20, AutoSize = true, Checked = UseEncryption };
            certificatesCheckBox = new CheckBox { Text = "X509", Left = 100, Top = 20, AutoSize = true, Checked = UseCertificates };
            checksumCheckBox = new CheckBox { Text = "Checksum", Left = 180, Top = 20, AutoSize = true, Checked = UseChecksum };
            certificatePathDialog = new OpenFileDialog { Title = "Wählen Sie ein Zertifikat aus" };
            var certificatePathLabel = new Label { Text = "Zertifikatspfad:", Left = 20, Top = 60, AutoSize = true };
            certificatePathTextBox = new TextBox { Left = 110, Top = 55, Width = 140, ReadOnly = true, Text = CertificatePath };
            var browseButton = new Button { Text = "Durchsuchen...", Left = 255, Top = 55, Width = 80 };
            browseButton.Click += (sender, e) =>
            {
                if (certificatePathDialog.ShowDialog() == DialogResult.OK)
                {
                    CertificatePath = certificatePathDialog.FileName;
                    certificatePathTextBox.Text = CertificatePath;
                }
            };

            var certificatePasswordLabel = new Label { Text = "Zertifikatpasswort:", Left = 20, Top = 95, AutoSize = true };
            certificatePasswordTextBox = new TextBox { Left = 110, Top = 90, Width = 220, PasswordChar = '*', Text = CertificatePassword }; // Password


            // Enable/Disable certificate path and password fields based on certificates checkbox
            certificatesCheckBox.CheckedChanged += (sender, e) =>
            {
                certificatePathTextBox.Enabled = certificatesCheckBox.Checked;
                browseButton.Enabled = certificatesCheckBox.Checked;
                certificatePasswordTextBox.Enabled = certificatesCheckBox.Checked;
            };
            certificatePathTextBox.Enabled = certificatesCheckBox.Checked;
            browseButton.Enabled = certificatesCheckBox.Checked;
            certificatePasswordTextBox.Enabled = certificatesCheckBox.Checked;

            var saveButton = new Button { Text = "Speichern", Left = 20, Top = 310, Width = 80 };
            var cancelButton = new Button { Text = "Abbrechen", Left = 290, Top = 310, Width = 80 };

            saveButton.Click += (sender, e) =>
            {
                if (int.TryParse(localUdpPortTextBox.Text, out int localUdp) &&
                    int.TryParse(defaultUdpServerPortTextBox.Text, out int defaultUdpServer) &&
                    int.TryParse(tcpServerPortTextBox.Text, out int tcpServer))
                {
                    LocalUdpPort = localUdp;
                    DefaultUdpServerPort = defaultUdpServer;
                    TcpServerPort = tcpServer;
                    UseEncryption = encryptionCheckBox.Checked;
                    UseCertificates = certificatesCheckBox.Checked;
                    UseChecksum = checksumCheckBox.Checked;
                    CertificatePassword = certificatePasswordTextBox.Text;

                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Bitte geben Sie gültige Portnummern ein.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            cancelButton.Click += (sender, e) => this.Close();

            this.Controls.Add(localUdpPortLabel);
            this.Controls.Add(localUdpPortTextBox);
            this.Controls.Add(defaultUdpServerPortLabel);
            this.Controls.Add(defaultUdpServerPortTextBox);
            this.Controls.Add(tcpServerPortLabel);
            this.Controls.Add(tcpServerPortTextBox);
            this.Controls.Add(encryptionGroupBox);
            encryptionGroupBox.Controls.Add(encryptionCheckBox);
            encryptionGroupBox.Controls.Add(certificatesCheckBox);
            encryptionGroupBox.Controls.Add(checksumCheckBox);
            encryptionGroupBox.Controls.Add(certificatePathLabel);
            encryptionGroupBox.Controls.Add(certificatePathTextBox);
            encryptionGroupBox.Controls.Add(browseButton);
            encryptionGroupBox.Controls.Add(certificatePasswordLabel);
            encryptionGroupBox.Controls.Add(certificatePasswordTextBox);
            this.Controls.Add(saveButton);
            this.Controls.Add(cancelButton);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            localUdpPortTextBox.Text = LocalUdpPort.ToString();
            defaultUdpServerPortTextBox.Text = DefaultUdpServerPort.ToString();
            tcpServerPortTextBox.Text = TcpServerPort.ToString();
            encryptionCheckBox.Checked = UseEncryption;
            certificatesCheckBox.Checked = UseCertificates;
            checksumCheckBox.Checked = UseChecksum;
            certificatePathTextBox.Text = CertificatePath;
            certificatePasswordTextBox.Text = CertificatePassword;
        }
    }
}
