using System.Text.RegularExpressions;

namespace MyDocuMgm.Infrastructure.ExternalFetch;

public sealed class RobotsPolicyEvaluator
{
    private readonly string _productToken;

    public RobotsPolicyEvaluator(string productToken = ExternalFetchOptions.RobotsProductToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productToken);
        _productToken = productToken;
    }

    public bool IsAllowed(string robotsText, Uri target)
    {
        var groups = Parse(robotsText);
        var exact = groups
            .Where(group => group.UserAgents.Any(agent =>
                !string.Equals(agent, "*", StringComparison.Ordinal) &&
                string.Equals(_productToken, agent, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var selected = exact.Length > 0
            ? exact
            : groups.Where(group => group.UserAgents.Contains("*", StringComparer.Ordinal)).ToArray();
        var path = string.IsNullOrEmpty(target.PathAndQuery) ? "/" : target.PathAndQuery;
        var matched = selected
            .SelectMany(group => group.Rules)
            .Where(rule => Matches(rule.Pattern, path))
            .OrderByDescending(rule => RuleLength(rule.Pattern))
            .ThenByDescending(rule => rule.Allow)
            .FirstOrDefault();
        return matched is null || matched.Allow;
    }

    private static IReadOnlyList<Group> Parse(string text)
    {
        var groups = new List<Group>();
        var current = new Group();
        foreach (var rawLine in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Split('#', 2)[0].Trim();
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (name.Equals("user-agent", StringComparison.OrdinalIgnoreCase))
            {
                if (current.Rules.Count > 0)
                {
                    groups.Add(current);
                    current = new Group();
                }

                if (value.Length > 0)
                {
                    current.UserAgents.Add(value.ToLowerInvariant());
                }

                continue;
            }

            if (current.UserAgents.Count == 0 ||
                !(name.Equals("allow", StringComparison.OrdinalIgnoreCase) ||
                  name.Equals("disallow", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (value.Length == 0 && name.Equals("disallow", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            current.Rules.Add(new Rule(
                name.Equals("allow", StringComparison.OrdinalIgnoreCase),
                value));
        }

        if (current.UserAgents.Count > 0)
        {
            groups.Add(current);
        }

        return groups;
    }

    private static bool Matches(string pattern, string path)
    {
        if (pattern.Length == 0)
        {
            return true;
        }

        var anchored = pattern.EndsWith('$');
        var value = anchored ? pattern[..^1] : pattern;
        var expression = "^" + Regex.Escape(value).Replace("\\*", ".*", StringComparison.Ordinal);
        if (anchored)
        {
            expression += "$";
        }

        return Regex.IsMatch(path, expression, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    private static int RuleLength(string pattern) => pattern.Count(character => character is not ('*' or '$'));

    private sealed class Group
    {
        public List<string> UserAgents { get; } = [];
        public List<Rule> Rules { get; } = [];
    }

    private sealed record Rule(bool Allow, string Pattern);
}
