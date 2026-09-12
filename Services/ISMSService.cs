using System.Threading.Tasks;

namespace ResuceNet.Services
{
    public interface ISMSService
    {
        Task<(bool Success, string StatusMessage)> SendSMSAsync(string phoneNumber, string message);
        SMSGatewayConfig GetConfig();
        void UpdateConfig(SMSGatewayConfig newConfig);
    }

    public class SMSGatewayConfig
    {
        public string Provider { get; set; } = "Simulation"; // 'Fast2SMS', 'Simulation'
        public string ApiKey { get; set; } = string.Empty;
    }
}
