using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMProOld;

namespace SilksongArabic;

// Opt-in measurements only. Bounds warnings are review candidates, not proof
// of clipping: some game titles intentionally extend beyond their rectangles.
internal static class OverflowDiagnostics
{
    static string Flag => Path.Combine(Plugin.Folder,"overflow-audit.flag");
    static string F(float n) => n.ToString("0.###",CultureInfo.InvariantCulture);
    static string Clean(string text) => text.Replace("\t"," ").Replace("\r","").Replace("\n","<br>");
    static readonly HashSet<string> Seen = new();
    static bool failed;

    static void WrapRegression()
    {
        var go=new GameObject("Arabic short label measurement",typeof(RectTransform),typeof(Text));
        go.transform.position=new Vector3(10000,10000,10000);
        try
        {
            var label=go.GetComponent<Text>();
            label.font=Plugin.UIFont;label.fontSize=30;label.resizeTextForBestFit=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;
            label.verticalOverflow=VerticalWrapMode.Overflow;
            var input=string.Join(" ",Plugin.Lines["MainMenu\tSAVE_WARNING"].Split(' ').Take(4));
            var settings=label.GetGenerationSettings(new Vector2(100000,100000));
            settings.horizontalOverflow=HorizontalWrapMode.Overflow;
            var generator=new TextGenerator();
            float Width(string s) => generator.GetPreferredWidth(s,settings)/label.pixelsPerUnit;
            float original=Width(Plugin.VisualLine(input));
            float narrow=input.Split(' ').Max(w=>Width(Plugin.VisualLine(w)))*1.08f;
            label.rectTransform.sizeDelta=new Vector2(narrow,300);
            string wrapped=Plugin.VisualUI(label,input);
            float maxWidth=wrapped.Split('\n').Max(Width);
            bool shortLabelPass=input.Length<=45 && original>narrow && !wrapped.Contains("\n");
            label.rectTransform.sizeDelta=new Vector2(original*1.2f,300);
            bool widePass=!Plugin.VisualUI(label,input).Contains("\n");
            label.rectTransform.sizeDelta=new Vector2(narrow,300);
            label.horizontalOverflow=HorizontalWrapMode.Overflow;
            bool explicitOverflowPass=!Plugin.VisualUI(label,input).Contains("\n");
            label.horizontalOverflow=HorizontalWrapMode.Wrap;
            var longInput=Plugin.Lines["MainMenu\tSAVE_WARNING"].Replace("<br>"," ").Replace("\n"," ");
            float longRect=longInput.Split(' ').Max(w=>Width(Plugin.VisualLine(w)))*1.1f;
            label.rectTransform.sizeDelta=new Vector2(longRect,1000);
            var longWrapped=Plugin.VisualUI(label,longInput);
            bool longTextPass=longInput.Length>45 && longWrapped.Contains("\n") && longWrapped.Split('\n').Max(Width)<=longRect;
            File.WriteAllText(Path.Combine(Plugin.Folder,"short-label-regression.txt"),
                $"version=0.1.4; characters={input.Length}; unwrappedWidth={F(original)}; narrowRect={F(narrow)}; shortLabelLines={wrapped.Split('\n').Length}; shortLabelPass={shortLabelPass}; widePass={widePass}; explicitOverflowPass={explicitOverflowPass}; longTextPass={longTextPass}\n");
        }
        finally { UnityEngine.Object.Destroy(go); }
    }

    static void Scan()
    {
        foreach(var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if(!text.enabled || !text.gameObject.activeInHierarchy || !Plugin.HasArabic(text.text) || text.font==null)continue;
            // Inspect existing layout; do not force updates or alter game text.
            if(text.textInfo==null || text.textInfo.characterCount==0)continue;
            var rect=text.rectTransform.rect;var bounds=text.textBounds;
            if(rect.width<=0 || rect.height<=0)continue;
            float tolerance=Math.Max(.05f,Math.Min(rect.width,rect.height)*.02f);
            bool width=bounds.size.x>rect.width+tolerance;
            bool height=bounds.size.y>rect.height+tolerance;
            if(!width && !height)continue;
            string path=LayoutDiagnostics.PathOf(text.transform);
            string id=path+"\t"+text.text+"\t"+F(rect.width)+"\t"+F(rect.height);
            if(Seen.Count>=2000 || !Seen.Add(id))continue;
            File.AppendAllText(Path.Combine(Plugin.Folder,"overflow-candidates.tsv"),
                $"{DateTime.UtcNow:O}\t{Clean(path)}\twidth={width}\theight={height}\trect={F(rect.width)}x{F(rect.height)}\tbounds={F(bounds.size.x)}x{F(bounds.size.y)}\tfontSize={F(text.fontSize)}\tlines={text.textInfo.lineCount}\t{Clean(text.text)}\n");
        }
    }

    internal static IEnumerator Run()
    {
        yield return null;
        try
        {
            if(!Plugin.EnsureFonts())yield break;
            File.AppendAllText(Path.Combine(Plugin.Folder,"overflow-candidates.tsv"),$"# Session {DateTime.UtcNow:O}; candidates require visual review\n");
            WrapRegression();
        }
        catch(Exception e) { failed=true;Plugin.LoggerInfo("Overflow diagnostics disabled: "+e.Message); }
        while(!failed && File.Exists(Flag))
        {
            try { Scan(); }
            catch(Exception e) { failed=true;Plugin.LoggerInfo("Overflow diagnostics disabled: "+e.Message); }
            yield return new WaitForSecondsRealtime(2f);
        }
    }
}
