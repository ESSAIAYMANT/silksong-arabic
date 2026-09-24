using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using SilksongArabicSetup;

static class Tests
{
    const string plugin="BepInEx/plugins/SilksongArabic/SilksongArabic.dll";
    const string parked=".silksong-arabic-installer/disabled/SilksongArabic.dll";
    const string marker=".silksong-arabic-installer/toggle.xml";
    static int passed;static string root;
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void ExpectFailure(Action a,string text)
    {try{a();}catch(IOException){return;}throw new Exception("Expected failure: "+text);}
    static void Test(string name,Action a){a();passed++;Console.WriteLine("PASS "+name);}
    static string Fresh(string name)
    {var path=Path.Combine(root,name);Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"Hollow Knight Silksong.exe"),"fixture-game");return path;}
    static Package Fake()
    {
        var p=new Package{Id="test-1"};p.GameHashes.Add("Hollow Knight Silksong.exe",Package.HashBytes(Encoding.UTF8.GetBytes("fixture-game")));
        foreach(var item in new[]{new[]{"winhttp.dll","loader","loader-data"},new[]{"BepInEx/core/BepInEx.dll","loader","core-data"},new[]{"BepInEx/plugins/SilksongArabic/SilksongArabic.dll","plugin","plugin-data"},new[]{"BepInEx/plugins/SilksongArabic/arabic.xml","plugin","arabic-data"}})
        {var bytes=Encoding.UTF8.GetBytes(item[2]);p.Files.Add(new PayloadFile{Path=item[0],Kind=item[1],Data=bytes,Hash=Package.HashBytes(bytes)});}return p;
    }
    static Engine Create(Package p){return new Engine(p,()=>false);}
    static string At(string dir,string rel){return Engine.Within(dir,rel);}
    static void Put(string dir,string rel,string value){string path=At(dir,rel);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,value);}
    static void Nothing(string s){}
    public static int Main(string[] args)
    {
        root=Path.GetFullPath(args.Length>0?args[0]:"installer-tests");Directory.CreateDirectory(root);
        try
        {
            Test("clean install, idempotence, remove, keep saves",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("clean");Put(d,"user1.dat","save-marker");
                e.Install(d,Nothing);foreach(var f in p.Files)Require(Package.HashFile(At(d,f.Path))==f.Hash,"payload mismatch");
                Require(e.Install(d,Nothing).Contains("بالفعل"),"reinstall not idempotent");e.Uninstall(d,Nothing);
                foreach(var f in p.Files)Require(!File.Exists(At(d,f.Path)),"new payload survived uninstall");Require(File.ReadAllText(At(d,"user1.dat"))=="save-marker","save changed");
                e.Install(d,Nothing);e.Uninstall(d,Nothing);
            });
            Test("restore earlier manual Arabic translation",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("prior-plugin");string rel=p.Files.Last().Path;Put(d,rel,"old-user-translation");e.Install(d,Nothing);e.Uninstall(d,Nothing);Require(File.ReadAllText(At(d,rel))=="old-user-translation","backup not restored");
            });
            Test("retain existing matching loader",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("prior-loader");foreach(var f in p.Files.Where(f=>f.Kind=="loader")){Directory.CreateDirectory(Path.GetDirectoryName(At(d,f.Path)));File.WriteAllBytes(At(d,f.Path),f.Data);}
                e.Install(d,Nothing);e.Uninstall(d,Nothing);Require(File.Exists(At(d,"winhttp.dll")),"preexisting loader removed");
            });
            Test("different loader rejected without changing it",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("conflicting-loader");Put(d,"winhttp.dll","other-mod-loader");ExpectFailure(()=>e.Install(d,Nothing),"loader conflict");Require(File.ReadAllText(At(d,"winhttp.dll"))=="other-mod-loader","loader overwritten");Require(!File.Exists(At(d,p.Files.Last().Path)),"partial install after conflict");
            });
            Test("wrong game binary rejected",()=>{
                var e=Create(Fake());string d=Fresh("wrong-version");Put(d,"Hollow Knight Silksong.exe","wrong-version");ExpectFailure(()=>e.Install(d,Nothing),"wrong game");Require(!File.Exists(At(d,"winhttp.dll")),"writes before validation");
            });
            Test("running game rejected",()=>{
                var e=new Engine(Fake(),()=>true);string d=Fresh("running");ExpectFailure(()=>e.Install(d,Nothing),"running game");Require(!File.Exists(At(d,"winhttp.dll")),"running game modified");
            });
            Test("injected mid-install failure rolls back exact originals",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("failure-rollback");Put(d,p.Files.Last().Path,"old-translation");e.AfterWrite=i=>{if(i==3)throw new IOException("injected write failure");};ExpectFailure(()=>e.Install(d,Nothing),"injected failure");Require(!File.Exists(At(d,"winhttp.dll")),"loader not rolled back");Require(File.ReadAllText(At(d,p.Files.Last().Path))=="old-translation","original not restored");Require(!File.Exists(At(d,".silksong-arabic-installer/state.xml")),"journal still active");
            });
            Test("modified installed file prevents destructive uninstall",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("modified-file");e.Install(d,Nothing);Put(d,p.Files.Last().Path,"friend-changes");ExpectFailure(()=>e.Uninstall(d,Nothing),"changed file");Require(File.ReadAllText(At(d,p.Files.Last().Path))=="friend-changes","changed translation erased");Require(File.Exists(At(d,"winhttp.dll")),"partial removal despite conflict");
            });
            Test("other mods retain shared loader",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("other-mod");e.Install(d,Nothing);Put(d,"BepInEx/plugins/FriendMod.dll","friendmod");e.Uninstall(d,Nothing);Require(File.Exists(At(d,"winhttp.dll")),"shared loader removed");Require(File.Exists(At(d,"BepInEx/plugins/FriendMod.dll")),"friendmod removed");Require(!File.Exists(At(d,p.Files.Last().Path)),"Arabic remained");
            });
            Test("tampered journal traversal rejected",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("traversal");e.Install(d,Nothing);string j=At(d,".silksong-arabic-installer/state.xml");var x=XDocument.Load(j);x.Root.Elements("file").First().SetAttributeValue("path","../outside.txt");x.Save(j);ExpectFailure(()=>e.Uninstall(d,Nothing),"traversal journal");Require(File.Exists(At(d,"winhttp.dll")),"partial mutation");
            });
            Test("missing backup blocks uninstall before any mutation",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("missing-backup");Put(d,p.Files.Last().Path,"original");e.Install(d,Nothing);var x=XDocument.Load(At(d,".silksong-arabic-installer/state.xml"));var entry=x.Root.Elements("file").First(n=>(string)n.Attribute("original")!="");File.Delete(At(d,".silksong-arabic-installer/"+(string)entry.Attribute("backup")));ExpectFailure(()=>e.Uninstall(d,Nothing),"missing backup");Require(File.Exists(At(d,"winhttp.dll")),"partially uninstalled");
            });
            Test("game update still permits uninstall",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("update");e.Install(d,Nothing);Put(d,"Hollow Knight Silksong.exe","updated-game");e.Uninstall(d,Nothing);Require(File.ReadAllText(At(d,"Hollow Knight Silksong.exe"))=="updated-game","game update overwritten");
            });
            Test("unfinished installation journal can be recovered",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("recover");e.Install(d,Nothing);string j=At(d,".silksong-arabic-installer/state.xml");var x=XDocument.Load(j);x.Root.SetAttributeValue("status","installing");x.Save(j);e.Uninstall(d,Nothing);Require(!File.Exists(At(d,"winhttp.dll")),"recovery failed");
            });
            Test("absolute, ADS and dot path rejection",()=>{
                string d=Fresh("path-guards");foreach(var path in new[]{"../bad","C:/bad","/bad","x:stream","x/../bad","x./bad"})ExpectFailure(()=>Engine.Within(d,path),"unsafe path");
            });
            Test("Windows case-insensitive game path restores correctly",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("case-path");e.Install(d,Nothing);e.Uninstall(d.ToUpperInvariant(),Nothing);Require(!File.Exists(At(d,"winhttp.dll")),"case variant rejected");
            });
            Test("compatible custom Doorstop config preserved",()=>{
                var p=Fake();byte[] data=Encoding.UTF8.GetBytes("[General]\nenabled = true\ntarget_assembly = BepInEx/core/BepInEx.Preloader.dll\n");
                p.Files.Add(new PayloadFile{Path="doorstop_config.ini",Kind="loader",Data=data,Hash=Package.HashBytes(data)});
                var e=Create(p);string d=Fresh("custom-config");string custom="[General]\n# keep this comment\nenabled=true\ntarget_assembly=BepInEx\\core\\BepInEx.Preloader.dll\n";Put(d,"doorstop_config.ini",custom);e.Install(d,Nothing);e.Uninstall(d,Nothing);Require(File.ReadAllText(At(d,"doorstop_config.ini"))==custom,"custom configuration lost");
            });
            Test("tampered backup hash rejects whole removal",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("corrupt-backup");Put(d,p.Files.Last().Path,"original");e.Install(d,Nothing);var x=XDocument.Load(At(d,".silksong-arabic-installer/state.xml"));var n=x.Root.Elements("file").First(f=>(string)f.Attribute("original")!="");Put(d,".silksong-arabic-installer/"+(string)n.Attribute("backup"),"tampered");ExpectFailure(()=>e.Uninstall(d,Nothing),"corrupted backup");Require(File.Exists(At(d,"winhttp.dll")),"mutation despite corrupt backup");
            });
            Test("concurrent installer lock rejects mutation",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("locked");string path=At(d,".silksong-arabic-installer/operation.lock");Directory.CreateDirectory(Path.GetDirectoryName(path));using(var handle=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None))ExpectFailure(()=>e.Install(d,Nothing),"operation lock");Require(!File.Exists(At(d,"winhttp.dll")),"lock ignored");
            });
            Test("review previews changes without writing state",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("review-clean");Put(d,p.Files.Last().Path,"earlier-text");
                var before=Directory.GetFiles(d,"*",SearchOption.AllDirectories).OrderBy(s=>s).ToArray();var r=e.Review(d);
                Require(r.CanInstall&&!r.CanRemove&&r.Replaced==1&&r.Added==3,"incorrect plan");
                Require(before.SequenceEqual(Directory.GetFiles(d,"*",SearchOption.AllDirectories).OrderBy(s=>s)),"review wrote files");
                Require(!Directory.Exists(At(d,".silksong-arabic-installer")),"review created state folder");
            });
            Test("missing owned files repaired while original backup survives",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("repair-missing");string rel=p.Files.Last().Path;Put(d,rel,"before-install");e.Install(d,Nothing);
                string journal=File.ReadAllText(At(d,".silksong-arabic-installer/state.xml"));File.Delete(At(d,rel));
                var r=e.Review(d);Require(r.CanRepair&&r.CanRemove&&!r.Healthy&&r.Missing==1,"repair not offered");
                e.Repair(d,Nothing);Require(Package.HashFile(At(d,rel))==p.Files.Last().Hash,"repair did not restore payload");
                Require(File.ReadAllText(At(d,".silksong-arabic-installer/state.xml"))==journal,"repair altered ownership");
                Require(e.Review(d).Healthy,"repair not healthy");e.Uninstall(d,Nothing);Require(File.ReadAllText(At(d,rel))=="before-install","repair lost original");
            });
            Test("repair refuses changed files before writing missing ones",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("repair-changed");e.Install(d,Nothing);File.Delete(At(d,p.Files[2].Path));Put(d,p.Files.Last().Path,"personal-edit");
                Require(!e.Review(d).CanRepair,"unsafe repair offered");ExpectFailure(()=>e.Repair(d,Nothing),"changed translation");
                Require(!File.Exists(At(d,p.Files[2].Path)),"partial repair");Require(File.ReadAllText(At(d,p.Files.Last().Path))=="personal-edit","edit overwritten");
            });
            Test("repair failure rolls back only newly restored files",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("repair-failure");e.Install(d,Nothing);File.Delete(At(d,p.Files[2].Path));File.Delete(At(d,p.Files[3].Path));
                e.AfterWrite=i=>{if(i==1)throw new IOException("repair fault");};ExpectFailure(()=>e.Repair(d,Nothing),"repair fault");
                Require(!File.Exists(At(d,p.Files[2].Path))&&!File.Exists(At(d,p.Files[3].Path)),"repair not rolled back");Require(File.Exists(At(d,"winhttp.dll")),"existing loader changed");
            });
            Test("repair refuses missing files owned by earlier manual install",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("repair-unowned");Put(d,p.Files.Last().Path,"arabic-data");e.Install(d,Nothing);File.Delete(At(d,p.Files.Last().Path));
                Require(!e.Review(d).CanRepair,"unowned repair offered");ExpectFailure(()=>e.Repair(d,Nothing),"unowned file");Require(!File.Exists(At(d,p.Files.Last().Path)),"unowned file recreated");
            });
            Test("review and repair honor running game guard",()=>{
                var p=Fake();string d=Fresh("repair-running");Create(p).Install(d,Nothing);File.Delete(At(d,p.Files.Last().Path));var e=new Engine(p,()=>true);
                var r=e.Review(d);Require(!r.CanInstall&&!r.CanRepair&&!r.CanRemove,"action enabled while running");ExpectFailure(()=>e.Repair(d,Nothing),"running repair");
            });
            Test("low disk space rejected before state creation",()=>{
                var p=Fake();string d=Fresh("low-space");var e=new Engine(p,()=>false,r=>0L);
                Require(!e.Review(d).CanInstall,"low disk install offered");ExpectFailure(()=>e.Install(d,Nothing),"low space");Require(!Directory.Exists(At(d,".silksong-arabic-installer")),"low disk wrote state");
            });
            Test("updated game review permits removal but not repair",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("review-updated");e.Install(d,Nothing);Put(d,"Hollow Knight Silksong.exe","new-version");File.Delete(At(d,p.Files.Last().Path));
                var r=e.Review(d);Require(r.CanRemove&&!r.CanInstall&&!r.CanRepair,"updated game actions wrong");
            });
            Test("directory collision blocks removal before any file changes",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("remove-directory");e.Install(d,Nothing);File.Delete(At(d,p.Files.Last().Path));Directory.CreateDirectory(At(d,p.Files.Last().Path));
                Require(!e.Review(d).CanRemove,"directory collision removal offered");ExpectFailure(()=>e.Uninstall(d,Nothing),"directory collision");Require(File.Exists(At(d,"winhttp.dll")),"partial removal");
            });
            Test("progress reports completed files and final total",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("progress");int last=-1,total=-1;e.ProgressChanged=(a,b)=>{Require(a>=0&&a<=b,"bad progress");last=a;total=b;};
                e.Install(d,Nothing);Require(last==p.Files.Count&&total==p.Files.Count,"install progress incomplete");e.Uninstall(d,Nothing);Require(last==total&&total==p.Files.Count,"remove progress incomplete");
            });
            Test("support report redacts game and profile across case and separators",()=>{
                string value=SupportReport.Redact("C:\\Users\\Tester\\Games\\Silksong\\file.xml c:/users/tester/log.txt",@"c:\users\tester\Games\Silksong",@"C:\Users\Tester");
                Require(value=="[GAME]\\file.xml [USER]/log.txt","privacy redaction failed: "+value);
            });
            Test("repeated toggle cycles preserve translation saves other mods and journal",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-cycles");e.Install(d,Nothing);Put(d,"user1.dat","save-marker");Put(d,"BepInEx/plugins/Friend.dll","friend");
                string journal=File.ReadAllText(At(d,".silksong-arabic-installer/state.xml"));
                for(int i=0;i<3;i++)
                {
                    Require(e.Review(d).CanDisable,"disable unavailable");e.DisableTranslation(d,Nothing);e.DisableTranslation(d,Nothing);
                    Require(!File.Exists(At(d,plugin))&&File.Exists(At(d,parked)),"DLL still in loader path");var r=e.Review(d);
                    Require(r.TranslationDisabled&&r.CanEnable&&r.CanRemove&&r.CanLaunch&&!r.CanRepair&&!r.CanInstall,"off actions incorrect");
                    ExpectFailure(()=>e.Install(d,Nothing),"install while off");ExpectFailure(()=>e.Repair(d,Nothing),"repair while off");
                    e.EnableTranslation(d,Nothing);e.EnableTranslation(d,Nothing);Require(e.Review(d).CanDisable&&!File.Exists(At(d,marker)),"on state incorrect");
                }
                Require(File.ReadAllText(At(d,"user1.dat"))=="save-marker"&&File.ReadAllText(At(d,"BepInEx/plugins/Friend.dll"))=="friend","unrelated file changed");
                Require(File.ReadAllText(At(d,".silksong-arabic-installer/state.xml"))==journal,"ownership changed");
                foreach(var f in p.Files)Require(Package.HashFile(At(d,f.Path))==f.Hash,"payload changed by toggle");
            });
            Test("recognized manual translation can toggle without being claimed",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-manual");foreach(var f in p.Files)Put(d,f.Path,System.Text.Encoding.UTF8.GetString(f.Data));
                Require(e.Review(d).CanDisable&&!e.Review(d).CanRemove,"manual ownership wrong");e.DisableTranslation(d,Nothing);e.EnableTranslation(d,Nothing);
                Require(!File.Exists(At(d,".silksong-arabic-installer/state.xml")),"manual install claimed");Require(Package.HashFile(At(d,plugin))==p.Files[2].Hash,"manual DLL changed");
            });
            Test("uninstall while disabled removes parked copy and restores earlier DLL",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("off-remove-old");Put(d,plugin,"previous-manual-DLL");e.Install(d,Nothing);e.DisableTranslation(d,Nothing);e.Uninstall(d,Nothing);
                Require(File.ReadAllText(At(d,plugin))=="previous-manual-DLL","original DLL not restored");Require(!File.Exists(At(d,parked))&&!File.Exists(At(d,marker)),"toggle left behind");
            });
            Test("uninstall respects identical preexisting DLL after disabling",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("off-remove-unowned");Put(d,plugin,"plugin-data");e.Install(d,Nothing);e.DisableTranslation(d,Nothing);e.Uninstall(d,Nothing);
                Require(File.ReadAllText(At(d,plugin))=="plugin-data","preexisting DLL removed");Require(!File.Exists(At(d,parked)),"parked copy remained");
            });
            Test("disable works after update but enable requires supported build",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-updated");e.Install(d,Nothing);Put(d,"Hollow Knight Silksong.exe","updated-game");Require(e.Review(d).CanDisable,"cannot disable after update");
                e.DisableTranslation(d,Nothing);var r=e.Review(d);Require(r.CanRemove&&r.CanLaunch&&!r.CanEnable,"updated off actions wrong");ExpectFailure(()=>e.EnableTranslation(d,Nothing),"unsupported enable");e.Uninstall(d,Nothing);
                Require(!File.Exists(At(d,plugin))&&!File.Exists(At(d,parked)),"updated removal failed");
            });
            Test("running game blocks both toggle directions",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-running");e.Install(d,Nothing);var runningEngine=new Engine(p,()=>true);
                ExpectFailure(()=>runningEngine.DisableTranslation(d,Nothing),"running disable");Require(!File.Exists(At(d,marker)),"marker created while running");
                e.DisableTranslation(d,Nothing);ExpectFailure(()=>runningEngine.EnableTranslation(d,Nothing),"running enable");Require(File.Exists(At(d,parked)),"enabled while running");
            });
            Test("tampered parked DLL blocks enabling and removal without mutation",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-corrupt");e.Install(d,Nothing);e.DisableTranslation(d,Nothing);Put(d,parked,"changed");
                var r=e.Review(d);Require(!r.CanEnable&&!r.CanRemove&&!r.CanInstall,"corrupt toggle actions enabled");ExpectFailure(()=>e.EnableTranslation(d,Nothing),"corrupt enable");ExpectFailure(()=>e.Uninstall(d,Nothing),"corrupt remove");
                Require(File.ReadAllText(At(d,parked))=="changed"&&File.Exists(At(d,"winhttp.dll")),"corruption preflight mutated files");
            });
            Test("duplicate active and parked DLLs are preserved on conflict",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-duplicate");e.Install(d,Nothing);e.DisableTranslation(d,Nothing);Put(d,plugin,"friend-copy");ExpectFailure(()=>e.EnableTranslation(d,Nothing),"duplicate");ExpectFailure(()=>e.Uninstall(d,Nothing),"duplicate remove");
                Require(File.Exists(At(d,parked))&&File.ReadAllText(At(d,plugin))=="friend-copy","conflict copy destroyed");
            });
            Test("interrupted disable before move can safely restore active state",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-before-move");e.Install(d,Nothing);e.AfterToggleStep=i=>{if(i==1)throw new IOException("power loss before move");};
                ExpectFailure(()=>e.DisableTranslation(d,Nothing),"before move");e.AfterToggleStep=null;var r=e.Review(d);Require(r.TogglePending&&r.CanEnable,"recovery unavailable");e.EnableTranslation(d,Nothing);
                Require(File.Exists(At(d,plugin))&&!File.Exists(At(d,marker)),"pending marker not cleared");
            });
            Test("interrupted disable after move is recognized as disabled",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-after-disable");e.Install(d,Nothing);e.AfterToggleStep=i=>{if(i==2)throw new IOException("power loss after disable");};
                ExpectFailure(()=>e.DisableTranslation(d,Nothing),"after move");e.AfterToggleStep=null;Require(e.Review(d).TranslationDisabled,"disabled state lost");e.EnableTranslation(d,Nothing);Require(File.Exists(At(d,plugin)),"cannot recover");
            });
            Test("interrupted enable after move can complete idempotently",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-after-enable");e.Install(d,Nothing);e.DisableTranslation(d,Nothing);e.AfterToggleStep=i=>{if(i==2)throw new IOException("power loss after enable");};
                ExpectFailure(()=>e.EnableTranslation(d,Nothing),"after enable");e.AfterToggleStep=null;Require(e.Review(d).TogglePending,"pending enable not recognized");e.EnableTranslation(d.ToUpperInvariant(),Nothing);Require(!File.Exists(At(d,marker)),"enable marker survived");
            });
            Test("legacy manual disable marker is not silently mixed",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-legacy");e.Install(d,Nothing);Put(d,plugin+".disabled","older-copy");ExpectFailure(()=>e.DisableTranslation(d,Nothing),"legacy disabled copy");Require(!File.Exists(At(d,marker)),"mixed markers");
            });
            Test("incomplete runtime blocks enable but still allows uninstall",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-missing-data");e.Install(d,Nothing);e.DisableTranslation(d,Nothing);File.Delete(At(d,p.Files.Last().Path));
                Require(!e.Review(d).CanEnable&&e.Review(d).CanRemove,"missing runtime actions incorrect");ExpectFailure(()=>e.EnableTranslation(d,Nothing),"missing data");e.Uninstall(d,Nothing);Require(!File.Exists(At(d,parked)),"off uninstall failed");
            });
            Test("wrong-root toggle journal blocks all mutation",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-wrong-root");e.Install(d,Nothing);e.DisableTranslation(d,Nothing);var x=XDocument.Load(At(d,marker));x.Root.SetAttributeValue("root",Path.GetPathRoot(d));x.Save(At(d,marker));
                ExpectFailure(()=>e.EnableTranslation(d,Nothing),"wrong root");ExpectFailure(()=>e.Uninstall(d,Nothing),"wrong-root remove");Require(File.Exists(At(d,parked)),"wrong-root payload touched");
            });
            Test("toggle shares exclusive operation lock",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-lock");e.Install(d,Nothing);using(var f=new FileStream(At(d,".silksong-arabic-installer/operation.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))ExpectFailure(()=>e.DisableTranslation(d,Nothing),"concurrent toggle");Require(!File.Exists(At(d,marker)),"lock bypassed");
            });
            Test("unknown manual plugin is never disabled",()=>{
                var p=Fake();var e=Create(p);string d=Fresh("toggle-unknown");Put(d,plugin,"unknown-plugin");Require(!e.Review(d).CanDisable,"unknown disable offered");ExpectFailure(()=>e.DisableTranslation(d,Nothing),"unknown plugin");Require(File.ReadAllText(At(d,plugin))=="unknown-plugin","unknown changed");
            });
            if(args.Length>1)
            Test("real embedded package install toggle repair remove in isolated game file copy",()=>{
                var p=Package.Load();string d=Path.Combine(root,"real-package");Directory.CreateDirectory(d);
                foreach(var file in p.GameHashes.Keys){string target=At(d,file);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Path.Combine(args[1],file.Replace('/',Path.DirectorySeparatorChar)),target);}
                var e=Create(p);e.Install(d,Nothing);e.DisableTranslation(d,Nothing);Require(e.Review(d).CanEnable,"real toggle cannot enable");e.EnableTranslation(d,Nothing);
                File.Delete(At(d,plugin));e.Repair(d,Nothing);foreach(var f in p.Files)Require(Package.HashFile(At(d,f.Path))==f.Hash,"real payload mismatch");e.DisableTranslation(d,Nothing);e.Uninstall(d,Nothing);
                Require(!File.Exists(At(d,parked))&&!File.Exists(At(d,marker)),"real toggle left behind");
                foreach(var f in p.Files)Require(!File.Exists(At(d,f.Path)),"real payload remained");foreach(var g in p.GameHashes)Require(Package.HashFile(At(d,g.Key))==g.Value,"game changed");
            });
            Console.WriteLine("TOTAL PASS: "+passed);File.WriteAllText(Path.Combine(root,"result.txt"),"PASS "+passed+" tests; "+DateTime.UtcNow.ToString("O"));return 0;
        }
        catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}
    }
}
