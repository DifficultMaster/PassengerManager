using PassengerManager.Shared.Protos;
using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.Services.Interfaces
{
    public interface IAuthErrorTranslator
    {
        string Translate(AuthResultCode code);
    }
}
