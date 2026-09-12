using System;
using System.Threading.Tasks;

namespace ResuceNet.Services
{
    public class PriorityService : IPriorityService
    {
        public Task<(string PriorityLevel, int RiskScore)> AnalyzePriorityAsync(string description, string emergencyType)
        {
            if (string.IsNullOrEmpty(description))
            {
                return Task.FromResult(("Medium", 40));
            }

            string descLower = description.ToLowerInvariant();
            int score = 0;

            // 1. Base score by type
            switch (emergencyType.ToLowerInvariant())
            {
                case "earthquake":
                    score += 55;
                    break;
                case "fire":
                    score += 50;
                    break;
                case "flood":
                    score += 45;
                    break;
                case "accident":
                    score += 35;
                    break;
                case "medical":
                    score += 30;
                    break;
                default:
                    score += 20;
                    break;
            }

            // 2. Keyword check (Critical markers)
            string[] criticalKeywords = { "trapped", "drowning", "dying", "unconscious", "cannot breathe", "rapidly rising", "sinking", "explosion", "suffocating", "severe bleeding" };
            foreach (var kw in criticalKeywords)
            {
                if (descLower.Contains(kw))
                {
                    score += 25;
                }
            }

            // 3. Keyword check (High markers)
            string[] highKeywords = { "injured", "child", "children", "baby", "elderly", "smoke", "crushed", "rising water", "trapped parents", "parents", "burns" };
            foreach (var kw in highKeywords)
            {
                if (descLower.Contains(kw))
                {
                    score += 12;
                }
            }

            // 4. Keyword check (Medium markers)
            string[] mediumKeywords = { "pain", "broken", "leak", "damage", "stuck", "assistance", "blocked" };
            foreach (var kw in mediumKeywords)
            {
                if (descLower.Contains(kw))
                {
                    score += 5;
                }
            }

            // Cap the score at 100
            score = Math.Min(100, score);
            score = Math.Max(10, score); // minimum score is 10

            // 5. Map score to level
            string priorityLevel;
            if (score >= 80)
            {
                priorityLevel = "Critical";
            }
            else if (score >= 60)
            {
                priorityLevel = "High";
            }
            else if (score >= 35)
            {
                priorityLevel = "Medium";
            }
            else
            {
                priorityLevel = "Low";
            }

            return Task.FromResult((priorityLevel, score));
        }
    }
}
