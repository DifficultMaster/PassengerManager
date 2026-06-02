using PassengerManager.Client.Core.Services.Translators;
using PassengerManager.Shared.Protos;

namespace PassengerManager.Tests.Unit.ClientCore;

public class AuthErrorTranslatorTests
{
    [TestCase(AuthResultCode.AccountLockout, "AccountLockout")]
    [TestCase(AuthResultCode.CredentialOverdue, "CredentialOverdue")]
    [TestCase(AuthResultCode.InvalidLogin, "InvalidLogin")]
    [TestCase(AuthResultCode.InvalidLoginFormat, "InvalidLoginFormat")]
    [TestCase(AuthResultCode.InvalidMode, "InvalidMode")]
    [TestCase(AuthResultCode.InvalidPassword, "InvalidPassword")]
    [TestCase(AuthResultCode.InvalidPasswordFormat, "InvalidPasswordFormat")]
    [TestCase(AuthResultCode.InvalidPasswordHistory, "InvalidPasswordHistory")]
    [TestCase(AuthResultCode.InvalidRole, "InvalidRole")]
    [TestCase(AuthResultCode.InvalidTarget, "InvalidTarget")]
    [TestCase(AuthResultCode.InvalidVehicle, "InvalidVehicle")]
    [TestCase(AuthResultCode.Unauthorized, "Unauthorized")]
    [TestCase(AuthResultCode.Unknown, "Unknown")]
    public void TranslateReturnsResourceKey(AuthResultCode code, string expectedKey)
    {
        AuthErrorTranslator translator = new();

        string message = translator.Translate(code);

        Assert.That(message, Does.Contain(expectedKey));
    }
}
