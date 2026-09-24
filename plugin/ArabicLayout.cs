using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SilksongArabic;

internal static class ArabicLayout
{
    static readonly Regex Tags = new("(<[^>]*>)");
    static readonly Regex LtrRuns = new("[A-Za-z0-9][A-Za-z0-9 _./:+#™%×–—-]*[A-Za-z0-9™%]|[A-Za-z0-9]");
    static string Reverse(string s) => new string(s.Reverse().ToArray());
    static char Mirror(char c) => c switch { '('=>')', ')'=>'(', '['=>']', ']'=>'[', '{'=>'}', '}'=>'{', _=>c };

    internal static string ForRtlLayout(string input)
    {
        return string.Concat(Tags.Split(input).Select(part => part.StartsWith("<")
            ? part : LtrRuns.Replace(new string(part.Select(Mirror).ToArray()), m=>Reverse(m.Value))));
    }

    // Preserve formatting on each visible character before visual reordering.
    // This also reconstructs nested color/size tags with their original values.
    internal static string VisualLine(string input)
    {
        var active = new List<string>();
        var glyphs = new List<(string value, string[] style)>();
        foreach (Match token in Regex.Matches(ForRtlLayout(input), "<[^>]*>|.", RegexOptions.Singleline))
        {
            var value = token.Value;
            if (Regex.IsMatch(value, "^<(b|i|size|color|material)(?:=[^>]*)?>$", RegexOptions.IgnoreCase))
                active.Add(value);
            else if (Regex.IsMatch(value, "^</(b|i|size|color|material)>$", RegexOptions.IgnoreCase))
            {
                var name = value.Substring(2, value.Length-3);
                int index = active.FindLastIndex(t => TagName(t).Equals(name, StringComparison.OrdinalIgnoreCase));
                if(index>=0) active.RemoveRange(index, active.Count-index);
            }
            else glyphs.Add((value, active.ToArray()));
        }
        var result = new StringBuilder();
        active.Clear();
        glyphs.Reverse();
        foreach(var glyph in glyphs)
        {
            int common=0;
            while(common<active.Count && common<glyph.style.Length && active[common]==glyph.style[common]) common++;
            for(int i=active.Count-1;i>=common;i--) result.Append("</").Append(TagName(active[i])).Append('>');
            for(int i=common;i<glyph.style.Length;i++) result.Append(glyph.style[i]);
            active.Clear(); active.AddRange(glyph.style);
            result.Append(glyph.value);
        }
        for(int i=active.Count-1;i>=0;i--) result.Append("</").Append(TagName(active[i])).Append('>');
        return result.ToString();
    }

    static string TagName(string tag) => Regex.Match(tag, "^<([^=>]+)").Groups[1].Value;
}
