using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MailAddressExtractor;

public sealed class YtChannelContext : DbContext
{
    public DbSet<ChannelInfo> ChannelInfos { get; set; }
    public DbSet<AboutRequest> AboutRequests { get; set; }

    public YtChannelContext()
    {
        Database.Migrate();
    }

    public YtChannelContext(DbContextOptions<YtChannelContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .SetBasePath(Directory.GetCurrentDirectory())
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection");
        optionsBuilder.UseNpgsql(connectionString);
    }

    public async Task AddChannelInfoRangeAsync(IEnumerable<ChannelInfo> channelInfos)
    {
        var existingItemsInDb = ChannelInfos
            .Where(dbItem => channelInfos.Select(x => x.Email)
                .Contains(dbItem.Email))
            .ToList();

        var itemsNotInDb = channelInfos
            .Where(x => !existingItemsInDb.Any(dbItem => dbItem.Name == x.Name && dbItem.Email == x.Email));

        await ChannelInfos.AddRangeAsync(itemsNotInDb);

        await SaveChangesAsync();
    }

    public Task<bool> ChannelInfosAnyAsync()
    {
        return ChannelInfos.AnyAsync();
    }

    public async Task AddOrUpdateCursorAsync(AboutRequest aboutRequest)
    {
        var requestInfo = await AboutRequests.FirstOrDefaultAsync(x => x.KeyWord == aboutRequest.KeyWord);

        if (requestInfo is null)
        {
            await AboutRequests.AddAsync(aboutRequest);
        }
        else
        {
            requestInfo.Cursor = aboutRequest.Cursor;
            AboutRequests.Update(requestInfo);
        }

        await SaveChangesAsync();
    }

    public Task<AboutRequest?> GetCursorByKeyword(string keyword)
    {
        return AboutRequests.FirstOrDefaultAsync(x => x.KeyWord == keyword);
    }
}

public record ChannelInfo
{
    public long Id { get; set; }
    public string Description { get; set; }
    public string? Email { get; set; }
    public string Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string KeyWord { get; set; }
    public long SubscriberCount { get; set; }
}

public record AboutRequest
{
    public long Id { get; set; }
    public string Cursor { get; set; }
    public string KeyWord { get; set; }
}