using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using BepInEx;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using OldText = TMProOld.TMP_Text;
using OldFont = TMProOld.TMP_FontAsset;

namespace SilksongArabic;

[BepInPlugin("local.silksong.arabic", "Arabic Translation", "0.1.4")]
public sealed class Plugin : BaseUnityPlugin
{
    internal static Plugin Instance;
    internal static readonly Dictionary<string,string> Lines = new();
    internal static readonly Dictionary<string,string> VisualCache = new();
    internal static Font UIFont;
    internal static OldFont GameFont;
    internal static bool FontFailed;
    internal static string Folder;
    static readonly Regex Arabic = new("[\\u0600-\\u06ff\\ufb50-\\ufdff\\ufe70-\\ufeff]");
    static readonly ConditionalWeakTable<OldText, OriginalFont> Originals = new();
    sealed class OriginalFont { internal OldFont Font; internal Material Material; }
    static readonly FieldInfo UIBacking = AccessTools.Field(typeof(Text), "m_Text");

    void Awake()
    {
        Instance = this;
        Folder = Path.GetDirectoryName(Info.Location);
        var xml = new XmlDocument();
        xml.Load(Path.Combine(Folder, "arabic.xml"));
        foreach (XmlElement e in xml.SelectNodes("/translation/entry"))
            Lines[e.GetAttribute("sheet") + "\t" + e.GetAttribute("key")] = e.InnerText;
        new Harmony("local.silksong.arabic").PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo($"Loaded {Lines.Count} original Arabic entries. Build target: 22479045.");
        if(File.Exists(Path.Combine(Folder,"font-regression.flag"))) StartCoroutine(FontDiagnostics.Run());
        if(File.Exists(Path.Combine(Folder,"layout-diagnostics.flag"))) StartCoroutine(LayoutDiagnostics.Run());
        if(File.Exists(Path.Combine(Folder,"overflow-audit.flag"))) StartCoroutine(OverflowDiagnostics.Run());
    }

    internal static bool HasArabic(string s) => s != null && Arabic.IsMatch(s);

