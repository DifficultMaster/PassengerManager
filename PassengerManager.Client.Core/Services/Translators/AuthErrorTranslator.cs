using PassengerManager.Client.Core.Resources;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Shared.Protos;
using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.Services.Translators
{
    public class AuthErrorTranslator : IAuthErrorTranslator
    {
        public string Translate(AuthResultCode code)
        {
            return code switch
            {
                AuthResultCode.AccountLockout => AuthErrors.AccountLockout,
                AuthResultCode.CredentialOverdue => AuthErrors.CredentialOverdue,
                AuthResultCode.InvalidLogin => AuthErrors.InvalidLogin,
                AuthResultCode.InvalidLoginFormat => AuthErrors.InvalidLoginFormat,
                AuthResultCode.InvalidMode => AuthErrors.InvalidMode,
                AuthResultCode.InvalidPassword => AuthErrors.InvalidPassword,
                AuthResultCode.InvalidPasswordFormat => AuthErrors.InvalidPasswordFormat,
                AuthResultCode.InvalidPasswordHistory => AuthErrors.InvalidPasswordHistory,                       
                AuthResultCode.InvalidRole => AuthErrors.InvalidRole,
                AuthResultCode.InvalidTarget => AuthErrors.InvalidTarget,
                AuthResultCode.InvalidVehicle => AuthErrors.InvalidVehicle,
                AuthResultCode.Unauthorized => AuthErrors.Unauthorized,    
                
                _ => AuthErrors.Unknown
            };
        }
    }
}
