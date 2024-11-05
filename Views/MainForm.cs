using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DataTransferSecure.Controller;

namespace DataTransferSecure.Views
{
    public partial class MainForm : Form
    {
        // UI-Elemente
        internal RichTextBox chatBox;
        internal TextBox messageInput;
        internal Button sendButton;
        internal StatusStrip statusStrip;
        internal ToolStripStatusLabel statusLabel;
        internal ToolStripStatusLabel ipLabel;
        internal ToolStripStatusLabel portLabel;
        internal MenuStrip menuStrip;
        internal MainFormController controller;

        public MainForm()
        {
            InitializeComponent();
            SetupUI();
            controller = new MainFormController(this);

            // Fenstergröße festlegen und Anpassung verhindern
            this.FormBorderStyle = FormBorderStyle.FixedDialog; // Verhindert Größenänderung
            this.MaximizeBox = false; // Deaktiviert die Maximierungsschaltfläche
        }

        internal void SetupUI()
        {
            this.Text = "Secure Data Transfer - Chat";
            this.Size = new Size(800, 520);

            // Menüleiste
            menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("Datei");
            var setPortMenu = new ToolStripMenuItem("Ports", null, SetPortMenu_Click);
            var connectMenu = new ToolStripMenuItem("Verbinden (Sicher)", null, async (sender, e) => await controller.Connect(encryptet: true));
            var connectUnencryptedMenu = new ToolStripMenuItem("Verbinden", null, async (sender, e) => await controller.Connect(encryptet: false));
            var disconnectMenu = new ToolStripMenuItem("Verbindung trennen", null, (sender, e) => controller.Disconnect());

            fileMenu.DropDownItems.AddRange(new[] { setPortMenu, connectMenu, connectUnencryptedMenu, disconnectMenu });
            menuStrip.Items.Add(fileMenu);
            this.Controls.Add(menuStrip);

            // ChatBox
            chatBox = new RichTextBox { Location = new Point(10, 40), Size = new Size(760, 370), ReadOnly = true };
            this.Controls.Add(chatBox);

            // Nachrichteneingabe
            messageInput = new TextBox { Location = new Point(10, 425), Size = new Size(660, 40) };
            this.Controls.Add(messageInput);

            // Senden-Button
            sendButton = new Button { Text = "Senden", Location = new Point(680, 420), Size = new Size(90, 30) };
            sendButton.Click += async (sender, e) => await controller.SendMessage(messageInput.Text);
            this.Controls.Add(sendButton);

            // Statusanzeige
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel { Text = "Status: Getrennt", Width = 500 };
            ipLabel = new ToolStripStatusLabel { Text = $" ", Width = 400 };
            portLabel = new ToolStripStatusLabel { Text = $" ", Width = 400 };
            statusStrip.Items.AddRange(new[] { statusLabel, ipLabel, portLabel });
            this.Controls.Add(statusStrip);
        }

        internal void SetPortMenu_Click(object sender, EventArgs e)
        {
            // udpPort, udpServerPort, tcpPort };
            string input = Prompt.ShowDialog("Lokaler UDP Port:", "UDP Port", controller.GetUDPPort().ToString());
            controller.SetUDPPort(input);
            input = Prompt.ShowDialog("Standart UDP Server Port:", "UDP Server Port", controller.GetUDPServerPort().ToString());
            controller.SetUDPServerPort(input);
            input = Prompt.ShowDialog("TCP Server Port:", "TCP Server Port", controller.GetTCPServerPort().ToString());
            controller.SetTCPServerPort(input);
        }

        // UI-Methoden
        internal void UpdateStatus(string status) => statusLabel.Text = $"Status: {status}";
        internal void AppendMessage(string message) => chatBox.AppendText($"{message}\n");
        internal void ShowError(string errorMessage) => MessageBox.Show(errorMessage, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
        internal void UpdateIPs(string localIP, string remoteIP) => ipLabel.Text = $"{localIP} => {remoteIP}";
        internal void UpdatePort(int port) => portLabel.Text = $"Local Port: {port}";
        internal void UpdateDefaultServerPort(int port) => portLabel.Text = $"Default Server Port: {port}";

        internal void AppendMessage(string message, Color? color = null)
        {
            if (color == null)
            {
                color = Color.Black;
            }

            chatBox.SelectionStart = chatBox.TextLength;
            chatBox.SelectionLength = 0;
            chatBox.SelectionColor = color.Value;

            chatBox.AppendText($"{message}\n");
            chatBox.SelectionColor = chatBox.ForeColor; // Farbe zurücksetzen
        }

        public void SetChatBoxColor(Color color)
        {
            chatBox.BackColor = color;
        }

    }
    // Eingabeaufforderung (Prompt)
    public static class Prompt
    {
        // Delegate, der in Tests überschrieben werden kann, um eine Mocking-Funktion zu verwenden
        public static Func<string, string, string, string> ShowDialog = (text, caption, defaultValue) =>
        {
            Form prompt = new Form
            {
                Width = 300,
                Height = 130,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label { Left = 10, Top = 5, Text = text, AutoSize = true };
            TextBox inputBox = new TextBox { Left = 10, Top = 25, Width = 260, Text = defaultValue }; // Setze den Standardwert
            Button confirmation = new Button { Text = "OK", Left = 200, Width = 60, Top = 50, DialogResult = DialogResult.OK };

            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.AddRange(new Control[] { textLabel, inputBox, confirmation });
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? inputBox.Text : string.Empty;
        };
    }


}
