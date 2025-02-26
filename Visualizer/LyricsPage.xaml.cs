namespace Visualizer;

public partial class LyricsPage : ContentPage
{
    private byte[]? _albumArt;

    public LyricsPage(SongData song, string lyrics, byte[] originalAlbumArt)
    {
        InitializeComponent();

        _albumArt = originalAlbumArt;
        lblTitle.Text = "\"" + song.Name + "\"";
        lblArtist.Text = song.Artist;
        imgCover.Source = ImageSource.FromStream(() => new MemoryStream(_albumArt));

        var lines = lyrics.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        LyricsCollectionView.ItemsSource = lines;
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        if (imgCover.Source != null)
        {
            await Navigation.PushModalAsync(new ImagePopupPage(_albumArt));
        }
    }
}