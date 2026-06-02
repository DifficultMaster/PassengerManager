using PassengerManager.Server.Services.Security;

namespace PassengerManager.Tests.Unit.Server;

public class PasswordHandlerTests
{
    [Test]
    public void HashingSamePasswordReturnsSameHash()
    {
        string hash1 = PasswordHandler.GetHashedPassword("password");
        string hash2 = PasswordHandler.GetHashedPassword("password");

        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public void VerifyPasswordReturnsTrueForMatchingPassword()
    {
        string hash = PasswordHandler.GetHashedPassword("password");

        bool result = PasswordHandler.VerifyPassword("password", hash);

        Assert.That(result, Is.True);
    }

    [Test]
    public void VerifyPasswordReturnsFalseForDifferentPassword()
    {
        string hash = PasswordHandler.GetHashedPassword("password");

        bool result = PasswordHandler.VerifyPassword("other", hash);

        Assert.That(result, Is.False);
    }
}
