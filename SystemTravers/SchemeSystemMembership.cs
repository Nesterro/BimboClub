using System;
using System.Collections.Generic;
using System.Linq;

namespace BimboClub
{
    internal static class SchemeSystemMembership
    {
        private static readonly char[] Separators = { ',', ';', '\r', '\n' };

        internal static bool ContainsSystem(string value, string systemName)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(systemName)) return false;
            // Preserve exact matches even if the system name itself contains a separator.
            if (string.Equals(value.Trim(), systemName.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return value.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                .Any(name => string.Equals(name.Trim(), systemName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        internal static List<string> GetMatchingValues(string systemName, IEnumerable<string> values)
        {
            if (string.IsNullOrWhiteSpace(systemName)) throw new ArgumentException("Не задано имя системы.", nameof(systemName));
            var matches = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { systemName };
            foreach (string value in values)
                if (ContainsSystem(value, systemName)) matches.Add(value);
            return matches.ToList();
        }
    }
}
