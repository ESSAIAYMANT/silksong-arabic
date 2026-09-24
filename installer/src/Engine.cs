using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;

namespace SilksongArabicSetup
{
    public sealed class CheckResult
    {
        public string Name, Detail;
        public bool Passed;
    }
    public sealed class InstallReview
    {
        public readonly List<CheckResult> Checks = new List<CheckResult>();
        public bool CanInstall, CanRepair, CanRemove, Healthy;
        public bool CanDisable, CanEnable, TranslationDisabled, TogglePending, CanLaunch;
        public int Added, Replaced, Kept, Missing;
        public long RequiredBytes;
        public string Summary;
        public void Add(string name, bool passed, string detail)
        { Checks.Add(new CheckResult { Name=name, Passed=passed, Detail=detail }); }
    }
    public sealed class PayloadFile
    {
        public string Path, Kind, Hash;
        public byte[] Data;
    }
    public sealed class Package
    {
        public string Id;
        public Dictionary<string,string> GameHashes = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        public List<PayloadFile> Files = new List<PayloadFile>();
        public static Package Load()
        {
            var p=new Package(); var asm=Assembly.GetExecutingAssembly();
            using(var stream=asm.GetManifestResourceStream("package.xml"))
            {
                var xml=ReadXml(stream);p.Id=(string)xml.Root.Attribute("id");
                foreach(var n in xml.Root.Element("game").Elements("file"))
                    p.GameHashes.Add((string)n.Attribute("path"),(string)n.Attribute("sha256"));
                using(var zip=new ZipArchive(asm.GetManifestResourceStream("payload.zip"),ZipArchiveMode.Read))
                foreach(var n in xml.Root.Element("payload").Elements("file"))
                {
                    var f=new PayloadFile{Path=(string)n.Attribute("path"),Hash=(string)n.Attribute("sha256"),Kind=(string)n.Attribute("kind")};
                    var entry=zip.GetEntry(f.Path);if(entry==null)throw new IOException("ملفات المثبت غير مكتملة.");
                    using(var input=entry.Open())using(var mem=new MemoryStream()){input.CopyTo(mem);f.Data=mem.ToArray();}
                    if(HashBytes(f.Data)!=f.Hash)throw new IOException("فشل التحقق من سلامة المثبت.");
                    p.Files.Add(f);
                }
            }
            return p;
        }
        public static XDocument ReadXml(Stream input)
        {
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4000000};
            using(var reader=XmlReader.Create(input,settings))return XDocument.Load(reader);
        }
        public static string HashBytes(byte[] data)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
        public static string HashFile(string file)
        {using(var sha=SHA256.Create())using(var input=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();}
    }

