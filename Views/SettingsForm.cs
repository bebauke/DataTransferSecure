using System;
using System.Windows.Forms;

namespace DataTransferSecure.Views
{
    public partial class SettingsForm : Form
    {
        // Properties to hold the user inputs
        public int LocalUdpPort { get; set; }
        public int DefaultUdpServerPort { get; set; }
        public int TcpServerPort { get; set; }
        public bool UseEncryption { get; set; }
        public bool UseCertificates { get; set; }
        public bool UseChecksum { get; set; }

        private TextBox localUdpPortTextBox;
        private TextBox defaultUdpServerPortTextBox;
        private TextBox tcpServerPortTextBox;
        private CheckBox encryptionCheckBox;
        private CheckBox certificatesCheckBox;
        private CheckBox checksumCheckBox;

        // Constructor
        public SettingsForm()
        {
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            // Set form properties
            this.Text = "Einstellungen";
            this.Size = new System.Drawing.Size(400, 290);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Labels and TextBoxes for ports
            var localUdpPortLabel = new Label { Text = "Lokaler UDP Port:", Left = 20, Top = 20, AutoSize = true };
            localUdpPortTextBox = new TextBox { Left = 150, Top = 20, Width = 200, Text = LocalUdpPort.ToString() };

            var defaultUdpServerPortLabel = new Label { Text = "Standart UDP Server Port:", Left = 20, Top = 60, AutoSize = true };
            defaultUdpServerPortTextBox = new TextBox { Left = 150, Top = 60, Width = 200, Text = DefaultUdpServerPort.ToString() };

            var tcpServerPortLabel = new Label { Text = "TCP Server Port:", Left = 20, Top = 100, AutoSize = true };
            tcpServerPortTextBox = new TextBox { Left = 150, Top = 100, Width = 200, Text = TcpServerPort.ToString() };

            // GroupBox for encryption technology
            var encryptionGroupBox = new GroupBox { Text = "Verschlüsselungstechnologie", Left = 20, Top = 140, Width = 350, Height = 50 };
            encryptionCheckBox = new CheckBox { Text = "AES", Left = 20, Top = 20, AutoSize = true, Checked = UseEncryption };
            certificatesCheckBox = new CheckBox { Text = "X509", Left = 100, Top = 20, AutoSize = true, Checked = UseCertificates };
            checksumCheckBox = new CheckBox { Text = "Checksum", Left = 180, Top = 20, AutoSize = true, Checked = UseChecksum };

            // Save and Cancel buttons
            var saveButton = new Button { Text = "Speichern", Left = 20, Top = 200, Width = 80 };
            var cancelButton = new Button { Text = "Abbrechen", Left = 290, Top = 200, Width = 80 };

            // Add event handlers
            saveButton.Click += (sender, e) =>
            {
                // Validate and save the user inputs
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

                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Bitte geben Sie gültige Portnummern ein.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            cancelButton.Click += (sender, e) => this.Close();

            // Add controls to the form
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
            this.Controls.Add(saveButton);
            this.Controls.Add(cancelButton);
        }

        // Load existing values into the UI controls when the form is shown
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            localUdpPortTextBox.Text = LocalUdpPort.ToString();
            defaultUdpServerPortTextBox.Text = DefaultUdpServerPort.ToString();
            tcpServerPortTextBox.Text = TcpServerPort.ToString();
            encryptionCheckBox.Checked = UseEncryption;
            certificatesCheckBox.Checked = UseCertificates;
            checksumCheckBox.Checked = UseChecksum;
        }
    }
}
