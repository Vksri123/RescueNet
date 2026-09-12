using System.Threading.Tasks;

namespace ResuceNet.Services
{
    public interface IPriorityService
    {
        Task<(string PriorityLevel, int RiskScore)> AnalyzePriorityAsync(string description, string emergencyType);
    }
}
