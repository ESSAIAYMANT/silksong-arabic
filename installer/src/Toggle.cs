using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SilksongArabicSetup
{
    public sealed partial class Engine
    {
        const string PluginDll=PluginDir+"SilksongArabic.dll";
        const string ParkedDll=StateDir+"/disabled/SilksongArabic.dll";
        const string ToggleJournal=StateDir+"/toggle.xml";
        public Action<int> AfterToggleStep; // Fault injection in the separate transaction tests.
        sealed class ToggleInfo { public bool Disabled; }
        string PluginHash()
        {
            var file=package.Files.FirstOrDefault(f=>f.Path.Equals(PluginDll,StringComparison.OrdinalIgnoreCase));
            if(file==null)throw new IOException("ملف تشغيل التعريب غير موجود في هذه الحزمة.");return file.Hash;
        }
        ToggleInfo ReadToggle(string root)
        {
            string marker=Within(root,ToggleJournal),active=Within(root,PluginDll),parked=Within(root,ParkedDll);
            if(Directory.Exists(marker)||Directory.Exists(parked))throw new IOException("مجلد يتعارض مع سجل التعطيل أو ملف التعريب المحفوظ.");
            if(!File.Exists(marker))
            {
                if(File.Exists(parked))throw new IOException("توجد نسخة معطلة بلا سجل صالح. احتفظ بها وعالج السجل قبل التبديل.");
                return null;
            }
            XDocument doc;using(var input=File.OpenRead(marker))doc=Package.ReadXml(input);
            if(doc.Root==null||doc.Root.Name!="toggle"||(string)doc.Root.Attribute("format")!="1"
                ||!string.Equals((string)doc.Root.Attribute("root"),root,StringComparison.OrdinalIgnoreCase)
                ||(string)doc.Root.Attribute("sha256")!=PluginHash())
                throw new IOException("سجل التعطيل لا يطابق هذا المجلد أو إصدار الملحق. استخدم المثبت الذي عطّله.");
            bool a=File.Exists(active),p=File.Exists(parked);
            if(Directory.Exists(active)||a==p)throw new IOException("حالة التعطيل متعارضة: نسخة مفقودة أو نسختان موجودتان. لم يتغير أي ملف.");
            if(Package.HashFile(p?parked:active)!=PluginHash())throw new IOException("تغير ملف التعريب منذ التعطيل. لن يُستبدل أو يُحذف تلقائيًا.");
            // Active + marker means interruption before disable-move or after enable-move.
            return new ToggleInfo{Disabled=p};
        }
        string PayloadPath(string root,string relative)
        {
            if(relative.Equals(PluginDll,StringComparison.OrdinalIgnoreCase))
            {var state=ReadToggle(root);if(state!=null&&state.Disabled)return Within(root,ParkedDll);}
            return Within(root,relative);
        }
        void EnsureActive(string root)
        {if(ReadToggle(root)!=null)throw new IOException("التعريب معطل مؤقتًا أو توجد عملية تبديل غير مكتملة. استخدم «إعادة تفعيل التعريب» أو «إزالة التثبيت» أولًا.");}
        static bool IsRuntimeFile(PayloadFile file)
        {
            if(file.Kind=="loader")return file.Path.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||file.Path=="doorstop_config.ini";
            return file.Path==PluginDll||file.Path==PluginDir+"arabic.xml"||file.Path==PluginDir+"arabicfont.bundle"||file.Path==PluginDir+"NotoSansArabic-Regular.ttf";
        }
        void CheckRuntime(string root)
        {
            foreach(var f in package.Files.Where(IsRuntimeFile))
            {
                string path=PayloadPath(root,f.Path);
                if(!File.Exists(path)||(Package.HashFile(path)!=f.Hash&&!(f.Path=="doorstop_config.ini"&&CompatibleDoorstop(path))))
                    throw new IOException("ملف مطلوب لتفعيل التعريب ناقص أو مختلف: "+f.Path);
            }
        }
        void ReviewToggle(string root,InstallReview result,bool version,bool stopped,bool loader)
        {
            try
            {
                var state=ReadToggle(root);bool ready=false,complete=true;
                try{CheckRuntime(root);ready=true;}catch(IOException){}
                try{CheckInstallCompleteIfPresent(root);}catch(IOException){complete=false;}
                if(state!=null)
                {
                    result.TranslationDisabled=state.Disabled;result.TogglePending=!state.Disabled;
                    result.CanInstall=false;result.CanRepair=false;
                    result.CanEnable=version&&stopped&&loader&&ready&&complete;
                    result.CanLaunch=stopped&&state.Disabled&&complete&&File.Exists(Within(root,"Hollow Knight Silksong.exe"));
                    result.Add("تفعيل التعريب",state.Disabled,state.Disabled?"معطل مؤقتًا. ستعمل اللعبة بلغتها الأصلية؛ يمكن إعادته دون تنزيل أو تثبيت جديد.":"تبديل غير مكتمل. اختر إعادة التفعيل لإنهاء الاستعادة.");
                    result.Summary=state.Disabled?"التعريب معطل مؤقتًا — مناسب للمقارنة والاختبار.":"تحتاج عملية التبديل إلى إكمال الاستعادة.";
                    if(!result.CanEnable)result.Add("إعادة التفعيل",false,"تتطلب إعادة التفعيل إغلاق اللعبة، وإصدارًا مدعومًا، وملفات تشغيل سليمة، وتثبيتًا مكتملًا.");
                }
                else
                {
                    string active=Within(root,PluginDll);
                    bool known=File.Exists(active)&&Package.HashFile(active)==PluginHash();
                    bool manualDisabled=File.Exists(Within(root,PluginDll+".disabled"));
                    result.CanDisable=stopped&&known&&!manualDisabled&&complete&&File.Exists(Within(root,"Hollow Knight Silksong.exe"));
                    result.CanLaunch=stopped&&version&&loader&&ready&&complete;
                    if(known)result.Add("تفعيل التعريب",true,"مفعّل. يمكنك تعطيله مؤقتًا دون حذف الترجمة أو تغيير التعديلات الأخرى.");
                }
            }
            catch(Exception e)
            {
                result.CanInstall=false;result.CanRepair=false;result.CanRemove=false;result.CanDisable=false;result.CanEnable=false;result.CanLaunch=false;
                result.Add("تفعيل التعريب",false,e.Message);result.Summary="راجع تعارض حالة التعريب قبل أي تغيير.";
            }
        }
        void CheckNoManualDisabled(string root)
        {if(File.Exists(Within(root,PluginDll+".disabled")))throw new IOException("توجد نسخة عطلت يدويًا. لا يمكن خلط طريقتي التعطيل.");}
        void CheckInstallCompleteIfPresent(string root)
        {
            if(File.Exists(StatePath(root))&&(string)LoadState(root).Root.Attribute("status")!="installed")
                throw new IOException("أكمل إزالة / استعادة العملية السابقة قبل تبديل اللغة.");
        }
        public string DisableTranslation(string folder,Action<string> progress)
        {
            // Disabling remains available after game updates; re-enabling requires a supported build.
            string root=Canonical(folder);CheckStopped();
            if(!File.Exists(Within(root,"Hollow Knight Silksong.exe")))throw new IOException("ملف تشغيل اللعبة غير موجود.");
            using(var handle=AcquireLock(root))
            {
                CheckStopped();CheckNoManualDisabled(root);CheckInstallCompleteIfPresent(root);var state=ReadToggle(root);
                if(state!=null&&state.Disabled)return "التعريب معطل مؤقتًا بالفعل.";
                string active=Within(root,PluginDll),parked=Within(root,ParkedDll);
                if(!File.Exists(active)||Package.HashFile(active)!=PluginHash())throw new IOException("نسخة التعريب غير معروفة أو تغيرت؛ لم تُعطّل.");
                if(state==null)
                {
                    Save(Within(root,ToggleJournal),new XDocument(new XElement("toggle",new XAttribute("format","1"),new XAttribute("root",root),new XAttribute("sha256",PluginHash()))));
                    if(AfterToggleStep!=null)AfterToggleStep(1);
                }
                CheckStopped();ReadToggle(root);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(parked));
                progress("تعطيل ملحق الترجمة مؤقتًا…");File.Move(active,parked);
                if(AfterToggleStep!=null)AfterToggleStep(2);
                if(!ReadToggle(root).Disabled)throw new IOException("تعذر التحقق من التعطيل.");Progress(1,1);
                return "عُطّل التعريب مؤقتًا. شغّل اللعبة لتجربة لغتها الأصلية، ثم أعد تفعيله من هنا بعد إغلاقها.";
            }
        }
        void RestoreToggleForRemoval(string root)
        {
            var state=ReadToggle(root);if(state==null)return;CheckStopped();
            if(state.Disabled)
            {
                string active=Within(root,PluginDll),parked=Within(root,ParkedDll);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(active));File.Move(parked,active);
                if(AfterToggleStep!=null)AfterToggleStep(2);
            }
            ReadToggle(root);File.Delete(Within(root,ToggleJournal));
        }
        public string EnableTranslation(string folder,Action<string> progress)
        {
            string root=Validate(folder);CheckStopped();
            using(var handle=AcquireLock(root))
            {
                CheckStopped();CheckNoManualDisabled(root);CheckInstallCompleteIfPresent(root);Inspect(root);CheckRuntime(root);
                if(ReadToggle(root)==null)return "التعريب مفعّل بالفعل.";
                progress("إعادة تفعيل التعريب…");RestoreToggleForRemoval(root);Progress(1,1);
                return "أُعيد تفعيل التعريب. شغّل اللعبة واختر English لعرض العربية.";
            }
        }
    }
}
