using System;
using System.Text.RegularExpressions;
namespace SilksongArabicSetup
{
    public static class SupportReport
    {
        public static string Redact(string text,string gameFolder,string profile)
        {return ReplacePath(ReplacePath(text,gameFolder,"[GAME]"),profile,"[USER]");}
        static string ReplacePath(string text,string path,string replacement)
        {
            if(string.IsNullOrWhiteSpace(path)||path.Trim().Length<4)return text;
            string pattern=string.Join(@"[\\/]",Array.ConvertAll(path.Trim().TrimEnd('\\','/').Replace('\\','/').Split('/'),Regex.Escape));
            return Regex.Replace(text,pattern,replacement,RegexOptions.IgnoreCase);
        }
    }
}