    public sealed partial class Engine
    {
        const string StateDir=".silksong-arabic-installer";
        const string PluginDir="BepInEx/plugins/SilksongArabic/";
        readonly Package package;
        readonly Func<bool> running;
        readonly Func<string,long> freeSpace;
        public Action<int> AfterWrite; // Used only by the separately compiled transaction tests.
        public Action<int,int> ProgressChanged;
        public Engine(Package p,Func<bool> isRunning,Func<string,long> availableSpace=null)
        {package=p;running=isRunning;freeSpace=availableSpace??(root=>new DriveInfo(System.IO.Path.GetPathRoot(root)).AvailableFreeSpace);}
        public static bool GameRunning()
        {return Process.GetProcessesByName("Hollow Knight Silksong").Any();}
        static string Canonical(string path)
        {
            var root=System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar);
            if(root.Length<4 || !Directory.Exists(root))throw new IOException("اختر مجلد اللعبة الصحيح.");
            RejectLinks(root);return root;
        }
        static void RejectLinks(string path)
        {
            string current=System.IO.Path.GetFullPath(path);
            while(!string.IsNullOrEmpty(current))
            {
                if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("المسار يحتوي على رابط أو مجلد معاد توجيهه؛ اختر مجلد اللعبة الفعلي: "+current);
                var parent=System.IO.Path.GetDirectoryName(current);if(parent==current)break;current=parent;
            }
        }
        public static string Within(string root,string relative)
        {
            if(string.IsNullOrWhiteSpace(relative)||relative.Contains(":")||relative.StartsWith("/")||relative.StartsWith("\\"))throw new IOException("مسار ملف غير صالح.");
            var parts=relative.Replace('\\','/').Split('/');
            if(parts.Any(s=>s==".."||s=="."||s.Length==0||s.EndsWith(".")||s.EndsWith(" ")))throw new IOException("مسار ملف غير آمن.");
            var full=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative.Replace('/',System.IO.Path.DirectorySeparatorChar)));
            if(!full.StartsWith(root.TrimEnd('\\','/')+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("المسار خارج مجلد اللعبة.");
            RejectLinks(full);return full;
        }
        string StatePath(string root){return Within(root,StateDir+"/state.xml");}
        void CheckStopped(){if(running())throw new IOException("احفظ تقدمك وأغلق اللعبة من قائمتها قبل تغيير حالة التعريب.");}
        public string Validate(string folder)
        {
            string root=Canonical(folder);
            foreach(var pair in package.GameHashes)
            {
                string file=Within(root,pair.Key);
                if(!File.Exists(file))throw new IOException("ملف اللعبة المطلوب غير موجود: "+pair.Key);
                if(Package.HashFile(file)!=pair.Value)throw new IOException("إصدار اللعبة غير مطابق. هذه الحزمة مخصصة للإصدار 1.0.30000 / Steam build 22479045. لم يتغير أي ملف.");
            }
            var steamapps=Directory.GetParent(root);
            if(steamapps!=null && steamapps.Parent!=null)
            {
                string manifest=System.IO.Path.Combine(steamapps.Parent.FullName,"appmanifest_1030300.acf");
                if(File.Exists(manifest))
                {
                    var m=Regex.Match(File.ReadAllText(manifest),"\"buildid\"\\s*\"([^\"]+)\"");
                    if(m.Success && m.Groups[1].Value!="22479045")throw new IOException("بناء Steam مختلف عن البناء المدعوم. لم يتغير أي ملف.");
                }
            }
            return root;
        }
        static bool CompatibleDoorstop(string path)
        {
            var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);string section="";
            foreach(var raw in File.ReadAllLines(path))
            {
                string line=raw.Trim();if(line.StartsWith("#")||line.StartsWith(";"))continue;
                if(line.StartsWith("[")&&line.EndsWith("]")){section=line.Substring(1,line.Length-2);continue;}
                int at=line.IndexOf('=');if(at>0)values[section+"/"+line.Substring(0,at).Trim()]=line.Substring(at+1).Trim();
            }
            string enabled,target;
            return values.TryGetValue("General/enabled",out enabled)&&enabled.Equals("true",StringComparison.OrdinalIgnoreCase)
                &&values.TryGetValue("General/target_assembly",out target)&&target.Replace('\\','/').Equals("BepInEx/core/BepInEx.Preloader.dll",StringComparison.OrdinalIgnoreCase);
        }
        public string Inspect(string folder)
        {
            string root=Validate(folder);CheckStopped();
            ReadToggle(root);
            foreach(var f in package.Files)
            {
                string path=Within(root,f.Path);
                if(Directory.Exists(path))throw new IOException("مجلد يتعارض مع ملف مطلوب: "+f.Path);
                if(f.Kind=="loader" && File.Exists(path) && Package.HashFile(path)!=f.Hash)
                {
                    if(f.Path=="doorstop_config.ini" && CompatibleDoorstop(path))continue;
                    throw new IOException("يوجد محمّل تعديلات مختلف. لن يستبدله المثبت: "+f.Path);
                }
            }
            if(File.Exists(Within(root,PluginDir+"SilksongArabic.dll.disabled")))throw new IOException("توجد نسخة تعريب معطلة يدويًا. أزلها أو استعدها بالطريقة التي ثُبّتت بها أولًا.");
            return "الإصدار متوافق. ملفات الحفظ خارج نطاق المثبت.";
        }
        void Progress(int current,int total)
        {if(ProgressChanged!=null)ProgressChanged(current,total);}
        long RequiredSpace(string root)
        {
            long bytes=8*1024*1024;
            foreach(var f in package.Files)
            {
                bytes+=2L*f.Data.Length;
                string file=PayloadPath(root,f.Path);if(File.Exists(file))bytes+=new FileInfo(file).Length;
            }
            return bytes;
        }
        void CheckSpace(string root)
        {
            if(freeSpace(root)<RequiredSpace(root))throw new IOException("المساحة الحرة لا تكفي للتثبيت والنسخ الاحتياطي. حرر مساحة ثم أعد الفحص.");
        }
        // Read-only review: no directory creation, journal mutation, or permission changes.
        public InstallReview Review(string folder)
        {
            var result=new InstallReview();string root;
            try{root=Canonical(folder);result.Add("مجلد اللعبة",true,"المجلد موجود ومساره صالح.");}
            catch(Exception e){result.Add("مجلد اللعبة",false,e.Message);result.Summary="اختر المجلد الذي يحتوي ملف تشغيل اللعبة.";return result;}
            bool version=false,stopped=false,loader=false,space=false;
            try{Validate(root);version=true;result.Add("إصدار اللعبة",true,"1.0.30000 • Steam 22479045 — بصمات الملفات مطابقة.");}
            catch(Exception e){result.Add("إصدار اللعبة",false,e.Message);}
            try{CheckStopped();stopped=true;result.Add("حالة اللعبة",true,"اللعبة مغلقة.");}
            catch(Exception e){result.Add("حالة اللعبة",false,e.Message);}
            if(version&&stopped)
            {
                try{Inspect(root);loader=true;result.Add("توافق ملفات التعريب",true,"لا يوجد تعارض مع المحمّل أو المسارات المطلوبة.");}
                catch(Exception e){result.Add("توافق ملفات التعريب",false,e.Message);}
            }
            try{result.RequiredBytes=RequiredSpace(root);CheckSpace(root);space=true;result.Add("المساحة الحرة",true,"المساحة كافية؛ المطلوب نحو "+Math.Ceiling(result.RequiredBytes/1048576.0)+" ميغابايت، مع النسخ الاحتياطي.");}
            catch(Exception e){result.Add("المساحة الحرة",false,e.Message);}
            try
            {
                foreach(var f in package.Files)
                {
                    string file=PayloadPath(root,f.Path);
                    if(!File.Exists(file)){result.Added++;continue;}
                    if(Package.HashFile(file)==f.Hash||(f.Path=="doorstop_config.ini"&&CompatibleDoorstop(file)))result.Kept++;
                    else result.Replaced++;
                }
                if(!File.Exists(StatePath(root)))
                {
                    result.CanInstall=version&&stopped&&loader&&space;
                    result.Add("حالة التثبيت",true,"تثبيت جديد: "+result.Added+" ملفًا للإضافة، "+result.Replaced+" للاستبدال مع نسخة احتياطية، "+result.Kept+" للإبقاء.");
                    result.Summary=result.CanInstall?"جاهز للتثبيت. ستُحفظ نسخة من كل ملف يُستبدل.":"عالج البنود التي تحتاج انتباهًا ثم أعد الفحص.";
                }
                else
                {
                    var journal=LoadState(root);bool removable=true;
                    try{ValidateRestore(root,journal);}
                    catch(Exception e){removable=false;result.Add("إمكان الاستعادة",false,e.Message);}
                    result.CanRemove=stopped&&removable;
                    string status=(string)journal.Root.Attribute("status");
                    if(status!="installed")
                    {
                        result.Add("حالة التثبيت",false,"عملية سابقة غير مكتملة. استخدم «إزالة / استعادة» أولًا.");
                        result.Summary="توجد عملية تحتاج الاستعادة قبل تثبيت جديد.";
                    }
                    else if((string)journal.Root.Attribute("package")!=package.Id)
                    {
                        result.Add("حالة التثبيت",false,"حزمة سابقة مختلفة. أزلها بهذا المثبت إن كان ذلك متاحًا ثم ثبّت الحزمة الحالية.");
                        result.Summary="يوجد تثبيت سابق مختلف.";
                    }
                    else
                    {
                        var missing=RepairPlan(root,journal);result.Missing=missing.Count;
                        result.Healthy=missing.Count==0;
                        result.CanRepair=missing.Count>0&&version&&stopped&&loader&&space&&removable;
                        result.Add("سلامة التثبيت",missing.Count==0,missing.Count==0?"التعريب مثبت، وجميع الملفات مطابقة.":"هناك "+missing.Count+" ملفًا ناقصًا يمكن إصلاحه دون استبدال ملفات معدلة.");
                        result.Summary=missing.Count==0?"التعريب مثبت وسليم.":"يمكن استعادة الملفات الناقصة بزر الإصلاح.";
                    }
                }
            }
            catch(Exception e){result.Add("حالة التثبيت",false,e.Message);result.Summary="تعذر اعتماد حالة التثبيت. راجع التفاصيل أدناه.";}
            if(result.Healthy&&result.Checks.Any(c=>!c.Passed))result.Summary="ملفات التعريب مطابقة، لكن هناك بنودًا تحتاج انتباهًا. راجع نتائج الفحص.";
            ReviewToggle(root,result,version,stopped,loader);
            return result;
        }
        List<PayloadFile> RepairPlan(string root,XDocument journal)
        {
            var missing=new List<PayloadFile>();
            foreach(var f in package.Files)
            {
                string file=PayloadPath(root,f.Path);
                if(Directory.Exists(file))throw new IOException("مجلد يتعارض مع ملف مطلوب: "+f.Path);
                if(File.Exists(file))
                {
                    if(Package.HashFile(file)!=f.Hash&&!(f.Path=="doorstop_config.ini"&&CompatibleDoorstop(file)))
                        throw new IOException("ملف معدل: "+f.Path+". لن يستبدله الإصلاح تلقائيًا؛ احتفظ به خارج هذا المسار أولًا.");
                    continue;
                }
                var entry=journal.Root.Elements("file").FirstOrDefault(n=>string.Equals((string)n.Attribute("path"),f.Path,StringComparison.OrdinalIgnoreCase));
                if(entry==null||(string)entry.Attribute("installed")!=f.Hash)
                    throw new IOException("ملف سابق ناقص لا يملكه هذا التثبيت: "+f.Path+". استعد التثبيت السابق أولًا.");
                missing.Add(f);
            }
            return missing;
        }
        public string Repair(string folder,Action<string> progress)
        {
            string root=Validate(folder);CheckStopped();EnsureActive(root);CheckSpace(root);
            using(var handle=AcquireLock(root))
            {
                EnsureActive(root);
                var journal=LoadState(root);
                if((string)journal.Root.Attribute("status")!="installed"||(string)journal.Root.Attribute("package")!=package.Id)
                    throw new IOException("الإصلاح يتطلب تثبيتًا مكتملًا من الحزمة نفسها؛ استخدم الاستعادة للعملية غير المكتملة.");
                ValidateRestore(root,journal);var missing=RepairPlan(root,journal);var written=new List<PayloadFile>();
                Progress(0,missing.Count);
                try
                {
                    foreach(var f in missing)
                    {
                        CheckStopped();string file=Within(root,f.Path);
                        if(File.Exists(file)||Directory.Exists(file))throw new IOException("تغير ملف أثناء الإصلاح: "+f.Path);
                        progress("إصلاح: "+f.Path);AtomicWrite(file,f.Data);written.Add(f);
                        if(Package.HashFile(file)!=f.Hash)throw new IOException("فشل التحقق من الملف بعد الإصلاح: "+f.Path);
                        if(AfterWrite!=null)AfterWrite(written.Count);Progress(written.Count,missing.Count);
                    }
                    return missing.Count==0?"التعريب سليم؛ لا توجد ملفات ناقصة.":"اكتمل إصلاح الملفات الناقصة. بقيت النسخ الاحتياطية الأصلية محفوظة.";
                }
                catch(Exception error)
                {
                    try
                    {
                        CheckStopped();
                        foreach(var f in written.AsEnumerable().Reverse())
                        {
                            string file=Within(root,f.Path);
                            if(File.Exists(file))
                            {if(Package.HashFile(file)!=f.Hash)throw new IOException("تغير ملف أثناء تراجع الإصلاح: "+f.Path);File.Delete(file);}
                        }
                    }
                    catch(Exception rollback){throw new IOException("توقف الإصلاح. سجل التثبيت والنسخ الاحتياطية محفوظان. "+error.Message+" | "+rollback.Message);}
                    throw new IOException("توقف الإصلاح وأُعيدت حالته السابقة: "+error.Message,error);
                }
            }
        }
        static void AtomicWrite(string path,byte[] data)
        {
            RejectLinks(path);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string tmp=path+".arabic-"+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var output=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {output.Write(data,0,data.Length);output.Flush(true);}
                if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
            }
            finally {if(File.Exists(tmp))File.Delete(tmp);}
        }
        static void Save(string path,XDocument doc){AtomicWrite(path,Encoding.UTF8.GetBytes(doc.ToString()));}
        XDocument LoadState(string root)
        {
            string path=StatePath(root);if(!File.Exists(path))throw new IOException("لا يوجد تثبيت مسجل بهذا المثبت في هذا المجلد.");
            XDocument doc;using(var stream=File.OpenRead(path))doc=Package.ReadXml(stream);
            var top=doc.Root;if(top==null||top.Name!="install"||(string)top.Attribute("format")!="1")throw new IOException("سجل التثبيت غير صالح.");
            if(!string.Equals((string)top.Attribute("root"),root,StringComparison.OrdinalIgnoreCase))throw new IOException("سجل التثبيت يخص مجلدًا مختلفًا.");
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var n in top.Elements("file"))
            {
                string relative=(string)n.Attribute("path"),hash=(string)n.Attribute("installed"),original=(string)n.Attribute("original"),kind=(string)n.Attribute("kind");
                var payload=package.Files.FirstOrDefault(f=>f.Path.Equals(relative,StringComparison.OrdinalIgnoreCase));
                if(payload==null||kind!=payload.Kind||!seen.Add(relative)||!Regex.IsMatch(hash??"","^[a-f0-9]{64}$")||!Regex.IsMatch(original??"","^([a-f0-9]{64})?$"))throw new IOException("سجل التثبيت يحتوي ملفًا غير معتمد.");
                Within(root,relative);
                string backup=(string)n.Attribute("backup");
                if(!Regex.IsMatch(backup??"","^backup-[a-f0-9]{32}/[0-9]+\\.bak$"))throw new IOException("مسار النسخة الاحتياطية غير صالح.");
                Within(root,StateDir+"/"+backup);
            }
            return doc;
        }
        public string Install(string folder,Action<string> progress)
        {
            string root=Validate(folder);CheckStopped();EnsureActive(root);CheckSpace(root);
            using(var handle=AcquireLock(root))return InstallCore(root,progress);
        }
        static FileStream AcquireLock(string root)
        {
            string path=Within(root,StateDir+"/operation.lock");Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException){throw new IOException("هناك عملية تثبيت أو إزالة أخرى، أو تعذر فتح سجل العملية.");}
        }
        string InstallCore(string folder,Action<string> progress)
        {
            string root=Validate(folder);CheckStopped();EnsureActive(root);string state=StatePath(root);
            if(File.Exists(state))
            {
                var previous=LoadState(root);string status=(string)previous.Root.Attribute("status");
                if(status=="installed" && (string)previous.Root.Attribute("package")==package.Id)
                {
                    foreach(var f in package.Files)
                    {
                        string target=Within(root,f.Path);
                        if(!File.Exists(target)||(Package.HashFile(target)!=f.Hash && !(f.Path=="doorstop_config.ini"&&CompatibleDoorstop(target))))throw new IOException("تغيرت ملفات تثبيت سابق. استخدم الإزالة أولًا؛ لن تُستبدل التغييرات تلقائيًا.");
                    }
                    return "التعريب مثبت بالفعل وسليم.";
                }
                throw new IOException("يوجد تثبيت سابق أو عملية غير مكتملة. استخدم «إزالة / استعادة» أولًا.");
            }
            Inspect(root);
            string backupFolder="backup-"+Guid.NewGuid().ToString("N");
            var journal=new XDocument(new XElement("install",new XAttribute("format","1"),new XAttribute("root",root),new XAttribute("package",package.Id),new XAttribute("status","installing")));
            var planned=new List<PayloadFile>();int index=0;
            foreach(var f in package.Files)
            {
                string target=Within(root,f.Path);string original=File.Exists(target)?Package.HashFile(target):"";
                if(original==f.Hash || (f.Kind=="loader"&&f.Path=="doorstop_config.ini"&&File.Exists(target)&&CompatibleDoorstop(target)))continue;
                string backup=backupFolder+"/"+(index++)+".bak";
                if(original!="")
                {
                    string fullBackup=Within(root,StateDir+"/"+backup);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullBackup));
                    File.Copy(target,fullBackup,false);
                    if(Package.HashFile(fullBackup)!=original)throw new IOException("فشل التحقق من النسخة الاحتياطية.");
                }
                journal.Root.Add(new XElement("file",new XAttribute("path",f.Path),new XAttribute("kind",f.Kind),new XAttribute("installed",f.Hash),new XAttribute("original",original),new XAttribute("backup",backup)));
                planned.Add(f);
            }
            Save(state,journal);
            try
            {
                index=0;Progress(0,planned.Count);
                foreach(var f in planned)
                {
                    CheckStopped();progress("تثبيت: "+f.Path);string target=Within(root,f.Path);
                    var entry=journal.Root.Elements("file").First(n=>(string)n.Attribute("path")==f.Path);
                    string current=File.Exists(target)?Package.HashFile(target):"";
                    if(current!=(string)entry.Attribute("original"))throw new IOException("تغير ملف أثناء التثبيت: "+f.Path);
                    AtomicWrite(target,f.Data);
                    if(Package.HashFile(target)!=f.Hash)throw new IOException("فشل التحقق بعد النسخ: "+f.Path);
                    index++;if(AfterWrite!=null)AfterWrite(index);Progress(index,planned.Count);
                }
                journal.Root.SetAttributeValue("status","installed");Save(state,journal);
                return "اكتمل التثبيت. شغّل اللعبة واختر English لعرض التعريب.";
            }
            catch(Exception error)
            {
                try{Restore(root,progress,false);}
                catch(Exception recovery){throw new IOException("توقف التثبيت؛ النسخ الاحتياطية محفوظة. استخدم الإزالة / الاستعادة بعد معالجة السبب. "+error.Message+" | "+recovery.Message);}
                throw new IOException("توقف التثبيت وأُعيدت الملفات السابقة: "+error.Message,error);
            }
        }
        bool OtherMods(string root)
        {
            foreach(string relative in new[]{"BepInEx/plugins","BepInEx/patchers"})
            {
                string dir=Within(root,relative);if(!Directory.Exists(dir))continue;
                var queue=new Queue<string>();queue.Enqueue(dir);
                while(queue.Count>0)
                {
                    string current=queue.Dequeue();RejectLinks(current);
                    foreach(var file in Directory.GetFiles(current,"*.dll"))
                    {
                        RejectLinks(file);
                        if(!file.StartsWith(Within(root,PluginDir.TrimEnd('/'))+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return true;
                    }
                    foreach(var sub in Directory.GetDirectories(current))queue.Enqueue(sub);
                }
            }
            return false;
        }
        public string Uninstall(string folder,Action<string> progress)
        {string root=Canonical(folder);CheckStopped();using(var handle=AcquireLock(root))return Restore(root,progress,true);}
        void ValidateRestore(string root,XDocument journal)
        {
            ReadToggle(root);
            string status=(string)journal.Root.Attribute("status");
            if(status!="installed"&&status!="installing"&&status!="removing")throw new IOException("حالة سجل التثبيت غير صالحة.");
            foreach(var n in journal.Root.Elements("file"))
            {
                string rel=(string)n.Attribute("path");string target=PayloadPath(root,rel);
                if(Directory.Exists(target))throw new IOException("مجلد يتعارض مع ملف مطلوب: "+rel);
                string current=File.Exists(target)?Package.HashFile(target):"";
                string installed=(string)n.Attribute("installed"),original=(string)n.Attribute("original");
                if(current!=""&&current!=installed&&current!=original)throw new IOException("لن نحذف ملفًا تغير بعد التثبيت: "+rel+". احتفظ به خارج هذا المسار قبل إعادة المحاولة.");
                if(original!="")
                {
                    string backup=Within(root,StateDir+"/"+(string)n.Attribute("backup"));
                    if(!File.Exists(backup)||Package.HashFile(backup)!=original)throw new IOException("نسخة احتياطية مفقودة أو متغيرة: "+rel);
                }
            }
        }
        string Restore(string root,Action<string> progress,bool normalUninstall)
        {
            CheckStopped();var journal=LoadState(root);ValidateRestore(root,journal);
            bool keepLoader=normalUninstall && OtherMods(root);var entries=journal.Root.Elements("file").ToList();
            // Normalize the parked DLL after full preflight so the original ownership journal remains authoritative.
            RestoreToggleForRemoval(root);
            journal.Root.SetAttributeValue("status","removing");Save(StatePath(root),journal);
            int done=0;Progress(0,entries.Count);
            foreach(var n in entries.AsEnumerable().Reverse())
            {
                string rel=(string)n.Attribute("path"),original=(string)n.Attribute("original");
                if(keepLoader&&(string)n.Attribute("kind")=="loader"&&original==""){Progress(++done,entries.Count);continue;}
                CheckStopped();string target=Within(root,rel);progress("استعادة: "+rel);
                string current=File.Exists(target)?Package.HashFile(target):"";
                if(current!=""&&current!=(string)n.Attribute("installed")&&current!=original)throw new IOException("تغير ملف أثناء الاستعادة: "+rel);
                if(original!="")
                {
                    AtomicWrite(target,File.ReadAllBytes(Within(root,StateDir+"/"+(string)n.Attribute("backup"))));
                    if(Package.HashFile(target)!=original)throw new IOException("تعذر التحقق من الملف المستعاد.");
                }
                else if(File.Exists(target))File.Delete(target);
                Progress(++done,entries.Count);
            }
            string archive=Within(root,StateDir+"/completed-"+Guid.NewGuid().ToString("N")+".xml");
            File.Move(StatePath(root),archive); // Backups and completion journal remain available.
            return "أزيلت ملفات هذا التثبيت واستعيدت الملفات السابقة. بقيت النسخ الاحتياطية وملفات الحفظ محفوظة."+(keepLoader?" احتُفظ بالمحمّل لاستخدامه مع تعديلات أخرى.":"");
        }
        public static List<string> Discover()
        {
            var steamRoots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in new[]{new[]{"HKCU","Software\\Valve\\Steam","SteamPath"},new[]{"HKLM","SOFTWARE\\WOW6432Node\\Valve\\Steam","InstallPath"}})
            {
                try{using(var key=(item[0]=="HKCU"?Registry.CurrentUser:Registry.LocalMachine).OpenSubKey(item[1])){var value=key==null?null:key.GetValue(item[2]) as string;if(!string.IsNullOrEmpty(value))steamRoots.Add(value);}}catch{}
            }
            steamRoots.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
            var libraries=new HashSet<string>(steamRoots,StringComparer.OrdinalIgnoreCase);
            foreach(var root in steamRoots)
            {
                string file=System.IO.Path.Combine(root,"steamapps","libraryfolders.vdf");
                try{if(File.Exists(file))foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"path\"\\s*\"([^\"]+)\""))libraries.Add(m.Groups[1].Value.Replace("\\\\","\\"));}catch{}
            }
            var result=new List<string>();
            foreach(var root in libraries)
            {
                try
                {
                    string manifest=System.IO.Path.Combine(root,"steamapps","appmanifest_1030300.acf");if(!File.Exists(manifest))continue;
                    var m=Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s*\"([^\"]+)\"");if(!m.Success)continue;
                    string common=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,"steamapps","common"));
                    string game=Within(common,m.Groups[1].Value);
                    if(File.Exists(System.IO.Path.Combine(game,"Hollow Knight Silksong.exe")))result.Add(game);
                }catch{}
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
