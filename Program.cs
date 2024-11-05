using DataTransferSecure.Views;
using System;
using System.Windows.Forms;

namespace DataTransferSecure
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Variablen für die Verbindungsinformationen deklarieren


            // Führe MainForm aus
            Application.Run(new MainForm());
        }
    }
}
