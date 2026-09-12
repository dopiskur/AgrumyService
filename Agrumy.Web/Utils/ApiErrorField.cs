using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Agrumy.Web.Utils
{
    /// Picks which form field an API validation message belongs under by matching the message's words against each property's name and Display name; empty (model-level) when nothing matches.
    public static class ApiErrorField
    {
        public static string Resolve<T>(string message)
        {
            var words = Regex.Matches(message, "[A-Za-z]{3,}").Select(m => m.Value.ToLowerInvariant()).ToHashSet();
            string best = string.Empty;
            int bestScore = 0;
            foreach (PropertyInfo p in typeof(T).GetProperties())
            {
                IEnumerable<string> tokens = Regex.Matches(p.Name, "[A-Z][a-z]+|[A-Z]+(?![a-z])").Select(m => m.Value)
                    .Concat(Regex.Matches(p.GetCustomAttribute<DisplayAttribute>()?.Name ?? "", "[A-Za-z]+").Select(m => m.Value));
                int score = tokens.Where(t => t.Length >= 3).Select(t => t.ToLowerInvariant()).Distinct().Count(words.Contains);
                if (score > bestScore)
                {
                    best = p.Name;
                    bestScore = score;
                }
            }
            return best;
        }
    }
}
