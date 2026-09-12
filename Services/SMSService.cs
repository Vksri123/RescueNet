using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ResuceNet.Services
{
    public class SMSService : ISMSService
    {
        private readonly HttpClient _httpClient;
        private static SMSGatewayConfig _config = new SMSGatewayConfig();

        public SMSService(IConfiguration configuration, HttpClient httpClient)
        {
            _httpClient = httpClient;

            var section = configuration.GetSection("SMSGateway");
            if (section.Exists())
            {
                _config.Provider = section["Provider"] ?? "Simulation";
                _config.ApiKey = section["ApiKey"] ?? "";
            }
        }

        public SMSGatewayConfig GetConfig() => _config;

        public void UpdateConfig(SMSGatewayConfig newConfig)
        {
            _config = newConfig;
        }

        public async Task<(bool Success, string StatusMessage)> SendSMSAsync(string phoneNumber, string message)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return (false, "Invalid phone number");
            }

            phoneNumber = phoneNumber.Trim();

            try
            {
                if (_config.Provider.Equals("Fast2SMS", StringComparison.OrdinalIgnoreCase) 
                    && !string.IsNullOrEmpty(_config.ApiKey))
                {
                    // Fast2SMS Gateway (India)
                    // Sanitize phone number to 10-digit Indian format for Fast2SMS
                    var cleanPhone = phoneNumber.Replace(" ", "").Replace("-", "").Replace("+91", "").Replace("+", "");
                    if (cleanPhone.StartsWith("91") && cleanPhone.Length == 12)
                    {
                        cleanPhone = cleanPhone.Substring(2);
                    }

                    var encodedMsg = Uri.EscapeDataString(message);
                    var url = $"https://www.fast2sms.com/dev/bulkV2?authorization={Uri.EscapeDataString(_config.ApiKey)}&route=q&message={encodedMsg}&language=english&flash=0&numbers={cleanPhone}";

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    var response = await _httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var resStr = await response.Content.ReadAsStringAsync();
                        return (true, $"Fast2SMS delivered to {cleanPhone}: {resStr}");
                    }
                    else
                    {
                        var errBody = await response.Content.ReadAsStringAsync();
                        return (false, $"Fast2SMS API Error ({response.StatusCode}): {errBody}");
                    }
                }
                else
                {
                    // Simulation mode fallback
                    return (true, $"Simulated SMS to {phoneNumber}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"SMS Gateway Error: {ex.Message}");
            }
        }
    }
}