    internal static bool EnsureFonts()
    {
        if (GameFont != null && UIFont != null) return true;
        if (FontFailed) return false;
        try
        {
            var fontBundle = AssetBundle.LoadFromFile(Path.Combine(Folder, "arabicfont.bundle"));
            if(fontBundle==null) throw new Exception("Cannot load Arabic font bundle");
            UIFont = fontBundle.LoadAsset<Font>("assets/codexarabicnoto.ttf");
            if(UIFont==null) throw new Exception("Arabic UI font is missing");
            fontBundle.Unload(false);
            if(UIFont.material==null) UIFont.material = new Material(Shader.Find("UI/Default"));
            UIFont.RequestCharactersInTexture("ﺍﻟﺴﻼﻡ ABC 123", 32, FontStyle.Normal);
            LoggerInfo($"UI font: {UIFont.name}, dynamic={UIFont.dynamic}, material={UIFont.material}");
            // Populate only required characters first. A broad Unicode range can
            // fill the single atlas before essential contextual Arabic forms.
            var characters = Lines.Values.SelectMany(s=>s).Where(c=>!char.IsControl(c))
                .Select(c=>(uint)c).Distinct().OrderBy(c=>c).ToArray();
            string legacyAudit="not requested";
            if(File.Exists(Path.Combine(Folder,"font-regression.flag")))
            {
                var probe=FontAsset.CreateFontAsset(UIFont,64,8,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,false);
                var oldSet=Enumerable.Range(32,224).Concat(Enumerable.Range(0x600,256))
                    .Concat(Enumerable.Range(0xfb50,0x4b0))
                    .Concat(new[]{0x2013,0x2014,0x2018,0x2019,0x201c,0x201d,0x2026,0x2122}).Select(c=>(uint)c).ToArray();
                probe.TryAddCharacters(oldSet,out uint[] _,false);
                var absent=characters.Where(c=>!probe.characterLookupTable.ContainsKey(c)).ToArray();
                legacyAudit=string.Join(" ",absent.Select(c=>$"U+{c:X4}"));
                LoggerInfo($"Regression: old atlas misses {absent.Length} required characters: {legacyAudit}");
                Destroy(probe.atlasTexture); Destroy(probe.material); Destroy(probe);
            }
            int atlasSize=2048;
            var modern = FontAsset.CreateFontAsset(UIFont, 64, 8, GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, false);
            modern.TryAddCharacters(characters, out uint[] missing, false);
            if(missing!=null && missing.Length>0)
            {
                Destroy(modern.atlasTexture); Destroy(modern.material); Destroy(modern);
                atlasSize=4096;
                modern=FontAsset.CreateFontAsset(UIFont,64,8,GlyphRenderMode.SDFAA,atlasSize,atlasSize,AtlasPopulationMode.Dynamic,false);
                modern.TryAddCharacters(characters,out missing,false);
            }
            var absentRequired=characters.Where(c=>!modern.characterLookupTable.ContainsKey(c)).ToArray();
            if(absentRequired.Length>0)
                throw new Exception("Arabic font is incomplete: "+string.Join(" ",absentRequired.Select(c=>$"U+{c:X4}")));
            var info = modern.faceInfo;
            // Noto reserves vertical room for stacked marks and scripts absent
            // from this atlas (2.112 em). TMP uses it for top alignment and line
            // advance, pushing Arabic buttons below their selection highlight.
            // Fit the actual exported glyphs instead, retaining every glyph and
            // a small leading gap. No transform/animation offsets are changed.
            var drawnGlyphs=modern.characterTable.Where(c=>c.glyph.metrics.height>0).ToArray();
            float inkTop=drawnGlyphs.Max(c=>c.glyph.metrics.horizontalBearingY);
            float inkBottom=drawnGlyphs.Min(c=>c.glyph.metrics.horizontalBearingY-c.glyph.metrics.height);
            float arabicLineHeight=inkTop-inkBottom+info.pointSize*.05f;
            GameFont = ScriptableObject.CreateInstance<OldFont>();
            GameFont.name = "Noto Sans Arabic - Original Localization";
            GameFont.AddFaceInfo(new TMProOld.FaceInfo {
                Name=info.familyName, PointSize=info.pointSize, Scale=info.scale*.9f,
                LineHeight=arabicLineHeight, Baseline=info.baseline, Ascender=inkTop,
                CapHeight=info.capLine, Descender=inkBottom, CenterLine=info.meanLine,
                SuperscriptOffset=info.superscriptOffset, SubscriptOffset=info.subscriptOffset,
                SubSize=info.subscriptSize, Underline=info.underlineOffset,
                UnderlineThickness=info.underlineThickness, TabWidth=info.tabWidth,
                Padding=8, AtlasWidth=atlasSize, AtlasHeight=atlasSize,
                CharacterCount=modern.characterTable.Count
            });
            GameFont.AddGlyphInfo(modern.characterTable.Select(c=>new TMProOld.TMP_Glyph {
                id=(int)c.unicode, x=c.glyph.glyphRect.x,
                y=atlasSize-c.glyph.glyphRect.y-c.glyph.metrics.height,
                width=c.glyph.metrics.width, height=c.glyph.metrics.height,
                xOffset=c.glyph.metrics.horizontalBearingX,
                yOffset=c.glyph.metrics.horizontalBearingY,
                xAdvance=c.glyph.metrics.horizontalAdvance, scale=1
            }).ToArray());
            GameFont.AddKerningInfo(new TMProOld.KerningTable());
            GameFont.fallbackFontAssets = new List<OldFont>();
            GameFont.fontAssetType = OldFont.FontAssetTypes.SDF;
            GameFont.atlas = modern.atlasTexture;
            GameFont.material = new Material(modern.material);
            GameFont.material.name = "Arabic SDF";
            GameFont.ReadFontDefinition();
            var absentOld=characters.Where(c=>!GameFont.characterDictionary.ContainsKey((int)c)).ToArray();
            if(absentOld.Length>0) throw new Exception("Converted Arabic glyph table is incomplete.");
            File.WriteAllText(Path.Combine(Folder,"font-audit.txt"),
                $"Required: {characters.Length}\nMissing required: {absentRequired.Length}\nMissing converted: {absentOld.Length}\nAtlas: {atlasSize}\nLegacy missing: {legacyAudit}\nOriginal line height: {info.lineHeight}\nArabic line height: {arabicLineHeight}\nArabic ink bounds: {inkBottom} .. {inkTop}\nArabic scale: {GameFont.fontInfo.Scale}\n");
            DontDestroyOnLoad(GameFont); DontDestroyOnLoad(GameFont.atlas);
            DontDestroyOnLoad(GameFont.material); DontDestroyOnLoad(modern);
            LoggerInfo($"Arabic font ready: {modern.characterTable.Count} glyphs, {modern.atlasTextureCount} atlas.");
            return true;
        }
        catch (Exception ex) { FontFailed=true; Instance.Logger.LogError(ex); return false; }
    }

    internal static void LoggerInfo(string s) => Instance.Logger.LogInfo(s);
    internal static string VisualLine(string input) => ArabicLayout.VisualLine(input);

