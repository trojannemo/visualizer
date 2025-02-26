namespace Visualizer;

public partial class AboutPage : ContentPage
{
    public AboutPage(string message)
    {
        InitializeComponent();
        aboutText.Text = "\n" + message;
    }
}