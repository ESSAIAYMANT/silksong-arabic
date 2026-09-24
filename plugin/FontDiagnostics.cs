using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMProOld;

namespace SilksongArabic;

// Opt-in development preview; it neither loads nor changes any save.
internal static class FontDiagnostics
{
    internal static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(6);
        if(!Plugin.EnsureFonts())yield break;
        var source=Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f=>f!=Plugin.GameFont && f.material!=null);
        if(source==null)source=Plugin.GameFont;
        var root=new GameObject("Arabic Font Regression Preview");
        UnityEngine.Object.DontDestroyOnLoad(root);
        var cameraObject=new GameObject("Arabic Preview Camera",typeof(Camera));
        cameraObject.transform.SetParent(root.transform);
        cameraObject.transform.position=new Vector3(10000,10000,-10);
        var camera=cameraObject.GetComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=4.5f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.06f);
        camera.cullingMask=1<<31;camera.depth=1000;
        string title=Plugin.Lines["Map Zones\tMOSS_CAVE"];
        string dialogue=Plugin.Lines["Bonebottom\tBB_CARETAKER_MEET_1"].Split(new[]{"<hpage>"},StringSplitOptions.None)[0];
        void World(string label,string value,float y,float size,float height)
        {
            var go=new GameObject(label,typeof(TextMeshPro));go.layer=31;go.transform.SetParent(root.transform);
            go.transform.position=new Vector3(10000,10000+y,0);
            var t=go.GetComponent<TextMeshPro>();t.font=source;t.isOrthographic=true;
            t.rectTransform.sizeDelta=new Vector2(14,height);t.fontSize=size;t.color=Color.white;
            t.alignment=TextAlignmentOptions.Center;t.enableWordWrapping=true;t.text=value;t.ForceMeshUpdate();
        }
        World("World Title",title,3.6f,.6f,1);
        World("World Dialogue",dialogue,1.8f,.36f,2.2f);
        var canvasObject=new GameObject("Arabic Canvas Preview",typeof(Canvas));canvasObject.transform.SetParent(root.transform);
        var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
        canvas.additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.TexCoord2|AdditionalCanvasShaderChannels.Normal|AdditionalCanvasShaderChannels.Tangent;
        var goUI=new GameObject("Arabic UI Dialogue",typeof(RectTransform),typeof(TextMeshProUGUI));goUI.transform.SetParent(canvasObject.transform,false);
        var ui=goUI.GetComponent<TextMeshProUGUI>();ui.font=source;ui.fontSize=32;ui.color=Color.white;
        ui.rectTransform.anchorMin=ui.rectTransform.anchorMax=new Vector2(.5f,.22f);
        ui.rectTransform.sizeDelta=new Vector2(Screen.width*.86f,250);
        ui.alignment=TextAlignmentOptions.Center;ui.enableWordWrapping=true;ui.text=dialogue;ui.ForceMeshUpdate();
        Plugin.LoggerInfo("Font regression preview ready: world title, world dialogue, and UGUI dialogue. No save loaded.");
        yield return new WaitForSecondsRealtime(180);
        UnityEngine.Object.Destroy(root);
    }
}
