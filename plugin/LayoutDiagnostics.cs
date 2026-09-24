using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using TMProOld;

namespace SilksongArabic;

// Read-only runtime measurements. Enabled only by a local developer flag.
internal static class LayoutDiagnostics
{
    static bool regressionDone;
    static void CompareMetrics()
    {
        if(regressionDone || !Plugin.EnsureFonts())return;
        regressionDone=true;
        var old=UnityEngine.Object.Instantiate(Plugin.GameFont);
        var face=old.fontInfo;
        face.Scale=1;face.Ascender=64*1.374f;face.Descender=-64*.738f;face.LineHeight=64*2.112f;
        old.AddFaceInfo(face);old.ReadFontDefinition();
        var go=new GameObject("Arabic layout measurement",typeof(TextMeshPro));
        go.transform.position=new Vector3(10000,10000,10000);
        var text=go.GetComponent<TextMeshPro>();text.isOrthographic=false;
        text.rectTransform.pivot=new Vector2(.5f,1);
        text.rectTransform.sizeDelta=new Vector2(50,5);text.fontSize=10;
        text.alignment=TextAlignmentOptions.Top;text.enableWordWrapping=true;
        // Set Arabic once through the normal patch; each font switch below uses
        // the already parsed character buffer, keeping every other setting equal.
        text.text=Plugin.Lines["MainMenu\tSAVE_WARNING"].Replace("<br>","\n");
        text.ForceMeshUpdate();
        float fixedHeight=text.textBounds.size.y;
        var fixedBaseline=text.textInfo.lineInfo[1].baseline;
        // Compare the old metrics on the same Arabic font, synchronously before
        // a frame is rendered, and immediately restore the corrected metrics.
        var fixedFace=Plugin.GameFont.fontInfo;
        float oldHeight;
        try
        {
            Plugin.GameFont.AddFaceInfo(face);Plugin.GameFont.ReadFontDefinition();
            text.ForceMeshUpdate();oldHeight=text.textBounds.size.y;
        }
        finally { Plugin.GameFont.AddFaceInfo(fixedFace);Plugin.GameFont.ReadFontDefinition(); }
        text.ForceMeshUpdate();
        File.AppendAllText(Path.Combine(Plugin.Folder,"layout-regression.txt"),
            $"Save warning: old height={oldHeight}; fixed height={fixedHeight}; lines={text.textInfo.lineCount}; second baseline={fixedBaseline}; improved={fixedHeight<oldHeight}\n");
        if(!(fixedHeight<oldHeight) || text.textInfo.lineCount!=2)Plugin.LoggerInfo("Layout regression requires inspection.");
        UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(old);
    }
    internal static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent)+"/"+t.name;
    internal static IEnumerator Run()
    {
        var seen=new HashSet<string>();
        string path=Path.Combine(Plugin.Folder,"layout-audit.txt");
        File.WriteAllText(path,"Arabic UI layout audit\n");
        if(File.Exists(Path.Combine(Plugin.Folder,"layout-regression.flag")))CompareMetrics();
        for(int frame=0;frame<240;frame++)
        {
            foreach(var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if(!t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text) || t.font==null)continue;
                string id=t.GetInstanceID()+"\t"+t.text;
                if(!seen.Add(id))continue;
                t.ForceMeshUpdate();
                var f=t.font.fontInfo;
                var bounds=t.textBounds;
                File.AppendAllText(path,$"{PathOf(t.transform)}\ttext={t.text}\tfont={t.font.name}\tsize={t.fontSize}\talign={t.alignment}\trect={t.rectTransform.rect}\tpos={t.transform.localPosition}\tlines={t.textInfo.lineCount}\tbounds={bounds}\tface={f.PointSize},{f.Ascender},{f.Descender},{f.LineHeight},{f.CapHeight}\n");
            }
            yield return new WaitForSecondsRealtime(.25f);
        }
    }
}