    internal static string VisualUI(Text label, string input)
    {
        var width=label.rectTransform.rect.width;
        var key=input+"\t"+label.fontSize+"\t"+width+"\t"+label.horizontalOverflow;
        if(VisualCache.TryGetValue(key,out var cached)) return cached;
        var logical=input.Replace("<br>","\n").Replace("<br/>","\n");
        var settings=label.GetGenerationSettings(new Vector2(100000,100000));
        settings.font=UIFont; settings.horizontalOverflow=HorizontalWrapMode.Overflow;
        settings.verticalOverflow=VerticalWrapMode.Overflow;
        var generator=new TextGenerator();
        var lines=new List<string>();
        foreach(var paragraph in logical.Split('\n'))
        {
            // Restore the established single-line treatment of short labels.
            // Menu button rectangles are narrower than their intended visible
            // titles; wrapping them here splits Start Game / Quit Game.
            if(label.horizontalOverflow==HorizontalWrapMode.Overflow || width<=1 || paragraph.Length<=45) {lines.Add(VisualLine(paragraph));continue;}
            string line="";
            foreach(var word in paragraph.Split(' '))
            {
                var next=line.Length==0?word:line+" "+word;
                float measured=generator.GetPreferredWidth(VisualLine(next),settings)/label.pixelsPerUnit;
                if(measured>width*0.97f && line.Length>0){ lines.Add(VisualLine(line));line=word; }
                else line=next;
            }
            lines.Add(VisualLine(line));
        }
        cached=string.Join("\n",lines); VisualCache[key]=cached; return cached;
    }

    [HarmonyPatch(typeof(Language), nameof(Language.Get), new[]{typeof(string),typeof(string)})]
    static class TranslationPatch
    {
        static void Postfix(string key,string sheetTitle,ref string __result)
        {
            if(Language.CurrentLanguage()==LanguageCode.EN && Lines.TryGetValue(sheetTitle+"\t"+key,out var translated)) __result=translated;
        }
    }

    [HarmonyPatch(typeof(OldText), "StringToCharArray")]
    static class TmpPatch
    {
        static void Prefix(OldText __instance,ref string text)
        {
            bool ar=HasArabic(text);
            __instance.isRightToLeftText=ar;
            if(!ar)
            {
                if(Originals.TryGetValue(__instance,out var original) && __instance.font==GameFont)
                { __instance.font=original.Font; __instance.fontSharedMaterial=original.Material; }
                return;
            }
            if(!EnsureFonts())return;
            if(__instance.font!=GameFont)
            {
                Originals.Remove(__instance);
                var original = new OriginalFont {Font=__instance.font,Material=__instance.fontSharedMaterial};
                Originals.Add(__instance,original);
                if(original.Font!=null && !GameFont.fallbackFontAssets.Contains(original.Font))
                    GameFont.fallbackFontAssets.Add(original.Font);
                __instance.font=GameFont;
                if(original.Material!=null)
                    __instance.fontSharedMaterial=TMProOld.TMP_MaterialManager.GetFallbackMaterial(original.Material,GameFont.material);
            }
            text=ArabicLayout.ForRtlLayout(text);
        }
    }

    [HarmonyPatch(typeof(Text), "OnPopulateMesh")]
    static class UiPatch
    {
        static void Prefix(Text __instance, out string __state)
        {
            __state=null;
            string s=__instance.text;
            if(!HasArabic(s)||!EnsureFonts())return;
            __state=s;
            UIBacking.SetValue(__instance, VisualUI(__instance,s));
        }
        static void Postfix(Text __instance,string __state)
        {
            if(__state!=null)UIBacking.SetValue(__instance,__state);
        }
    }

    [HarmonyPatch(typeof(Text), "set_text")]
    static class UiTextPatch
    {
        static void Prefix(Text __instance,string value)
        {
            if(HasArabic(value)&&EnsureFonts()&&__instance.font!=UIFont)
                __instance.font=UIFont;
        }
    }

    [HarmonyPatch(typeof(Text), nameof(Text.GetGenerationSettings))]
    static class UiLayoutPatch
    {
        static void Postfix(Text __instance,ref TextGenerationSettings __result)
        {
            if(!HasArabic(__instance.text))return;
            // Lines have already been wrapped before bidi reordering. Letting
            // uGUI wrap the visual string again reverses the paragraph order.
            __result.horizontalOverflow=HorizontalWrapMode.Overflow;
            __result.verticalOverflow=VerticalWrapMode.Overflow;
            __result.lineSpacing=Math.Max(1f,__result.lineSpacing);
        }
    }

    [HarmonyPatch(typeof(Text), "OnEnable")]
    static class UiEnablePatch
    {
        static void Prefix(Text __instance)
        {
            if(HasArabic(__instance.text)&&EnsureFonts()&&__instance.font!=UIFont)
                __instance.font=UIFont;
        }
    }
}
