using System;
using System.Linq;
using BimboClub;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++;
    }
    private static void Main()
    {
        const string selected = "В7.3";
        const string shared = "В7.3,П7.3,В7.3";
        Check(shared != selected, "Original exact inequality reproduces the hidden AHU");
        foreach (var value in new[] { selected, shared, "П7.3,В7.3", "П7.3,В7.3,П8", " В7.3 ", "П7.3,  В7.3 , В8", "П7.3; В7.3", "П7.3\r\nВ7.3", "в7.3", "П7.3,\tВ7.3", "П7.3,\u00a0В7.3" })
            Check(SchemeSystemMembership.ContainsSystem(value, selected), "Includes exact member: " + value);
        foreach (var value in new[] { "В7.30", "В7.3A", "АВ7.3", "В7", "П7.3,В7.30", "П7.3", "B7.3", "", " ", null })
            Check(!SchemeSystemMembership.ContainsSystem(value, selected), "Excludes unrelated member: " + value);
        Check(!SchemeSystemMembership.ContainsSystem(shared, ""), "Empty selected system never matches");
        var values = SchemeSystemMembership.GetMatchingValues(selected, new[] { shared, shared, "В7.30", "П7.3", "П7.3, В7.3", " В7.3 ", null });
        Check(values.Count == 4, "Deduplicates full values, preserves whitespace and canonical single-system value");
        Check(values.Contains(shared) && values.Contains("П7.3, В7.3"), "Keeps the original full parameter strings for native equality rules");
        Func<string, bool> hidden = value => values.All(accepted => !string.Equals(value, accepted, StringComparison.OrdinalIgnoreCase));
        Check(!hidden(shared) && !hidden(selected), "Conjunction of not-equals keeps shared and single-system equipment");
        Check(hidden("В7.30") && hidden("П7.3"), "Conjunction hides unrelated equipment");
        var secondSystem = SchemeSystemMembership.GetMatchingValues("П7.3", new[] { shared });
        Check(secondSystem.Contains(shared), "Same AHU also appears on its second system scheme");
        var refreshed = SchemeSystemMembership.GetMatchingValues(selected, new[] { "П9,В7.3" });
        Check(refreshed.Contains("П9,В7.3") && !refreshed.Contains(shared), "Repeated generation refreshes changed system combinations");
        Console.WriteLine($"PASS: {checks} membership checks; production matcher, no Revit/model loaded.");
    }
}
