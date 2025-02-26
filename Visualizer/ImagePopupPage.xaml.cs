using SkiaSharp;

namespace Visualizer;

public partial class ImagePopupPage : ContentPage
{
    public ImagePopupPage(byte[] originalAlbumArt)
    {
        InitializeComponent();

        if (originalAlbumArt == null )
        {
            FullSizeImage.Source = "default_cover.png";
            return;
        }

        using (var stream = new MemoryStream(originalAlbumArt))
        using (var skStream = new SKManagedStream(stream))
        {
            SKBitmap originalBitmap = SKBitmap.Decode(skStream);
            SKBitmap resizedBitmap = ResizeBitmap(originalBitmap, 1024, 1024); // Upscale with high-quality smoothing

            using (var ms = new MemoryStream())
            {
                resizedBitmap.Encode(ms, SKEncodedImageFormat.Png, 100); // Save with high quality
                FullSizeImage.Source = ImageSource.FromStream(() => new MemoryStream(ms.ToArray()));
            }
        }
    }

    private async void OnCloseTapped(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private SKBitmap ResizeBitmap(SKBitmap original, int newWidth, int newHeight)
    {
        SKBitmap resized = new SKBitmap(newWidth, newHeight);

        using (SKCanvas canvas = new SKCanvas(resized))
        {
            canvas.Clear(SKColors.Transparent);

            SKSamplingOptions samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear); // High-quality filtering

            SKPaint paint = new SKPaint
            {
                IsAntialias = true, // Anti-aliasing for smooth edges
                FilterQuality = SKFilterQuality.High, // Included for compatibility with older versions
            };

            SKRect destRect = new SKRect(0, 0, newWidth, newHeight);
            SKRect sourceRect = new SKRect(0, 0, original.Width, original.Height);
                        
            canvas.DrawBitmap(original, sourceRect, destRect, paint);
        }

        return resized;
    }
}