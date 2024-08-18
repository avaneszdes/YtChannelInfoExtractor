using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MailAddressExtractor;

record Data
{
    public bool HasMore { get; set; }
    public int RecordNumber { get; set; }
    public List<ChannelInfo> Records { get; set; }
}

public partial class Program
{
    private const string PhoneNumberPattern = @"(?:WhatsApp\s*[:+-]?\s*)?(\+?\d{1,4}[-.\s]?\(?\d{1,4}\)?[-.\s]?\d{1,4}[-.\s]?\d{1,4}[-.\s]?\d{1,9})";
    private const string EmailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";
    
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };
    
    public static async Task Main(string[] args)
    {
        await ExtractAndSaveDataFromJsonAsync("FilesToWork");
        await FetchAndSaveData(
            "learn english, learn spanish, learn german, learn japanese, learn korean, learn portuguese, learn italian, learn arabic");
    }

    private static List<ChannelInfo> ParseChannelInfo(List<ChannelInfo> channelInfosToWork)
    {
        var channelInfos = new List<ChannelInfo>();

        foreach (var record in channelInfosToWork)
        {
            var emailMatches = EmailRegex().Matches(record.Description);
            var phoneNumberMatches = PhoneNumberRegex().Matches(record.Description);

            if (emailMatches.Count > 0 || phoneNumberMatches.Count > 0)
            {
                var email = string.Join(",", emailMatches);
                var phoneNumber = string.Join(",", phoneNumberMatches);

                channelInfos.Add(record with { Email = email, PhoneNumber = phoneNumber});
            }
        }

        return channelInfos;
    }

    private static async Task WaitRandom()
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
    private static async Task<string> FetchData(string url, Dictionary<string, object> parameters)
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
    private static async Task FetchAndSaveData(string keywords)
    {
        const string baseUrl = "https://api.playboard.co/v1/search/channel";
        var parameters = new Dictionary<string, object>
        {
            { "subscribers", new { type = "range", from = 50, to = 100000000 } }
        };

        var keywordArray = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries);

       

        foreach (var keyword in keywordArray)
        {
            var encodedKeyword = keyword.Replace(", ", " ").Trim();
            string cursor;
            
            await using (var db = new YtChannelContext())
            {
                cursor = (await db.GetCursorByKeyword(keyword))?.Cursor ?? string.Empty;
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

                    var channelInfos = jsonDataList.Deserialize<List<ChannelInfo>>(Options);

                    if (string.IsNullOrEmpty(cursor))
                    {
                        Console.WriteLine("No more pages to fetch.");
                        break;
                    }

                    if (channelInfos is not null && channelInfos.Count != 0)
                    {
                        Console.WriteLine($"Fetched {channelInfos.Count} items for keyword: {encodedKeyword}");

                        var channelInfosToSave = ParseChannelInfo(channelInfos);

                        await using var db = new YtChannelContext();
                        await db.AddChannelInfoRangeAsync(channelInfosToSave);
                            
                        await db.AddOrUpdateCursorAsync(new AboutRequest()
                        {
                            Cursor = cursor,
                            KeyWord = encodedKeyword
                                
                        });
                        
                        Console.WriteLine($"{channelInfos.Count} records saved in db");
                        
                        Console.WriteLine("Cursor updated");
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

    private static string GetAssemblyLocation()
    {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyLocation is null)
        {
            throw new FileNotFoundException("Assembly Location folder not found");
        }

        return assemblyLocation;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="folderName">Folder name. Path is bin/debug/net8.0/FilesToWork.</param>
    private static async Task ExtractAndSaveDataFromJsonAsync(string folderName)
    {
        await using var db = new YtChannelContext();

        var isChannelInfosEmpty = await db.ChannelInfosAnyAsync();

        if (isChannelInfosEmpty)
        {
            Console.WriteLine("ChannelInfos table is not empty starting fetch new data...");
            return;
        }
        
        var assemblyLocation = GetAssemblyLocation();
        var folderLocation = GetFolderLocation(assemblyLocation, folderName);
        
        var fileNames = Directory.GetFiles(folderLocation);

        if (fileNames.Length == 0)
        {
            Console.WriteLine("Files not exist");
            return;
        }

        var channelInfosToSave = await ParseJsonFilesAsync(fileNames);

        await db.AddChannelInfoRangeAsync(channelInfosToSave);
    }

    private static async Task<List<ChannelInfo>> ParseJsonFilesAsync(string[] fileNames)
    {
        var channelInfos = new List<ChannelInfo>();

        foreach (var fileName in fileNames)
        {
            await using var openStream = File.OpenRead(fileName);

            if (openStream.Length > 0)
            {
                var data = await JsonSerializer.DeserializeAsync<Data>(openStream, Options);

                if (data is null)
                {
                    continue;
                }

                channelInfos = ParseChannelInfo(data.Records);
            }
        }

        return channelInfos;
    }

    private static string GetFolderLocation(string assemblyLocation, string folderName)
    {
        var folderLocation = Path.Combine(assemblyLocation, folderName);

        if (!Directory.Exists(folderLocation))
        {
            throw new FileNotFoundException($"Directory ({folderLocation}) not found");
        }

        return folderLocation;
    }
    
    [GeneratedRegex(PhoneNumberPattern)]
    private static partial Regex PhoneNumberRegex();
    [GeneratedRegex(EmailPattern)]
    private static partial Regex EmailRegex();
}