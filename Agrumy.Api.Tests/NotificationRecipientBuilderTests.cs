using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Notifications;
using Moq;

namespace Agrumy.Api.Tests;

/// Opt-out model: a user absent from the disabled-channels result stays enabled by default, only an explicit "email" entry excludes them.
public class NotificationRecipientBuilderTests
{
    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);

    [Fact]
    public async Task NoOverrides_EveryoneWithEmailIncluded()
    {
        var users = new List<User>
        {
            new() { IDUser = 1, Email = "a@example.com" },
            new() { IDUser = 2, Email = "b@example.com" },
        };
        _users.Setup(u => u.NotificationDisabledChannelsGetAsync(It.IsAny<IEnumerable<int>>(), NotificationEventType.Offline))
              .ReturnsAsync(new Dictionary<int, HashSet<string>>());

        var recipients = await NotificationRecipientBuilder.FilterAsync(_users.Object, users, NotificationEventType.Offline);

        Assert.Equal(2, recipients.Count);
    }

    [Fact]
    public async Task UserOptedOutOfEmailForThisEventType_IsExcluded_OthersUnaffected()
    {
        var users = new List<User>
        {
            new() { IDUser = 1, Email = "a@example.com" },
            new() { IDUser = 2, Email = "b@example.com" },
        };
        _users.Setup(u => u.NotificationDisabledChannelsGetAsync(It.IsAny<IEnumerable<int>>(), NotificationEventType.LowBattery))
              .ReturnsAsync(new Dictionary<int, HashSet<string>> { [1] = new HashSet<string> { NotificationChannels.Email } });

        var recipients = await NotificationRecipientBuilder.FilterAsync(_users.Object, users, NotificationEventType.LowBattery);

        Assert.Single(recipients);
        Assert.Equal("b@example.com", recipients[0].Email);
    }

    [Fact]
    public async Task OptOutIsPerEventType_DoesNotAffectAnotherEventType()
    {
        var users = new List<User> { new() { IDUser = 1, Email = "a@example.com" } };
        // Opted out of LowBattery specifically - Offline for the same user is a separate row/lookup and stays enabled.
        _users.Setup(u => u.NotificationDisabledChannelsGetAsync(It.IsAny<IEnumerable<int>>(), NotificationEventType.Offline))
              .ReturnsAsync(new Dictionary<int, HashSet<string>>());

        var recipients = await NotificationRecipientBuilder.FilterAsync(_users.Object, users, NotificationEventType.Offline);

        Assert.Single(recipients);
    }

    [Fact]
    public async Task NoUsersWithEmail_SkipsRepositoryCallEntirely()
    {
        // Strict mock with zero .Setup() calls - if FilterAsync called NotificationDisabledChannelsGetAsync here it would throw.
        var users = new List<User> { new() { IDUser = 1, Email = null } };

        var recipients = await NotificationRecipientBuilder.FilterAsync(_users.Object, users, NotificationEventType.TankRefill);

        Assert.Empty(recipients);
    }
}
