using Grpc.Core;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Shared.Protos;
using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.Services
{
    public class GrpcAuthService : IAuthService
    {
        private readonly AuthService.AuthServiceClient _client;

        public GrpcAuthService(AuthService.AuthServiceClient client)
        {
            _client = client;
        }

        public async Task<HardwareLoginResponse> AuthenticateHardwareAsync(HardwareLoginRequest request)
        {
            try
            {
                return await _client.HardwareLoginAsync(request);
            }
            catch (RpcException ex)
            {
                return new HardwareLoginResponse
                {
                    Success = false,
                    Message = $"Network error: {ex.Status.Detail}",
                    Code = AuthResultCode.Unknown
                };
            }
            catch (Exception ex)
            {
                return new HardwareLoginResponse
                {
                    Success = false,
                    Message = $"Unhandled local exception",
                    Code = AuthResultCode.Unknown
                };
            }
        }

        public async Task<DriverLoginResponse> AuthenticateDriverAsync(DriverLoginRequest request)
        {
            try
            {
                return await _client.DriverLoginAsync(request);
            }
            catch (RpcException ex)
            {
                return new DriverLoginResponse
                {
                    Success = false,
                    Message = $"Network error: {ex.Status.Detail}",
                    Code = AuthResultCode.Unknown
                };
            }
            catch (Exception ex)
            {
                return new DriverLoginResponse
                {
                    Success = false,
                    Message = $"Unhandled local exception",
                    Code = AuthResultCode.Unknown
                };
            }
        }

        public async Task<PasswordChangeResponse> ChangeDriverPasswordAsync(PasswordChangeRequest request, string tempToken)
        {
            try
            {
                Metadata headers = new Metadata();

                if (!string.IsNullOrWhiteSpace(tempToken))
                    headers.Add("Authorization", $"Bearer {tempToken}");

                return await _client.PasswordChangeAsync(request, headers: headers);
            }
            catch (RpcException ex)
            {
                return new PasswordChangeResponse
                {
                    Success = false,
                    Message = $"Network error: {ex.Status.Detail}",
                    Code = AuthResultCode.Unknown
                };
            }
            catch
            {
                return new PasswordChangeResponse
                {
                    Success = false,
                    Message = $"Unhandled local exception",
                    Code = AuthResultCode.Unknown
                };
            }
        }
    }
}
