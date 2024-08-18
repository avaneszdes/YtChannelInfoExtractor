using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MailAddressExtractor;

public class Program
{
    public static async Task Main(string[] args)
    {
        await FetchAndSaveData(
            "learn english, learn spanish, learn german, learn japanese, learn korean, learn portuguese, learn italian, learn arabic");
    }

    static List<ChannelInfo> ParseChannelInfo(List<ChannelInfo> channelInfosToWork)
    {
        const string phoneNumberPattern = @"(?:WhatsApp\s*[:+-]?\s*)?(\+?\d{1,4}[-.\s]?\(?\d{1,4}\)?[-.\s]?\d{1,4}[-.\s]?\d{1,4}[-.\s]?\d{1,9})";
        const string emailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";

        var channelInfos = new List<ChannelInfo>();

        foreach (var record in channelInfosToWork)
        {
            var emailMatches = Regex.Matches(record.Description, emailPattern);
            var phoneNumberMatches = Regex.Matches(record.Description, phoneNumberPattern);

            if (emailMatches.Count > 0 || phoneNumberMatches.Count > 0)
            {
                var email = string.Join(",", emailMatches);
                var phoneNumber = string.Join(",", phoneNumberMatches);

                channelInfos.Add(record with { Email = email, PhoneNumber = phoneNumber});
            }
        }

        return channelInfos;
    }

    static async Task WaitRandom()
    {
        // Generate a random number between 6000 and 25000 milliseconds (6 to 25 seconds)
        var random = new Random();
        var randomDuration = random.Next(6000, 25001);
        await Task.Delay(randomDuration);
    }

    // Function to build the request URL
    static string BuildUrl(string baseUrl, string keyword, string cursor)
    {
        return $"{baseUrl}?q={Uri.EscapeDataString(keyword)}&sortTypeId=1&cursor={cursor}";
    }

    // Function to fetch data from API
    static async Task<string> FetchData(string url, Dictionary<string, object> parameters)
    {
        using var client = new HttpClient();
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(parameters), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);

            if ((int)response.StatusCode == 429)
            {
                throw new Exception("Rate limit exceeded (status code 429)");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching data: {ex.Message}");
            throw;
        }
    }

    // Main function to orchestrate data fetching and saving
    static async Task FetchAndSaveData(string keywords)
    {
        const string baseUrl = "https://api.playboard.co/v1/search/channel";
        var parameters = new Dictionary<string, object>
        {
            { "subscribers", new { type = "range", from = 50, to = 100000000 } }
        };

        var keywordArray = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false
        };

        foreach (var keyword in keywordArray)
        {
            var encodedKeyword = keyword.Replace(", ", " ").Trim();
            var cursor = string.Empty;
            
            await using (var db = new YtChannelContext())
            {
                cursor = (await db.GetCursorByKeyword(keyword))?.Cursor;
            }
            
            Console.WriteLine($"Starting data fetch for keyword: {encodedKeyword}");

            while (true)
            {
                try
                {
                    var url = BuildUrl(baseUrl, encodedKeyword, cursor);
                    Console.WriteLine($"Making request to URL: {url}");
                    var jsonData = await FetchData(url, parameters);
                    var jsonDoc = JsonDocument.Parse(jsonData);
                    var jsonDataList = jsonDoc.RootElement.GetProperty("list");
                    cursor = jsonDoc.RootElement.GetProperty("cursor").ToString();

                    var channelInfos = jsonDataList.Deserialize<List<ChannelInfo>>(options);

                    if (string.IsNullOrEmpty(cursor))
                    {
                        Console.WriteLine("No more pages to fetch.");
                        break;
                    }

                    if (channelInfos is not null && channelInfos.Count != 0)
                    {
                        Console.WriteLine($"Fetched {channelInfos.Count} items for keyword: {encodedKeyword}");

                        var channelInfosToSave = ParseChannelInfo(channelInfos);

                        await using (var db = new YtChannelContext())
                        {
                            await db.AddChannelInfoRangeAsync(channelInfosToSave);
                        }
                        
                        await using (var db = new YtChannelContext())
                        {
                            await db.AddOrUpdateCursorAsync(new AboutRequest()
                            {
                                Cursor = cursor,
                                KeyWord = encodedKeyword
                                
                            });
                        }
                    }
                    else
                    {
                        Console.WriteLine("No more data available.");
                        break;
                    }

                    await WaitRandom();
                }
                catch (Exception)
                {
                    break; // Exit loop on error
                }
            }
        }
    }
}