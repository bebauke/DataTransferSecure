using Xunit;
using DataTransferSecure.Views;
using DataTransferSecure.Controller;
using System.Threading.Tasks;

namespace DataTransferSecure.Tests
{
    public class MainFormTests
    {

        [Fact]
        public void T02_ShouldUpdatePortLabel_WhenSetPortMenuIsClicked()
        {
            // Arrange
            int expectedPort = 8080;
            MainForm form = new MainForm();
            MainFormController controller = new MainFormController(form);

            // Mocking user input
            Prompt.ShowDialog = (text, caption, defaultValue) => expectedPort.ToString();

            // Act
            form.SetPortMenu_Click(null, null); // Simulate menu click

            // Assert
            Assert.Equal($"Local Port: {expectedPort}", form.portLabel.Text);
        }



        [Fact]
        public void T06_ShouldShowError_WhenInvalidPortIsEntered()
        {
            // Arrange
            MainForm form = new MainForm();
            MainFormController controller = new MainFormController(form);

            // Mocking invalid port input
            Prompt.ShowDialog = (text, caption, defaultValue) => "Invalid Port";

            // Act
            form.SetPortMenu_Click(null, null); // Simulate menu click

            // Assert
            Assert.Equal("Local Port: 9000", form.portLabel.Text); // No update should occur
        }
    }
}
