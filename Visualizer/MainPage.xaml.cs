using x360N;
using nautilusFreeAndroid;
using CommunityToolkit.Maui.Alerts;
using BCnEncoder.Decoder;
using BCnEncoder.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedBass;
using ManagedBass.Mix;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Visualizer
{
    public class LyricsResponse
    {
        [JsonPropertyName("lyrics")]
        public string? Lyrics { get; set; }
    }

    public partial class MainPage : ContentPage
    {
        private DTAParser Parser;
        private int backPressCount;
        private string? _youtubeUrl;
        private nTools nemoTools;
        private bool doAutoplay = true;
        private bool doLoop = false;
        private bool doPreview = true;
        private bool doDrums = true;
        private bool doBass = true;
        private bool doGuitar = true;
        private bool doKeys = true;
        private bool doVocals = true;
        private bool doCrowd = true;
        private bool doBacking = true;
        private int _bassStream;
        private int _mixerStream;
        private bool _isPlaying = false;
        private IDispatcherTimer? _trackTimer;
        private readonly IDispatcherTimer? _spectrumTimer;
        private readonly int _fftSize = 2048; // Higher = more accuracy
        private float[] _fftData;
        private float[] _fftDataSmooth = new float[2048]; // Smoothed FFT values
        private int _visualizationMode = 0;
        private int metadataHeight;
        private int instrumentHeight;
        private string? _screenshotPath;
        private List<AudioParticle> _particles = new List<AudioParticle>();
        private Random _rand = new Random();
        private Queue<float> _waveformHistory = new Queue<float>();
        private byte[]? originalAlbumArt; // Store the original image bytes

        public MainPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            nemoTools = new nTools();
            Parser = new DTAParser();

            InitBass();

            doAutoplay = Preferences.Get("doAutoplay", true);
            doLoop = Preferences.Get("doLoop", false);
            doPreview = Preferences.Get("doPreview", true);

            imgAutoplay.Source = doAutoplay ? "autoplay.png" : "autoplay_off.png";
            imgLoop.Source = doLoop ? "loop.png" : "loop_off.png";
            imgPreview.Source = doPreview ? "dopreview.png" : "dosong.png";

            // Initialize FFT Data
            _fftData = new float[_fftSize];

            // Start Spectrum Timer (60 FPS ~ 16ms)
            _spectrumTimer = Dispatcher.CreateTimer();
            _spectrumTimer.Interval = TimeSpan.FromMilliseconds(16);
            _spectrumTimer.Tick += async (s, e) =>
            {
               if (_bassStream == 0 || _mixerStream == 0) return;

                // Run FFT Processing in a background thread
                await Task.Run(() =>
                {
                    // Read FFT data from BASS (CPU-Intensive)
                    Bass.ChannelGetData(_mixerStream, _fftData, (int)DataFlags.FFT2048);

                    // Ensure the smoothed array matches the FFT data size
                    if (_fftDataSmooth == null || _fftDataSmooth.Length != _fftData.Length)
                    {
                        _fftDataSmooth = new float[_fftData.Length]; // Reinitialize if sizes don't match
                    }

                    // Apply smoothing
                    for (int i = 0; i < _fftData.Length; i++)
                    {
                        _fftDataSmooth[i] = (_fftDataSmooth[i] * 0.75f) + (_fftData[i] * 0.25f); // 75% old, 25% new
                    }
                });

                // Update UI on the main thread
                SpectrumCanvas.InvalidateSurface();
            };
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            const double baseDensity = 2.4375; //development device, works perfect, use this as reference
            double density = DeviceDisplay.MainDisplayInfo.Density;
            double scaleFactor = baseDensity / density;

            AlbumArtImage.WidthRequest = Math.Min(210 * scaleFactor, 210);
            AlbumArtImage.HeightRequest = Math.Min(210 * scaleFactor, 210);

            imgGuitar.WidthRequest = Math.Min(50 * scaleFactor, 50);
            imgGuitar.HeightRequest = Math.Min(50 * scaleFactor, 50);
            proGuitar.WidthRequest = Math.Min(50 * scaleFactor, 50);
            proGuitar.HeightRequest = Math.Min(50 * scaleFactor, 50);

            imgBass.WidthRequest = Math.Min(50 * scaleFactor, 50);
            imgBass.HeightRequest = Math.Min(50 * scaleFactor, 50);
            proBass.WidthRequest = Math.Min(50 * scaleFactor, 50);
            proBass.HeightRequest = Math.Min(50 * scaleFactor, 50);

            imgKeys.WidthRequest = Math.Min(50 * scaleFactor, 50);
            imgKeys.HeightRequest = Math.Min(50 * scaleFactor, 50);
            proKeys.WidthRequest = Math.Min(50 * scaleFactor, 50);
            proKeys.HeightRequest = Math.Min(50 * scaleFactor, 50);

            imgDrums.WidthRequest = Math.Min(50 * scaleFactor, 50);
            imgDrums.HeightRequest = Math.Min(50 * scaleFactor, 50);
            drums2X.WidthRequest = Math.Min(50 * scaleFactor, 50);
            drums2X.HeightRequest = Math.Min(50 * scaleFactor, 50);

            imgVocals.WidthRequest = Math.Min(50 * scaleFactor, 50);
            imgVocals.HeightRequest = Math.Min(50 * scaleFactor, 50);

            guitarDifficulty.WidthRequest = Math.Min(110 * scaleFactor, 110);
            guitarDifficulty.HeightRequest = Math.Min(33 * scaleFactor, 33);

            bassDifficulty.WidthRequest = Math.Min(110 * scaleFactor, 110);
            bassDifficulty.HeightRequest = Math.Min(33 * scaleFactor, 33);

            keysDifficulty.WidthRequest = Math.Min(110 * scaleFactor, 110);
            keysDifficulty.HeightRequest = Math.Min(33 * scaleFactor, 33);

            drumsDifficulty.WidthRequest = Math.Min(110 * scaleFactor, 110);
            drumsDifficulty.HeightRequest = Math.Min(33 * scaleFactor, 33);

            vocalsDifficulty.WidthRequest = Math.Min(110 * scaleFactor, 110);
            vocalsDifficulty.HeightRequest = Math.Min(33 * scaleFactor, 33);

            var trackWidth = Math.Min(300 * scaleFactor, 300);
            imgTrackLine.WidthRequest = trackWidth;
            imgTrackLine.HeightRequest = Math.Min(16 * scaleFactor, 16);

            imgSeekSlider.WidthRequest = Math.Min(30 * scaleFactor, 30);
            imgSeekSlider.HeightRequest = Math.Min(30 * scaleFactor, 30);
            imgSeekSlider.TranslationX = -trackWidth / 2;

            SpectrumCanvas.WidthRequest = width * 0.9;
            SpectrumCanvas.HeightRequest = height * 0.20;

            ArtistLabel.FontSize = Math.Min(24 * scaleFactor, 22);
            SongTitleLabel.FontSize = Math.Min(20 * scaleFactor, 20);
            AlbumLabel.FontSize = Math.Min(14 * scaleFactor, 16);
            ReleaseYearLabel.FontSize = Math.Min(14 * scaleFactor, 14);
            TrackNumberLabel.FontSize = Math.Min(14 * scaleFactor, 14);
            GenreLabel.FontSize = Math.Min(14 * scaleFactor, 14);
            SubGenreLabel.FontSize = Math.Min(14 * scaleFactor, 14);
            RatingLabel.FontSize = Math.Min(14 * scaleFactor, 14);
            AuthorLabel.FontSize = Math.Min(16 * scaleFactor, 16);
            lblCurrentTime.FontSize = Math.Min(14 * scaleFactor, 14);
            lblSongLength.FontSize = Math.Min(14 * scaleFactor, 14);
        }

        private void OnGridSizeChanged(object sender, EventArgs e)
        {
            if (metadataHeight <= 0)
            {
                if (MetadataGrid.Height > 0)
                {
                    metadataHeight = (int)MetadataGrid.Height;
                }
            }
            if (instrumentHeight <= 0)
            {
                if (InstrumentGrid.Height > 0)
                {
                    instrumentHeight = (int)InstrumentGrid.Height;
                }
            }

            if (MetadataGrid.Height < 0 || InstrumentGrid.Height < 0 || DividerBox.Height < 0 || PlaybackGrid.Height < 0 || SpacerBox.Height < 0
                || DividerBox2.Height < 0 || FooterGrid.Height < 0) return;
            
            // Get the full height of all elements except VisualsGrid
            double usedHeight = MetadataGrid.Height +
                                InstrumentGrid.Height +
                                DividerBox.Height +
                                PlaybackGrid.Height +
                                SpacerBox.Height +
                                DividerBox2.Height +
                                FooterGrid.Height;

            // Calculate remaining space for VisualsGrid
            double availableHeight = this.Height - usedHeight;

            if (availableHeight >= 50)
            {                          
                SpectrumCanvas.HeightRequest = availableHeight;
                SpectrumCanvas.IsVisible = true;
            }
            else
            {
                SpectrumCanvas.HeightRequest = 0; //if there's not enough space to display it well, means low res device, remove it
                SpectrumCanvas.IsVisible = false;
            }
            ((Grid)this.Content).RowDefinitions[3].Height = new GridLength(availableHeight > 0 ? availableHeight : 0);
            SpectrumCanvas.WidthRequest = VisualsGrid.Width;
            SpectrumCanvas.InvalidateSurface();
        }

        private void OnCanvasSizeChanged(object sender, EventArgs e)
        {          
            if (VisualsGrid.Height > 0)
            {
                SpectrumCanvas.HeightRequest = VisualsGrid.Height;
                SpectrumCanvas.WidthRequest = VisualsGrid.Width;
                SpectrumCanvas.InvalidateSurface();
            }
        }

        private void OnSpectrumPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            var info = e.Info;
            canvas.Clear(SKColors.Transparent);

            if (_mixerStream == 0 || Bass.ChannelIsActive(_mixerStream) != PlaybackState.Playing)
                return;                       

            switch (_visualizationMode)
            {
                case 0:
                    DrawSolidWaveform(canvas, info.Width, info.Height);                    
                    break;
                case 1:
                    DrawSpectrum(canvas, info.Width, info.Height);
                    break;
                case 2:
                    DrawWaveform(canvas, info.Width, info.Height);
                    break;
                case 3:
                    DrawParticles(canvas, info.Width, info.Height);                    
                    break;
                case 4:
                    DrawWaveformRing(canvas, info.Width, info.Height);
                    break;
                case 5:
                    DrawCircularSpectrum(canvas, info.Width, info.Height);
                    break;
                case 6:
                    DrawFireSpectrum(canvas, info.Width, info.Height);
                    break;
                case 7:
                    DrawOscilloscope(canvas, info.Width, info.Height);
                    break;
                case 8:
                    DrawParticleBars(canvas, info.Width, info.Height);
                    break;                
                case 9:
                    Draw3DCircularSpectrum(canvas, info.Width, info.Height);
                    break;
            }
        }

        private void DrawSolidWaveform(SKCanvas canvas, int width, int height)
        {
            using (var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true })
            {
                SKPath path = new SKPath();
                int minFreqIndex = 2;
                int maxFreqIndex = _fftSize / 2;
                float bottomY = height - 10;

                path.MoveTo(0, bottomY);

                for (int i = 0; i < width; i++)
                {
                    double logPosition = Math.Pow((i + 1) / (double)width, 2.5);
                    int fftIndex = (int)(logPosition * (maxFreqIndex - minFreqIndex)) + minFreqIndex;
                    fftIndex = Math.Max(0, Math.Min(fftIndex, _fftDataSmooth.Length - 1));

                    float scaleFactor = 30 + (float)(50 * Math.Sqrt(logPosition));
                    float amplitude = Math.Min(1, _fftDataSmooth[fftIndex] * scaleFactor);
                    float peakY = bottomY - (amplitude * (height * 0.9f));

                    float x = i;
                    path.LineTo(x, peakY);
                }

                path.LineTo(width, bottomY);
                path.Close();

                // **Gradient from bottom to peaks**
                paint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, bottomY), new SKPoint(0, 0),
                    new SKColor[] { SKColors.LimeGreen, SKColors.Yellow, SKColors.Red },
                    new float[] { 0.0f, 0.6f, 1.0f },
                    SKShaderTileMode.Clamp
                );

                canvas.DrawPath(path, paint);
            }
        }

        private void Draw3DCircularSpectrum(SKCanvas canvas, int width, int height)
        {
            int numBars = 36;
            float radius = Math.Min(width, height) * 0.35f;
            float tilt = 0.6f;

            int fftSize = _fftDataSmooth.Length;
            int minFreqIndex = 2;
            int maxFreqIndex = fftSize / 2;

            using (var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 4, IsAntialias = true })
            {
                SKPoint center = new SKPoint(width / 2, height / 2);

                for (int i = 0; i < numBars; i++)
                {
                    double angle = i * (2 * Math.PI / numBars);
                    double logPosition = Math.Pow((i + 1) / (double)numBars, 2.5);
                    int fftIndex = (int)(logPosition * (maxFreqIndex - minFreqIndex)) + minFreqIndex;
                    fftIndex = Math.Max(0, Math.Min(fftIndex, _fftDataSmooth.Length - 1));

                    float magnitude = Math.Min(1, _fftDataSmooth[fftIndex] * 50);
                    float barHeight = magnitude * radius * 0.9f; // Increased height

                    float depthFactor = (float)(1.0 - tilt * Math.Sin(angle));
                    barHeight *= depthFactor;

                    SKPoint start = new SKPoint(
                        center.X + (float)(radius * Math.Cos(angle)),
                        center.Y + (float)((radius * Math.Sin(angle)) * tilt)
                    );

                    SKPoint end = new SKPoint(
                        center.X + (float)((radius + barHeight) * Math.Cos(angle)),
                        center.Y + (float)(((radius + barHeight) * Math.Sin(angle)) * tilt)
                    );

                    float brightness = 1.0f - (depthFactor * 0.4f);
                    paint.Color = new SKColor(
                        (byte)(255 * brightness),
                        (byte)(100 * brightness),
                        (byte)(255 * brightness)
                    );

                    canvas.DrawLine(start, end, paint);
                }
            }
        }

        private void DrawOscilloscope(SKCanvas canvas, int width, int height)
        {
            int numSamples = 300; // Higher sample count for smooth scrolling
            float centerY = height / 2f; // **This ensures it's always centered**
            float amplitudeBoost = height * 0.4f; // **Keeps waves visible without clipping**

            using (var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3, // Thicker for visibility
                IsAntialias = true,
                Color = SKColors.Red
            })
            {
                SKPath path = new SKPath();

                // Maintain history size
                while (_waveformHistory.Count > numSamples)
                    _waveformHistory.Dequeue();

                // **Extract amplitude data from the FFT**
                if (_fftDataSmooth.Length > 0)
                {
                    float maxAmplitude = 0;
                    float avgSample = 0;

                    for (int i = _fftSize / 16; i < _fftSize / 4; i++) // Sample mid-high frequencies
                    {
                        avgSample += _fftDataSmooth[i];
                        if (_fftDataSmooth[i] > maxAmplitude) maxAmplitude = _fftDataSmooth[i];
                    }
                    avgSample /= (_fftSize / 4 - _fftSize / 16);

                    float normalizedSample = avgSample / (maxAmplitude > 0 ? maxAmplitude : 1);
                    float newSample = (normalizedSample - 0.5f) * 2f; // Adjust multiplier to keep wave balanced
                    _waveformHistory.Enqueue(newSample);
                }

                // **Ensure buffer has data (fallback wave)**
                if (_waveformHistory.Count == 0)
                {
                    for (int i = 0; i < numSamples; i++)
                    {
                        float fakeWave = (float)Math.Sin(i * 0.1) * 0.5f; // Fake sine wave
                        _waveformHistory.Enqueue(fakeWave);
                    }
                }

                float[] waveformArray = _waveformHistory.ToArray();

                for (int i = 0; i < waveformArray.Length; i++)
                {
                    float x = (i / (float)numSamples) * width;
                    float y = centerY + (waveformArray[i] * amplitudeBoost); // **Wave is now properly centered**

                    if (i == 0)
                        path.MoveTo(x, y);
                    else
                        path.LineTo(x, y);
                }

                canvas.DrawPath(path, paint);
            }
        }

        private void DrawCircularSpectrum(SKCanvas canvas, int width, int height)
        {
            int numBars = 48; // More bars for a smooth circle
            float radius = Math.Min(width, height) * 0.4f; // Radius of the circle

            int fftSize = _fftDataSmooth.Length;
            int minFreqIndex = 2;
            int maxFreqIndex = fftSize / 2;

            using (var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4,
                IsAntialias = true
            })
            {
                SKPoint center = new SKPoint(width / 2, height / 2);

                for (int i = 0; i < numBars; i++)
                {
                    double angle = i * (2 * Math.PI / numBars); // Angle in radians
                    double logPosition = Math.Pow((i + 1) / (double)numBars, 2.5);
                    int fftIndex = (int)(logPosition * (maxFreqIndex - minFreqIndex)) + minFreqIndex;
                    fftIndex = Math.Max(0, Math.Min(fftIndex, _fftDataSmooth.Length - 1));

                    float magnitude = Math.Min(1, _fftDataSmooth[fftIndex] * 40);
                    float barHeight = magnitude * radius * 0.8f;

                    SKPoint start = new SKPoint(center.X + (float)(radius * Math.Cos(angle)),
                                                center.Y + (float)(radius * Math.Sin(angle)));

                    SKPoint end = new SKPoint(center.X + (float)((radius + barHeight) * Math.Cos(angle)),
                                              center.Y + (float)((radius + barHeight) * Math.Sin(angle)));

                    // **Color change based on height**
                    paint.Color = magnitude > 0.7f ? SKColors.Red :
                                  magnitude > 0.4f ? SKColors.Yellow :
                                  SKColors.LimeGreen;

                    canvas.DrawLine(start, end, paint);
                }
            }
        }

        private void DrawWaveformRing(SKCanvas canvas, int width, int height)
        {
            using (var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
                IsAntialias = true,
                Color = SKColors.Green,
            })
            {
                SKPath path = new SKPath();
                SKPoint center = new SKPoint(width / 2, height / 2);
                float radius = Math.Min(width, height) * 0.4f; // Base radius

                int minFreqIndex = 2;
                int maxFreqIndex = _fftSize / 2;

                for (int i = 0; i < 360; i++)
                {
                    double angle = Math.PI * i / 180; // Convert to radians
                    double logPosition = Math.Pow((i + 1) / 360.0, 2.5);
                    int fftIndex = (int)(logPosition * (maxFreqIndex - minFreqIndex)) + minFreqIndex;
                    fftIndex = Math.Max(0, Math.Min(fftIndex, _fftDataSmooth.Length - 1));

                    float magnitude = Math.Min(1, _fftDataSmooth[fftIndex] * 50);
                    float waveOffset = magnitude * radius * 0.4f;

                    float x = center.X + (radius + waveOffset) * (float)Math.Cos(angle);
                    float y = center.Y + (radius + waveOffset) * (float)Math.Sin(angle);

                    if (i == 0)
                        path.MoveTo(x, y);
                    else
                        path.LineTo(x, y);
                }

                canvas.DrawPath(path, paint);
            }
        }

        private void DrawFireSpectrum(SKCanvas canvas, int width, int height)
        {
            int numBars = 24;
            float barWidth = width / (float)numBars;
            float maxHeight = height * 0.90f;

            int fftSize = _fftDataSmooth.Length;
            int minFreqIndex = 2;
            int maxFreqIndex = fftSize / 2;

            using (var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true })
            {
                for (int i = 0; i < numBars; i++)
                {
                    double logPosition = Math.Pow((i + 1) / (double)numBars, 2.5);
                    int fftIndex = (int)(logPosition * (maxFreqIndex - minFreqIndex)) + minFreqIndex;
                    fftIndex = Math.Max(0, Math.Min(fftIndex, _fftDataSmooth.Length - 1));

                    float magnitude = Math.Min(1, _fftDataSmooth[fftIndex] * 30);
                    float barHeight = magnitude * maxHeight;

                    float x = i * barWidth;
                    float y = height - barHeight;

                    // **Smooth fire flicker effect**
                    float flicker = (float)(Math.Sin(Environment.TickCount * 0.002f + i) * 5);
                    y -= flicker;

                    // **Fire Gradient**
                    paint.Shader = SKShader.CreateLinearGradient(
                        new SKPoint(x, y + barHeight), new SKPoint(x, y),
                        new SKColor[] { SKColors.Red, SKColors.Orange, SKColors.Yellow },
                        new float[] { 0.0f, 0.5f, 1.0f },
                        SKShaderTileMode.Clamp
                    );

                    canvas.DrawRect(x, y, barWidth - 2, barHeight, paint);
                }
            }
        }

        private void DrawParticleBars(SKCanvas canvas, int width, int height)
        {
            int numLines = Math.Max(50, width / 15); // Dynamically adjust number of lines based on screen width
            float maxHeight = height * 0.9f;

            int fftSize = _fftDataSmooth.Length;
            int minFreqIndex = 2;
            int maxFreqIndex = fftSize / 2;

            float spacing = width / (float)numLines; // Spread lines evenly

            using (var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true })
            {
                for (int i = 0; i < numLines; i++)
                {
                    double logPosition = Math.Pow((i + 1) / (double)numLines, 2.5);
                    int fftIndex = (int)(logPosition * (maxFreqIndex - minFreqIndex)) + minFreqIndex;
                    fftIndex = Math.Max(0, Math.Min(fftIndex, _fftDataSmooth.Length - 1));

                    float magnitude = Math.Min(1, _fftDataSmooth[fftIndex] * 40);
                    float lineHeight = magnitude * maxHeight;

                    float x = i * spacing; // Spread lines across full width
                    float y = height - lineHeight;

                    paint.Color = SKColors.LimeGreen;
                    canvas.DrawLine(x, height, x, y, paint);

                    // Draw floating particle at the top
                    using (var dotPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.Red })
                    {
                        canvas.DrawCircle(x, y, 4, dotPaint);
                    }
                }
            }
        }


        private void DrawParticles(SKCanvas canvas, int width, int height)
        {
            int numParticles = 40; // Number of particles
            float maxRadius = height * 0.1f; // Max size of particles
            float centerX = width / 2f;
            float centerY = height / 2f;

            int fftSize = _fftDataSmooth.Length;
            int minFreqIndex = 2;
            int maxFreqIndex = fftSize / 2;

            if (_particles.Count == 0)
            {
                // Initialize particles
                for (int i = 0; i < numParticles; i++)
                {
                    _particles.Add(new AudioParticle
                    {
                        Position = new SKPoint(_rand.Next(width), _rand.Next(height)),
                        Size = _rand.Next(5, 20),
                        Color = SKColors.LimeGreen,
                        SpeedY = (float)_rand.NextDouble() * 2 + 1
                    });
                }
            }

            using (var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true })
            {
                for (int i = 0; i < numParticles; i++)
                {
                    // Apply logarithmic frequency mapping
                    double logPosition = Math.Pow((i + 1) / (double)numParticles, 2.5);
                    int fftIndex = (int)(logPosition * (maxFreqIndex - minFreqIndex)) + minFreqIndex;
                    fftIndex = Math.Max(0, Math.Min(fftIndex, _fftDataSmooth.Length - 1));

                    // Scale amplitude to determine size
                    float magnitude = Math.Min(1, _fftDataSmooth[fftIndex] * 50);
                    _particles[i].Size = Math.Max(5, magnitude * maxRadius);

                    // Change color based on intensity
                    _particles[i].Color = magnitude > 0.7f ? SKColors.Red :
                                          magnitude > 0.4f ? SKColors.Yellow :
                                          SKColors.LimeGreen;

                    // Move particles downward and wrap around
                    _particles[i].Position = new SKPoint(
                        _particles[i].Position.X + (_rand.Next(-2, 3)), // Slight horizontal drift
                        _particles[i].Position.Y + _particles[i].SpeedY
                    );

                    if (_particles[i].Position.Y > height) // Reset when out of bounds
                        _particles[i].Position = new SKPoint(_rand.Next(width), 0);

                    // Draw particle
                    paint.Color = _particles[i].Color;
                    canvas.DrawCircle(_particles[i].Position, _particles[i].Size, paint);
                }
            }
        }

        private void DrawWaveform(SKCanvas canvas, int width, int height)
        {
            using (var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
                IsAntialias = true
            })
            {
                SKPath path = new SKPath();

                int minFreqIndex = 2;
                int maxFreqIndex = _fftSize / 2;
                float bottomY = height - 10;

                for (int i = 0; i < width; i++)
                {
                    double logPosition = Math.Pow((i + 1) / (double)width, 2.5);
                    int fftIndex = (int)(logPosition * (maxFreqIndex - minFreqIndex)) + minFreqIndex;
                    fftIndex = Math.Max(0, Math.Min(fftIndex, _fftDataSmooth.Length - 1));

                    float scaleFactor = 30 + (float)(50 * Math.Sqrt(logPosition));
                    float amplitude = Math.Min(1, _fftDataSmooth[fftIndex] * scaleFactor);
                    float yPosition = bottomY - (amplitude * (height * 0.9f));

                    float x = i * (width / (float)width);

                    // **Color based on amplitude**
                    if (amplitude > 0.7f)
                        paint.Color = SKColors.Red; // High amplitude
                    else if (amplitude > 0.4f)
                        paint.Color = SKColors.Yellow; // Mid amplitude
                    else
                        paint.Color = SKColors.LimeGreen; // Low amplitude

                    if (i == 0)
                        path.MoveTo(x, bottomY);
                    else
                        path.LineTo(x, yPosition);
                }

                canvas.DrawPath(path, paint);
            }
        }

        private void OnSpectrumTapped(object sender, EventArgs e)
        {
            Vibrate();
            _visualizationMode = (_visualizationMode + 1) % 10; // Switch between available modes           
            SpectrumCanvas.InvalidateSurface(); // Redraw with new visualization
        }

        private async void OnAuthorTapped(object sender, EventArgs e)
        {
            if (AuthorLabel.Text == "Authored by..." || string.IsNullOrEmpty(AuthorLabel.Text) || AuthorLabel.Text.Contains(",")) return;
            var author = AuthorLabel.Text.Replace("Authored by ", "");
            var link = "https://rhythmverse.co/songfiles/author/" + author.ToLowerInvariant();
            Vibrate();
            await Launcher.OpenAsync(link);
        }

        private void DrawSpectrum(SKCanvas canvas, int width, int height)
        {
            int numBars = 24;
            float barWidth = width / (float)numBars;
            float maxHeight = height * 0.90f;
            float centerX = width / 2f;

            int fftSize = _fftDataSmooth.Length;
            int minFreqIndex = 2;
            int maxFreqIndex = fftSize / 2;

            using (var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true })
            {
                for (int i = 0; i < numBars; i++)
                {
                    double logPosition = Math.Pow((i + 1) / (double)numBars, 2.5);
                    int fftIndex = (int)(logPosition * (maxFreqIndex - minFreqIndex)) + minFreqIndex;
                    fftIndex = Math.Max(0, Math.Min(fftIndex, _fftDataSmooth.Length - 1));

                    float magnitude = Math.Min(1, _fftDataSmooth[fftIndex] * 40);
                    float barHeight = magnitude * maxHeight;

                    float x = centerX - ((numBars / 2f) * barWidth) + (i * barWidth);
                    float y = height - barHeight;

                    // **Smooth color gradient**
                    paint.Shader = SKShader.CreateLinearGradient(
                        new SKPoint(x, y), new SKPoint(x, height),
                        new SKColor[] { SKColors.Red, SKColors.Yellow, SKColors.LimeGreen },
                        new float[] { 0.0f, 0.5f, 1.0f },
                        SKShaderTileMode.Clamp
                    );

                    canvas.DrawRect(x, y, barWidth - 2, barHeight, paint);
                }
            }
        }

        private async void OnUploadTapped(object sender, EventArgs e)
        {
            Vibrate();
            SaveScreenshot();
            if (string.IsNullOrEmpty(_screenshotPath) || !File.Exists(_screenshotPath)) return;

            string imageUrl = await new ImgurUploader().UploadToImgurAsync(_screenshotPath);

            if (string.IsNullOrEmpty(imageUrl)) return;
            await Clipboard.SetTextAsync(imageUrl);
            await DisplayAlert("Upload Successful", "Screenshot was successfully uploaded to Imgur and the link has been copied to your clipboard," +
                " ready to paste it anywhere.", "OK");
        }
            
        private double GetFullScreenHeight()
        {
            return (MetadataGrid.Height + InstrumentGrid.Height + DividerBox.Height + VisualsGrid.Height + PlaybackGrid.Height + 
                SpacerBox.Height + DividerBox2.Height + FooterGrid.Height) * DeviceDisplay.MainDisplayInfo.Density;
        }

        private async void SaveScreenshot()
        {
            _screenshotPath = "";            
            
            try
            {
                // 1️⃣ Capture Full Screenshot
                DividerBox.IsVisible = false;//hide it so it doesn't show on screenshot on certain devices
                var screenshot = await Screenshot.CaptureAsync();
                DividerBox.IsVisible = true;//restore it
                if (screenshot == null) return;

                Stream screenshotStream = await screenshot.OpenReadAsync();
                SKBitmap fullBitmap = SKBitmap.Decode(screenshotStream);

                //best attempt to compromise when doing screenshot since Android is crazy
                double screenDensity = DeviceDisplay.MainDisplayInfo.Density;
                double baseDensity = 2.4375;  // Development phone density (high end)
                double targetDensity = 2.8125;   // Development phone density (entry level)
                double baseHeightFactor = 0.6; //known to work with high end phone
                double targetHeightFactor = 0.4; //known to work with entry level phone

                // Linear interpolation formula
                double heightFactor = baseHeightFactor + ((screenDensity - baseDensity) / (targetDensity - baseDensity)) * (targetHeightFactor - baseHeightFactor);

                // Clamp to prevent extreme values
                heightFactor = Math.Clamp(heightFactor, 0.35, 0.65);

                var topY = (float)((DeviceDisplay.MainDisplayInfo.Height - GetFullScreenHeight()) * heightFactor);
                var height = (int)(((MetadataGrid.Height + InstrumentGrid.Height) * DeviceDisplay.MainDisplayInfo.Density));

                // 3️⃣ Crop the Screenshot to Only the Grid Area
                SKBitmap croppedBitmap = new SKBitmap(fullBitmap.Width, height);
                using (var canvas = new SKCanvas(croppedBitmap))
                {
                    SKRect sourceRect = new SKRect(0, topY, fullBitmap.Width, topY + height);
                    SKRect destRect = new SKRect(0, 0, fullBitmap.Width, height);
                    canvas.DrawBitmap(fullBitmap, sourceRect, destRect);

                    // 4️⃣ Add a 2px Black Border
                    using (var borderPaint = new SKPaint
                    {
                        Color = SKColors.Black,
                        StrokeWidth = 8,
                        Style = SKPaintStyle.Stroke
                    })
                    {
                        canvas.DrawRect(4, 4, fullBitmap.Width - 8, height - 8, borderPaint);
                    }
                }

                // 5️⃣ Save & Share the Cropped Image
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string screenshotPath = Path.Combine(FileSystem.CacheDirectory, $"visualizer_{timestamp}.png");
                using (var image = SKImage.FromBitmap(croppedBitmap))
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (var fileStream = File.OpenWrite(screenshotPath))
                {
                    data.SaveTo(fileStream);
                }

                _screenshotPath = screenshotPath;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Failed to capture screenshot:\n" + ex.Message, "OK");
                _screenshotPath = "";
            }
        }

        private async void OnRVTapped(object sender, EventArgs e)
        {
            Vibrate();
            string url = "https://rhythmverse.co/songfiles/game/rb3xbox";
            await Launcher.OpenAsync(url);
        }

        private async void OnShareTapped(object sender, EventArgs e)
        {
            Vibrate();
            SaveScreenshot();
            if (string.IsNullOrEmpty(_screenshotPath) || !File.Exists(_screenshotPath)) return;

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Check this out!",
                File = new ShareFile(_screenshotPath)
            });
        }

        private void SaveSettings()
        {
            Preferences.Set("doAutoplay", doAutoplay);
            Preferences.Set("doLoop", doLoop);
            Preferences.Set("doPreview", doPreview);
        }

        private async void LoadLyricsAsync(string artist, string title)
        {
            imgLyrics.BindingContext = null; //reset
            imgLyrics.IsVisible = false; //reset
            string lyrics = await GetLyricsAsync(artist, title);
            if (!string.IsNullOrEmpty(lyrics))
            {
                imgLyrics.IsVisible = true;
                imgLyrics.BindingContext = lyrics;
            }
        }

        private void StartTrackTimer()
        {
            if (_trackTimer == null)
            {
                _trackTimer = Dispatcher.CreateTimer();
                _trackTimer.Interval = TimeSpan.FromMilliseconds(500); // Update every 0.5 sec
                _trackTimer.Tick += (sender, e) => UpdateTrackPosition();
            }

            _trackTimer.Start();
            _spectrumTimer?.Start();
        }

        public async Task<string> GetLyricsAsync(string artist, string title)
        {
            string apiUrl = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(artist)}/{Uri.EscapeDataString(title)}";

            using HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                var lyricsData = JsonSerializer.Deserialize<LyricsResponse>(json);
                return lyricsData?.Lyrics ?? string.Empty;
            }

            return string.Empty;
        }

        public async Task<string> GetYouTubeMusicVideoUrl(string artist, string title)
        {
            try
            {
                string apiKey = "AIzaSyDXS062js9V3w-DHu-_io4p2bPTpX25N-0"; //please get your own
                string query = Uri.EscapeDataString($"{artist} {title} music");
                string url = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&q={query}&key={apiKey}";

                using HttpClient client = new HttpClient();
                string response = await client.GetStringAsync(url);

                using JsonDocument doc = JsonDocument.Parse(response);
                JsonElement root = doc.RootElement;

                string videoId = root.GetProperty("items")[0].GetProperty("id").GetProperty("videoId").GetString();
                return $"https://www.youtube.com/watch?v={videoId}";
            }
            catch (Exception ex)
            {
                //await DisplayAlert("Error", $"❌ YouTube API Error: {ex.Message}", "OK");
                return null;
            }
        }
        private string GetFormattedRating(string rating)
        {
            switch (rating)
            {
                case "1":
                case "FF":
                    return "Family Friendly";
                case "2":
                case "SR":
                    return "Supervision Recommended";
                case "3":
                case "M":
                    return "Mature";
                default:
                    return "Not Rated";
            }
        }

        private void doDefaults()
        {
            ArtistLabel.Text = "";
            SongTitleLabel.Text = "";
            AlbumLabel.Text = "";
            ReleaseYearLabel.Text = "Year Released:";
            RatingLabel.Text = "Rating:";
            GenreLabel.Text = "Genre:";
            SubGenreLabel.Text = "SubGenre:";
            TrackNumberLabel.Text = "Track #";
            imgVocals.Source = "mic1.png";
            proBass.IsVisible = false;
            proGuitar.IsVisible = false;
            proKeys.IsVisible = false;
            drums2X.IsVisible = false;
            vocalsDifficulty.Source = "diff0.png";
            bassDifficulty.Source = "diff0.png";
            drumsDifficulty.Source = "diff0.png";
            guitarDifficulty.Source = "diff0.png";
            keysDifficulty.Source = "diff0.png";
            AuthorLabel.Text = "Authored by...";          
            doDrums = true;
            doBass = true;
            doGuitar = true;
            doKeys = true;
            doVocals = true;
            doCrowd = true;
            doBacking = true;
            AuthorLabel.FontAttributes = FontAttributes.None;
            AuthorLabel.TextColor = Colors.Black;
            AuthorLabel.TextDecorations = TextDecorations.None;
        }

        private async void ProcessCONFile(string file)
        {
            doDefaults();
            originalAlbumArt = null;

            if (VariousFunctions.ReadFileType(file) != XboxFileType.STFS)
            {
                await DisplayAlert("Error", "That's not a valid STFS file", "OK");
                return;
            }
            var xPackage = new STFSPackage(file);
            if (xPackage == null)
            {
                await DisplayAlert("Error", "Error loading CON file", "OK");
                return;
            }
            var xFile = xPackage.GetFile("songs/songs.dta");
            if (xFile == null)
            {
                await DisplayAlert("Error", "Can't find a songs.dta file", "OK");
                xPackage.CloseIO();
                return;
            }
            var xDTA = xFile.Extract();
            if (xDTA == null)
            {
                await DisplayAlert("Error", "Failed to extract the songs.dta file", "OK");
                xPackage.CloseIO();
                return;
            }
            if (!Parser.ReadDTA(xDTA))
            {
                await DisplayAlert("Error", "Failed to read that songs.dta file", "OK");
                xPackage.CloseIO();
                return;
            }
            if (Parser.Songs.Count > 1)
            {
                await DisplayAlert("Error", "This seems to be a pack, I can only work with individual songs", "OK");
                xPackage.CloseIO();
                return;
            }
            try
            {
                SongTitleLabel.Text = "\"" + Parser.Songs[0].Name + "\"";
                ArtistLabel.Text = Parser.Songs[0].Artist;
                AlbumLabel.Text = Parser.Songs[0].Album;
                ReleaseYearLabel.Text = "Year Released: " + Parser.Songs[0].YearReleased.ToString();
                TrackNumberLabel.Text = "Track #: " + (Parser.Songs[0].TrackNumber > 0 ? Parser.Songs[0].TrackNumber.ToString() : "");
                GenreLabel.Text = "Genre: " + Parser.doGenre(Parser.Songs[0].Genre);
                SubGenreLabel.Text = "SubGenre: " + Parser.doSubGenre(Parser.Songs[0].SubGenre);
                RatingLabel.Text = "Rating: " + GetFormattedRating(Parser.Songs[0].Rating.ToString());
                AuthorLabel.Text = string.IsNullOrEmpty(Parser.Songs[0].ChartAuthor) ? "" : ("Authored by " + Parser.Songs[0].ChartAuthor);
                if (!string.IsNullOrEmpty(AuthorLabel.Text) && !AuthorLabel.Text.Contains(","))
                {
                    AuthorLabel.FontAttributes = FontAttributes.Bold;
                    AuthorLabel.TextDecorations = TextDecorations.Underline;
                    AuthorLabel.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#0366D6");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Error while populating fields with parsed data:\n\n" + ex.Message, "OK");
                xPackage.CloseIO();
                return;
            }
            var internalName = Parser.Songs[0].InternalName;
            //var xMIDI = xPackage.GetFile("songs/" + internalName + "/" + internalName + ".mid");//what to do with this?
            var xMOGG = xPackage.GetFile("songs/" + internalName + "/" + internalName + ".mogg");
            var xART = xPackage.GetFile("songs/" + internalName + "/gen/" + internalName + "_keep.png_xbox");

            byte[]? moggBytes = null;
            if (xMOGG != null)
            {
                moggBytes = xMOGG.Extract();
            }
            byte[]? artBytes = null;
            if (xART != null)
            {
                artBytes = xART.Extract();
            }
            xPackage.CloseIO();
                        
            if (artBytes != null && artBytes.Count() > 0)
            {                
                ConvertXboxImage(artBytes);
            }
            else
            {
                AlbumArtImage.Source = "default_cover.png";
            }
            doInstruments();
            await Task.Delay(50);

            if (moggBytes != null && moggBytes.Count() > 0)
            {
                loadingIndicator.IsVisible = true;
                loadingIndicator.IsRunning = true;

                processMogg(moggBytes);

                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;                
            }

            LoadLyricsAsync(Parser.Songs[0].Artist, Parser.Songs[0].Name);
            LoadYouTubeVideo(Parser.Songs[0].Artist, Parser.Songs[0].Name);
        }

        private bool InitBass()
        {
            if (!Bass.Init(-1, 44100, DeviceInitFlags.Default, IntPtr.Zero))
            {
                DisplayAlert("Error", "Failed to initialize BASS, won't be able to play any audio!", "OK");
                return false;
            }            
            return true;
        }

        private async void processMogg(byte[] moggBytes)
        {
            StopPlayback();

            //Show loading indicator before starting decryption
            MainThread.BeginInvokeOnMainThread(() =>
            {
                loadingIndicator.IsVisible = true;
                loadingIndicator.IsRunning = true;
            });

            bool success = await Task.Run(() =>
            {
                return nemoTools.DecryptMogg(moggBytes);
            });

            // Hide loading indicator on the main thread after decryption finishes
            MainThread.BeginInvokeOnMainThread(() =>
            {
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
            });

            if (!success)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Error", "Failed to decrypt mogg file, can't play the song audio", "OK");
                });
                return;
            }

            // Create a BASS stream from memory
            _bassStream = Bass.CreateStream(nemoTools.OggData, 0L, nemoTools.OggData.Length, BassFlags.Decode | BassFlags.Float);
            if (_bassStream == 0)
            {
                await DisplayAlert("Error", "Failed to load audio stream", "OK");
                return;
            }

            var streamInfo = Bass.ChannelGetInfo(_bassStream);
            var frequency = 0;
            try
            {
                frequency = streamInfo.Frequency;
            }
            catch
            {
                await DisplayAlert("Error", "Failed to load audio stream", "OK");
                return;
            }
            
            // Initialize Mixer (for track muting later)
            _mixerStream = BassMix.CreateMixerStream(frequency, 2, BassFlags.MixerEnd);
            BassMix.MixerAddChannel(_mixerStream, _bassStream, BassFlags.MixerChanMatrix);
            UpdateChannelMatrix();
            SetAudioStart();
            if (!doAutoplay) return;
            Bass.ChannelPlay(_mixerStream);
            imgPlay.Source = "pause.png";
            _isPlaying = true;
            StartTrackTimer();
        }

        private void UpdateChannelMatrix()
        {
            if (Parser.Songs == null || Parser.Songs.Count == 0) return;

            var matrix = new float[2, Parser.Songs[0].ChannelsTotal];
            var tools = new BassTools();
            matrix = tools.GetChannelMatrix(Parser.Songs[0], Parser.Songs[0].ChannelsTotal, GetStemsToPlay(), 2, true);
            BassMix.ChannelSetMatrix(_bassStream, matrix);
        }

        private void OnPlayTapped(object sender, EventArgs e)
        {
            Vibrate();
            if (_bassStream == 0 || _mixerStream == 0) return;

            switch (Bass.ChannelIsActive(_mixerStream))
            {
                case PlaybackState.Playing:
                    Bass.ChannelPause(_mixerStream);
                    imgPlay.Source = "play.png";
                    _isPlaying = false;
                    _trackTimer?.Stop();
                    break;
                case PlaybackState.Paused:
                    Bass.ChannelPlay(_mixerStream, false);
                    imgPlay.Source = "pause.png";
                    _isPlaying = true;
                    StartTrackTimer();
                    break;
                default:
                    SetAudioStart();
                    Bass.ChannelPlay(_mixerStream, false);
                    imgPlay.Source = "pause.png";
                    _isPlaying = true;
                    StartTrackTimer();
                    break;
            }            
        }

        private void OnStopTapped(object sender, EventArgs e)
        {
            Vibrate();
            StopPlayback();
        }
        
        private void StopPlayback()
        {
            if (_bassStream == 0 || _mixerStream == 0) return;

            _trackTimer?.Stop(); 
            _spectrumTimer?.Stop();
            Bass.ChannelStop(_mixerStream);
            Bass.ChannelStop(_bassStream);          
            SetPlayLocation(doPreview && Parser.Songs != null && Parser.Songs[0].PreviewStart > 0 ? (double)(Parser.Songs[0].PreviewStart/1000) : 0.0);
            UpdateTrackPosition();
            imgPlay.Source = "play.png"; // Reset play button
            _isPlaying = false;
        }

        private void UpdateTrackPosition()
        {
            if (_bassStream == 0 || _mixerStream == 0) return;

            double currentTime = Bass.ChannelBytes2Seconds(_bassStream, Bass.ChannelGetPosition(_bassStream));
            double totalTime = Bass.ChannelBytes2Seconds(_bassStream, Bass.ChannelGetLength(_bassStream));

            lblCurrentTime.Text = TimeSpan.FromSeconds(currentTime).ToString(@"m\:ss");
            lblSongLength.Text = TimeSpan.FromSeconds(totalTime).ToString(@"m\:ss");

            // Move the slider
            double progress = (currentTime / totalTime) * imgTrackLine.Width;
            imgSeekSlider.TranslationX = progress - (imgTrackLine.Width / 2);

            if (currentTime >= totalTime) //song ended
            {
                if (doLoop)
                {
                    SetAudioStart();
                    Bass.ChannelPlay(_mixerStream, true);
                    imgPlay.Source = "pause.png";
                    _isPlaying = true;
                    StartTrackTimer();
                }
                else
                {
                    StopPlayback();
                }
            }
            if (!doPreview) return; //loop back to preview start time if enabled
            double endTime = Parser.Songs[0].PreviewEnd > 0 ? (Parser.Songs[0].PreviewEnd / 1000) : ((Parser.Songs[0].PreviewStart + 30000) / 1000);
            if (currentTime >= endTime)
            {
                SetAudioStart();
            }
        }

        private async void SetPlayLocation(double time)
        {
            if (time < 0)
            {
                time = 0.0;
            }
            try
            {
                BassMix.ChannelSetPosition(_bassStream, Bass.ChannelSeconds2Bytes(_bassStream, time));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Error setting play location:\n" + ex.Message, "OK");
            }
        }

        private double _startSeekPosition;
        private void OnSeekTapped(object sender, PanUpdatedEventArgs e)
        {
            if (_bassStream == 0 || _mixerStream == 0) return;

            double totalTime = Bass.ChannelBytes2Seconds(_bassStream, Bass.ChannelGetLength(_bassStream));

            //if user is manually going through the song, disable preview playback
            doPreview = false;
            imgPreview.Source = "dosong.png";

            // Get the actual width of the progress bar
            double trackWidth = imgTrackLine.Width;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    // Store the starting position
                    _startSeekPosition = Bass.ChannelBytes2Seconds(_bassStream, Bass.ChannelGetPosition(_bassStream));
                    break;

                case GestureStatus.Running:
                    // Calculate the new position based on initial seek position and movement
                    double newTime = _startSeekPosition + ((e.TotalX / trackWidth) * totalTime);

                    // Ensure within valid range
                    newTime = Math.Max(0, Math.Min(newTime, totalTime));

                    // Update BASS playback position
                    Bass.ChannelSetPosition(_bassStream, Bass.ChannelSeconds2Bytes(_bassStream, newTime));

                    // Update UI elements
                    lblCurrentTime.Text = TimeSpan.FromSeconds(newTime).ToString(@"m\:ss");

                    // Move the seek slider visually
                    imgSeekSlider.TranslationX = (newTime / totalTime) * trackWidth - (trackWidth/2);
                    break;
            }
        }

        private string GetStemsToPlay()
        {
            var stems = "";
            if (doDrums)
            {
                stems += "drums|";
            }
            if (doBass)
            {
                stems += "bass|";
            }
            if (doGuitar)
            {
                stems += "guitar|";
            }
            if (doVocals)
            {
                stems += "vocals|";
            }
            if (doKeys)
            {
                stems += "keys|";
            }
            if (doBacking)
            {
                stems += "backing|";
            }
            if (doCrowd)
            {
                stems += "crowd";
            }
            return stems;
        }

        private void OnAutoplayTapped(object sender, EventArgs e)
        {
            Vibrate();
            doAutoplay = !doAutoplay;
            imgAutoplay.Source = doAutoplay ? "autoplay.png" : "autoplay_off.png";
            SaveSettings();

            if (_bassStream == 0 || _mixerStream == 0) return;

            if (doAutoplay && Bass.ChannelIsActive(_mixerStream) != PlaybackState.Playing)
            {
                SetAudioStart();
                Bass.ChannelPlay(_mixerStream, false);
                imgPlay.Source = "pause.png";
                _isPlaying = true;
                StartTrackTimer();
            }
        }

        private void OnLoopTapped(object sender, EventArgs e)
        {
            Vibrate();
            doLoop = !doLoop;
            imgLoop.Source = doLoop ? "loop.png" : "loop_off.png";
            SaveSettings();
        }

        private void OnPreviewTapped(object sender, EventArgs e)
        {
            Vibrate();
            doPreview = !doPreview;
            imgPreview.Source = doPreview ? "dopreview.png" : "dosong.png";
            SaveSettings();

            if (_bassStream == 0 || _mixerStream == 0 || Parser.Songs == null || Parser.Songs.Count == 0) return;
            SetAudioStart();
        }

        private void SetAudioStart()
        {
            if (doPreview)
            {
                SetPlayLocation(Parser.Songs[0].PreviewStart / 1000);
            }
            else
            {
                SetPlayLocation(0);
            }
        }

        private void OnDrumsTapped(object sender, EventArgs e)
        {
            Vibrate();
            doDrums = !doDrums;
            imgDrums2.Source = doDrums ? "drums2.png" : "nodrums.png";
            UpdateChannelMatrix();
        }

        private void OnBassTapped(object sender, EventArgs e)
        {
            Vibrate();
            doBass = !doBass;
            imgBass2.Source = doBass ? "bass2.png" : "nobass.png";
            UpdateChannelMatrix();
        }

        private void OnGuitarTapped(object sender, EventArgs e)
        {
            Vibrate();
            doGuitar = !doGuitar;
            imgGuitar2.Source = doGuitar ? "guitar2.png" : "noguitar.png";
            UpdateChannelMatrix();
        }

        private void OnKeysTapped(object sender, EventArgs e)
        {
            Vibrate();
            doKeys = !doKeys;
            imgKeys2.Source = doKeys ? "keys2.png" : "nokeys.png";
            UpdateChannelMatrix();
        }

        private void OnVocalsTapped(object sender, EventArgs e)
        {
            Vibrate();
            doVocals = !doVocals;
            imgVocals2.Source = doVocals ? "vocals.png" : "novocals.png";
            UpdateChannelMatrix();
        }

        private void OnCrowdTapped(object sender, EventArgs e)
        {
            Vibrate();
            doCrowd = !doCrowd;
            imgCrowd.Source = doCrowd ? "crowd.png" : "nocrowd.png";
            UpdateChannelMatrix();
        }

        private void OnBackingTapped(object sender, EventArgs e)
        {
            Vibrate();
            doBacking = !doBacking;
            imgBacking.Source = doBacking ? "backing.png" : "nobacking.png";
            UpdateChannelMatrix();
        }

        private void doInstruments()
        {
            switch (Parser.Songs[0].VocalParts)
            {
                case 2:
                    imgVocals.Source = "mic2.png";
                    break;
                case 3:
                    imgVocals.Source = "mic3.png";
                    break;
            }

            if (Parser.Songs[0].VocalParts > 0)
            {
                switch(Parser.Songs[0].VocalsDiff)
                {
                    case 1:
                        vocalsDifficulty.Source = "diff1.png";
                        break;
                    case 2:
                        vocalsDifficulty.Source = "diff2.png";
                        break;
                    case 3:
                        vocalsDifficulty.Source = "diff3.png";
                        break;
                    case 4:
                        vocalsDifficulty.Source = "diff4.png";
                        break;
                    case 5:
                        vocalsDifficulty.Source = "diff5.png";
                        break;
                    case 6:
                        vocalsDifficulty.Source = "diff6.png";
                        break;
                    case 7:
                        vocalsDifficulty.Source = "diff7.png";
                        break;
                }
            }

            switch (Parser.Songs[0].DrumsDiff)
            {
                case 1:
                    drumsDifficulty.Source = "diff1.png";
                    break;
                case 2:
                    drumsDifficulty.Source = "diff2.png";
                    break;
                case 3:
                    drumsDifficulty.Source = "diff3.png";
                    break;
                case 4:
                    drumsDifficulty.Source = "diff4.png";
                    break;
                case 5:
                    drumsDifficulty.Source = "diff5.png";
                    break;
                case 6:
                    drumsDifficulty.Source = "diff6.png";
                    break;
                case 7:
                    drumsDifficulty.Source = "diff7.png";
                    break;
            }
            if (Parser.Songs[0].DoubleBass)
            {
                drums2X.IsVisible = true;
            }

            switch (Parser.Songs[0].BassDiff)
            {
                case 1:
                    bassDifficulty.Source = "diff1.png";
                    break;
                case 2:
                    bassDifficulty.Source = "diff2.png";
                    break;
                case 3:
                    bassDifficulty.Source = "diff3.png";
                    break;
                case 4:
                    bassDifficulty.Source = "diff4.png";
                    break;
                case 5:
                    bassDifficulty.Source = "diff5.png";
                    break;
                case 6:
                    bassDifficulty.Source = "diff6.png";
                    break;
                case 7:
                    bassDifficulty.Source = "diff7.png";
                    break;
            }
            if (Parser.Songs[0].ProBassDiff > 0)
            {
                proBass.IsVisible = true;
                switch (Parser.Songs[0].ProBassDiff)
                {
                    case 1:
                        bassDifficulty.Source = "diff1.png";
                        break;
                    case 2:
                        bassDifficulty.Source = "diff2.png";
                        break;
                    case 3:
                        bassDifficulty.Source = "diff3.png";
                        break;
                    case 4:
                        bassDifficulty.Source = "diff4.png";
                        break;
                    case 5:
                        bassDifficulty.Source = "diff5.png";
                        break;
                    case 6:
                        bassDifficulty.Source = "diff6.png";
                        break;
                    case 7:
                        bassDifficulty.Source = "diff7.png";
                        break;
                }
            }

            switch (Parser.Songs[0].GuitarDiff)
            {
                case 1:
                    guitarDifficulty.Source = "diff1.png";
                    break;
                case 2:
                    guitarDifficulty.Source = "diff2.png";
                    break;
                case 3:
                    guitarDifficulty.Source = "diff3.png";
                    break;
                case 4:
                    guitarDifficulty.Source = "diff4.png";
                    break;
                case 5:
                    guitarDifficulty.Source = "diff5.png";
                    break;
                case 6:
                    guitarDifficulty.Source = "diff6.png";
                    break;
                case 7:
                    guitarDifficulty.Source = "diff7.png";
                    break;
            }
            if (Parser.Songs[0].ProGuitarDiff > 0)
            {
                proGuitar.IsVisible = true;
                switch (Parser.Songs[0].ProGuitarDiff)
                {
                    case 1:
                        guitarDifficulty.Source = "diff1.png";
                        break;
                    case 2:
                        guitarDifficulty.Source = "diff2.png";
                        break;
                    case 3:
                        guitarDifficulty.Source = "diff3.png";
                        break;
                    case 4:
                        guitarDifficulty.Source = "diff4.png";
                        break;
                    case 5:
                        guitarDifficulty.Source = "diff5.png";
                        break;
                    case 6:
                        guitarDifficulty.Source = "diff6.png";
                        break;
                    case 7:
                        guitarDifficulty.Source = "diff7.png";
                        break;
                }
            }

            switch (Parser.Songs[0].KeysDiff)
            {
                case 1:
                    keysDifficulty.Source = "diff1.png";
                    break;
                case 2:
                    keysDifficulty.Source = "diff2.png";
                    break;
                case 3:
                    keysDifficulty.Source = "diff3.png";
                    break;
                case 4:
                    keysDifficulty.Source = "diff4.png";
                    break;
                case 5:
                    keysDifficulty.Source = "diff5.png";
                    break;
                case 6:
                    keysDifficulty.Source = "diff6.png";
                    break;
                case 7:
                    keysDifficulty.Source = "diff7.png";
                    break;
            }
            if (Parser.Songs[0].ProKeysDiff > 0)
            {
                proKeys.IsVisible = true;
                switch (Parser.Songs[0].ProKeysDiff)
                {
                    case 1:
                        keysDifficulty.Source = "diff1.png";
                        break;
                    case 2:
                        keysDifficulty.Source = "diff2.png";
                        break;
                    case 3:
                        keysDifficulty.Source = "diff3.png";
                        break;
                    case 4:
                        keysDifficulty.Source = "diff4.png";
                        break;
                    case 5:
                        keysDifficulty.Source = "diff5.png";
                        break;
                    case 6:
                        keysDifficulty.Source = "diff6.png";
                        break;
                    case 7:
                        keysDifficulty.Source = "diff7.png";
                        break;
                }
            }
        }

        public async void LoadYouTubeVideo(string artist, string title)
        {
            imgYouTube.IsVisible = false;//reset
            _youtubeUrl = await GetYouTubeMusicVideoUrl(artist, title);

            if (!string.IsNullOrEmpty(_youtubeUrl))
            {
                imgYouTube.IsVisible = true;
            }
        }

        private async void OnAboutTapped(object sender, EventArgs e)
        {
            var message = "This is a simplified Android port of my Windows application, also named Visualizer, found within the tool suite Nautilus\n\n"
                + "Visualizer is designed to read Rock Band song files in Xbox 360 format (CON/LIVE) and do the following:\n" +
                "- read and display the song's metadata\n- convert and display the song's album art (when present)\n- display what instruments" +
                " are present in the song and their respective difficulties\n- read, decrypt (when needed), and play the song's audio\n\n" +
                "Like the original, Visualizer allows you to select which track(s) are played and which are muted during audio playback - just " +
                "click on the corresponding icon to mute/unmute a track\n\n" +
                "You can also select whether to auto play the audio upon loading a song (on by default), whether to play the entire song or play " +
                "only the preview clip that would play in game (on by default), and whether to loop the playback so it doesn't stop playing" +
                "\n\nIf you are on a higher end device with high screen density (where everything can fit nicely), Visualizer will show you an animation" +
                " that corresponds to the music just like cPlayer does - tap on the animation to cycle through the available animations - it's just for fun!" +
                "\n\nVisualizer will attempt to find lyrics that match the song - if it does it will display an icon that you can click to open the" +
                " lyrics within Visualizer - don't worry, the music will keep on playing even though you're away from the main form\n\n" +
                "Visualizer will also attempt to find a matching YouTube video for your song - if it finds one it will display an icon that you can " +
                "click to open in the YouTube app or your browser, depending on your device\n\n" +
                "The original Visualizer allowed you to capture an image that displayed the most important song data (metadata, album art, instrument data) - " +
                "and so does this Android version! Click on the Share icon at the bottom of the screen and Visualizer will take a screenshot and let you share it " +
                "via your device's share function\n\nYou can also click on the Imgur icon to take a screenshot and upload it directly to Imgur - the link" +
                " will be automatically copied to your clipboard so you can paste it anywhere\n\n" +
                "To open a Rock Band song file (any Rock Band game CON/LIVE file will work except The Beatles: Rock Band), click on the folder icon at the" +
                " bottom of the screen and select your file\n\nIf you need to download Rock Band song files, I highly recommend using RhythmVerse to do so" +
                " - just click on the RhythmVerse icon next to the folder icon and your browser will take you to the right page over at RhythmVerse.co\n\n" +
                "If the song's author information is present, you can click on the 'Authored by' label and Visualizer will open the RhythmVerse website and " +
                "try to locate that author for you - note that this will only work for songs charted by one author, not group releases with multiple authors\n\n" +
                "I am not affiliated with nor sponsored by RhythmVerse - I just think it's a neat website\n\n" + 
                "This is a learning experiment for me so anything and everything is subject to change from version to version.";
            Vibrate();
            await Navigation.PushAsync(new AboutPage(message));
        }

        private async void OnFolderTapped(object sender, EventArgs e)
        {
            try
            {
                Vibrate();
                var file = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select CON file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "*/*" } },
                    })
                });

                if (file != null)
                {                    
                    loadingIndicator.IsVisible = true;
                    loadingIndicator.IsRunning = true;

                    await Task.Delay(50);
                    ProcessCONFile(file.FullPath);

                    loadingIndicator.IsVisible = false;
                    loadingIndicator.IsRunning = false;                    
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"File selection failed: {ex.Message}", "OK");
            }
        }

        private async void OnAlbumArtTapped(object sender, EventArgs e)
        {
            Vibrate();
            await Navigation.PushModalAsync(new ImagePopupPage(originalAlbumArt));
        }

        private async void OnLyricsTapped(object sender, EventArgs e)
        {
            if (imgLyrics.BindingContext is string lyrics)
            {
                Vibrate();
                await Navigation.PushAsync(new LyricsPage(Parser.Songs[0], lyrics, originalAlbumArt));
            }
        }

        private async void OnYouTubeTapped(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_youtubeUrl))
            {
                Vibrate();

                if (Bass.ChannelIsActive(_mixerStream) == PlaybackState.Playing) //pause the music when opening YouTube
                {
                        Bass.ChannelPause(_mixerStream);
                        imgPlay.Source = "play.png";
                        _isPlaying = false;
                        _trackTimer?.Stop();                        
                }
                try
                {
                    await Launcher.OpenAsync($"vnd.youtube:{_youtubeUrl.Replace("https://www.youtube.com/watch?v=", "")}");
                }
                catch
                {
                    await Launcher.OpenAsync(_youtubeUrl);
                }
            }
        }

        private void Vibrate()
        {
            try
            {
                HapticFeedback.Perform(HapticFeedbackType.Click);
            }
            catch (FeatureNotSupportedException)
            {
                Console.WriteLine("⚠ Haptic feedback not supported on this device.");
            }
        }

        private async void ConvertXboxImage(byte[] xImage)
        {
            if (xImage == null || xImage.Count() == 0)
            {
                await DisplayAlert("Error", "Invalid or missing png_xbox file", "OK");
                return;
            }

            var buffer = new byte[4];
            var swap = new byte[4];

            //get filesize / 4 for number of times to loop
            //32 is the size of the HMX header to skip
            var loop = (xImage.Length - 32) / 4;

            //skip the HMX header
            var input = new MemoryStream(xImage, 32, xImage.Length - 32);

            //grab HMX header to compare against known headers
            var full_header = new byte[16];
            var file_header = new MemoryStream(xImage, 0, 16);
            file_header.Read(full_header, 0, 16);
            file_header.Dispose();

            //some games have a bunch of headers for the same files, so let's skip the varying portion and just
            //grab the part that tells us the dimensions and image format
            var short_header = new byte[11];
            file_header = new MemoryStream(xImage, 5, 11);
            file_header.Read(short_header, 0, 11);
            file_header.Dispose();

            byte[]? ddsBytes;

            // Create a MemoryStream to hold the DDS data
            using (var outputStream = new MemoryStream())
            {
                // Get DDS header and write it to memory
                var header = GetDDSHeader(full_header, short_header);
                outputStream.Write(header, 0, header.Length);

                // Read the image data, skipping the 32-byte HMX header
                var inputStream = new MemoryStream(xImage, 32, xImage.Length - 32); // No nested 'using'

                for (var x = 0; x < loop; x++)
                {
                    input.Read(buffer, 0, 4);

                    // Xbox images are byte-swapped, so we swap the bytes
                    swap[0] = buffer[1];
                    swap[1] = buffer[0];
                    swap[2] = buffer[3];
                    swap[3] = buffer[2];

                    outputStream.Write(swap, 0, 4);
                }

                // Clean up manually since we're not using 'using'
                inputStream.Dispose();

                // Return the DDS as a byte array
                ddsBytes = outputStream.ToArray();
            }            

            using var inStream = new MemoryStream(ddsBytes);
            var decoder = new BcDecoder();

            // Decode DDS to an Image<Rgba32>
            using Image<Rgba32> image = decoder.DecodeToImageRgba32(inStream);
            using var pngStream = new MemoryStream();
            image.Save(pngStream, new PngEncoder());
            pngStream.Seek(0, SeekOrigin.Begin);

            originalAlbumArt = pngStream.ToArray(); // Store the original quality image
            AlbumArtImage.Source = ImageSource.FromStream(() => new MemoryStream(originalAlbumArt));
        }

        private class AudioParticle
        {
            public SKPoint Position;
            public float Size;
            public SKColor Color;
            public float SpeedY;
        }

        protected override bool OnBackButtonPressed()
        {
            /*DisplayAlert("Debugging", $"Screen Height: {DeviceDisplay.MainDisplayInfo.Height}\nScreen Width: {DeviceDisplay.MainDisplayInfo.Width}" +
                    $"\nDevice Density: {DeviceDisplay.MainDisplayInfo.Density}", "OK");
            */

            Vibrate();
            if (backPressCount == 0)
            {
                backPressCount++;
                Toast.Make("Press back again to exit", CommunityToolkit.Maui.Core.ToastDuration.Short).Show();
                Task.Delay(2000).ContinueWith(_ => backPressCount = 0);
                return true;
            }

            // Stop audio playback
            if (_mixerStream != 0)
            {
                Bass.ChannelStop(_mixerStream);
                Bass.StreamFree(_mixerStream);
                _mixerStream = 0;
            }

            if (_bassStream != 0)
            {
                Bass.ChannelStop(_bassStream);
                Bass.StreamFree(_bassStream);
                _bassStream = 0;
            }

            // Free BASS resources
            Bass.Free();

            // Exit the application
            System.Diagnostics.Process.GetCurrentProcess().Kill();

            return base.OnBackButtonPressed();
        }
                
        private static readonly Dictionary<byte[], DDSInfo> KnownHeaders = new Dictionary<byte[], DDSInfo>(new ByteArrayComparer())
    {
        { new byte[] { 0x01, 0x04, 0x08, 0x00, 0x00, 0x00, 0x05, 0x00, 0x04, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00, 0x00 }, new DDSInfo { Format = "DXT1", Width = 1024, Height = 1024 } },
        { new byte[] { 0x01, 0x08, 0x18, 0x00, 0x00, 0x00, 0x05, 0x00, 0x04, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00, 0x00 }, new DDSInfo { Format = "DXT5", Width = 1024, Height = 1024 } },
        { new byte[] { 0x01, 0x04, 0x08, 0x00, 0x00, 0x00, 0x04, 0x00, 0x01, 0x00, 0x01, 0x80, 0x00, 0x00, 0x00, 0x00 }, new DDSInfo { Format = "DXT1", Width = 256, Height = 256 } },
        { new byte[] { 0x01, 0x08, 0x18, 0x00, 0x00, 0x00, 0x04, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00 }, new DDSInfo { Format = "DXT5", Width = 256, Height = 256 } },
        { new byte[] { 0x01, 0x04, 0x08, 0x00, 0x00, 0x00, 0x05, 0x00, 0x02, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x00 }, new DDSInfo { Format = "DXT1", Width = 512, Height = 512 } },
        { new byte[] { 0x01, 0x08, 0x18, 0x00, 0x00, 0x00, 0x05, 0x00, 0x02, 0x00, 0x02, 0x00, 0x02, 0x00, 0x00, 0x00 }, new DDSInfo { Format = "DXT5", Width = 512, Height = 512 } },
        { new byte[] { 0x01, 0x04, 0x00, 0x00, 0x00, 0x08, 0x05, 0x02, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 }, new DDSInfo { Format = "DXT1", Width = 512, Height = 512 } },
        { new byte[] { 0x01, 0x08, 0x00, 0x00, 0x00, 0x18, 0x05, 0x02, 0x00, 0x02, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00 }, new DDSInfo { Format = "DXT5", Width = 512, Height = 512 } }
    };

        public byte[] GetDDSHeader(byte[] full_header, byte[] short_header)
        {
            foreach (var kvp in KnownHeaders)
            {
                if (full_header.Take(kvp.Key.Length).SequenceEqual(kvp.Key) ||
                    short_header.Take(kvp.Key.Length).SequenceEqual(kvp.Key))
                {                    
                    return BuildDDSHeader(kvp.Value.Format.ToLowerInvariant(), kvp.Value.Width, kvp.Value.Height);
                }
            }
            // Default fallback
            return BuildDDSHeader("dxt1", 256, 256);
        }

        private static byte[] BuildDDSHeader(string format, int width, int height)
        {
            var dds = new byte[] //512x512 DXT5 
                {
                    0x44, 0x44, 0x53, 0x20, 0x7C, 0x00, 0x00, 0x00, 0x07, 0x10, 0x0A, 0x00, 0x00, 0x02, 0x00, 0x00,
                    0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x4E, 0x45, 0x4D, 0x4F, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00,
                    0x04, 0x00, 0x00, 0x00, 0x44, 0x58, 0x54, 0x35, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                };

            switch (format.ToLowerInvariant())
            {
                case "dxt1":
                    dds[87] = 0x31;
                    break;
                case "dxt3":
                    dds[87] = 0x33;
                    break;
                case "normal":
                    dds[84] = 0x41;
                    dds[85] = 0x54;
                    dds[86] = 0x49;
                    dds[87] = 0x32;
                    break;
            }

            switch (height)
            {
                case 8:
                    dds[12] = 0x08;
                    dds[13] = 0x00;
                    break;
                case 16:
                    dds[12] = 0x10;
                    dds[13] = 0x00;
                    break;
                case 32:
                    dds[12] = 0x20;
                    dds[13] = 0x00;
                    break;
                case 64:
                    dds[12] = 0x40;
                    dds[13] = 0x00;
                    break;
                case 128:
                    dds[12] = 0x80;
                    dds[13] = 0x00;
                    break;
                case 256:
                    dds[13] = 0x01;
                    break;
                case 1024:
                    dds[13] = 0x04;
                    break;
                case 2048:
                    dds[13] = 0x08;
                    break;
            }

            switch (width)
            {
                case 8:
                    dds[16] = 0x08;
                    dds[17] = 0x00;
                    break;
                case 16:
                    dds[16] = 0x10;
                    dds[17] = 0x00;
                    break;
                case 32:
                    dds[16] = 0x20;
                    dds[17] = 0x00;
                    break;
                case 64:
                    dds[16] = 0x40;
                    dds[17] = 0x00;
                    break;
                case 128:
                    dds[16] = 0x80;
                    dds[17] = 0x00;
                    break;
                case 256:
                    dds[17] = 0x01;
                    break;
                case 1024:
                    dds[17] = 0x04;
                    break;
                case 2048:
                    dds[17] = 0x08;
                    break;
            }

            if (width == height)
            {
                switch (width)
                {
                    case 8:
                        dds[0x1C] = 0x00; //no mipmaps at this size
                        break;
                    case 16:
                        dds[0x1C] = 0x05;
                        break;
                    case 32:
                        dds[0x1C] = 0x06;
                        break;
                    case 64:
                        dds[0x1C] = 0x07;
                        break;
                    case 128:
                        dds[0x1C] = 0x08;
                        break;
                    case 256:
                        dds[0x1C] = 0x09;
                        break;
                    case 1024:
                        dds[0x1C] = 0x0B;
                        break;
                    case 2048:
                        dds[0x1C] = 0x0C;
                        break;
                }
            }
            return dds;
        }
    }

public class DDSInfo
    {
        public string? Format { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    // Helper class for comparing byte arrays as dictionary keys
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y) => x.SequenceEqual(y);
        public int GetHashCode(byte[] obj) => obj.Sum(b => b);
    }

}
