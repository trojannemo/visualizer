using System.Net.Http.Headers;
using System.Text.Json;

namespace Visualizer
{
    public class ImgurUploader
    {
        private const string ClientId = "248d1718f9e6925"; //please get your own

        public async Task<string> UploadToImgurAsync(string imagePath)
        {
            string link = string.Empty;
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Client-ID", ClientId);

                    var imageBytes = await File.ReadAllBytesAsync(imagePath);
                    var base64Image = Convert.ToBase64String(imageBytes);

                    var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "image", base64Image }
                });

                    var response = await client.PostAsync("https://api.imgur.com/3/upload.json", content);
                    response.EnsureSuccessStatusCode();

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    if (doc.RootElement.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("link", out var linkElement))
                    {
                        link = linkElement.GetString();
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                string errorMessage = ex.Message.Contains("429") ?
                    "Error Code 429: Rate limiting\nToo many uploads recently. Try again in a few hours." :
                    ex.Message.Contains("500") ?
                    "Error Code 500: Unexpected internal error\nTry again later." :
                    "Unknown error: " + ex.Message;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert("Upload Error", errorMessage, "OK");
                });
            }

            return link;
        }
    }
}
