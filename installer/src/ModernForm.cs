using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SilksongArabicSetup
{
    sealed class ModernForm:Form
    {
        const string Version="0.3.0 RC3";
        readonly Color ink=Color.FromArgb(26,38,51), muted=Color.FromArgb(89,104,121), accent=Color.FromArgb(22,100,112);
        readonly Engine engine;
        readonly ComboBox folder=new ComboBox();
        readonly TextBox log=new TextBox();
        readonly Label status=new Label(), totals=new Label();
        readonly ProgressBar progress=new ProgressBar();
        readonly ListView checks=new ListView();
        readonly StringBuilder sessionLog=new StringBuilder();
        Button install,repair,remove,toggle,review,browse,detect,admin,launch,export;
        bool busy;InstallReview lastReview;string checkedFolder="";

        public ModernForm(Package package,string[] args)
        {
            engine=new Engine(package,Engine.GameRunning);
            engine.ProgressChanged=(current,total)=>OnUI(()=>{progress.Style=ProgressBarStyle.Blocks;progress.Value=total==0?100:Math.Min(100,(int)(100L*current/total));});
            Text="تعريب سيلكسونغ | التثبيت والصيانة";ClientSize=new Size(980,744);MinimumSize=new Size(900,740);StartPosition=FormStartPosition.CenterScreen;
            Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
            Font=new Font("Segoe UI",10.5f);RightToLeft=RightToLeft.Yes;RightToLeftLayout=true;BackColor=Color.FromArgb(243,245,248);ForeColor=ink;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,116));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,76));Controls.Add(root);
            var header=new Panel{Dock=DockStyle.Fill,BackColor=ink,Margin=Padding.Empty,Padding=new Padding(26,14,26,12)};
            var title=TextLabel("سيلكسونغ بالعربية",25,Color.White);title.Dock=DockStyle.Top;title.Height=49;
            var subtitle=TextLabel("مثبّت التعريب  "+Version+"    •    Windows 64-bit\n١  اختر اللعبة     ←     ٢  افحص وثبّت     ←     ٣  شغّل واستمتع",10.5f,Color.FromArgb(220,229,235));subtitle.Dock=DockStyle.Fill;
            header.Controls.Add(subtitle);header.Controls.Add(title);root.Controls.Add(header,0,0);
            var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty};root.Controls.Add(scroll,0,1);
            var body=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Padding=new Padding(24,12,24,12)};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(body);
            var location=Card("موقع اللعبة",126);var pathGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=2};
            pathGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));pathGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));pathGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));
            pathGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,42));pathGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            folder.Dock=DockStyle.Fill;folder.RightToLeft=RightToLeft.No;folder.DropDownStyle=ComboBoxStyle.DropDown;folder.Margin=new Padding(3,7,3,3);folder.AccessibleName="مسار مجلد اللعبة";
            folder.TextChanged+=delegate{lastReview=null;checkedFolder="";UpdateActions();};pathGrid.Controls.Add(folder,0,0);
            browse=MakeButton("اختيار مجلد…",async delegate{using(var d=new FolderBrowserDialog{Description="اختر مجلد Hollow Knight Silksong",ShowNewFolderButton=false})if(d.ShowDialog(this)==DialogResult.OK){folder.Text=d.SelectedPath;await Review();}});
            detect=MakeButton("اكتشاف تلقائي",async delegate{Discover();await Review();});pathGrid.Controls.Add(browse,1,0);pathGrid.Controls.Add(detect,2,0);
            var supported=TextLabel("النسخة المدعومة: 1.0.30000 • Steam 22479045. بعد التثبيت اختر English داخل اللعبة.",10,muted);supported.Dock=DockStyle.Fill;
            pathGrid.Controls.Add(supported,0,1);pathGrid.SetColumnSpan(supported,3);location.Controls.Add(pathGrid);body.Controls.Add(location);
            var validation=Card("الفحص قبل التثبيت",238);
            checks.Dock=DockStyle.Fill;checks.View=View.Details;checks.FullRowSelect=true;checks.HeaderStyle=ColumnHeaderStyle.Nonclickable;checks.BorderStyle=BorderStyle.None;
            checks.RightToLeft=RightToLeft.Yes;checks.RightToLeftLayout=true;checks.MultiSelect=false;checks.ShowItemToolTips=true;
            checks.Columns.Add("الفحص",165);checks.Columns.Add("الحالة",100);checks.Columns.Add("التفاصيل",550);checks.AccessibleName="نتائج الفحص";
            checks.Resize+=delegate{if(checks.ClientSize.Width>300)checks.Columns[2].Width=Math.Max(260,checks.ClientSize.Width-290);};
            validation.Controls.Add(checks);body.Controls.Add(validation);
            var outcome=Card("الحالة والخطوة التالية",128);var outcomeGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};
            outcomeGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));outcomeGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,25));outcomeGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,8));
            status.Dock=DockStyle.Fill;status.Font=new Font(Font,FontStyle.Bold);status.Text="اختر مجلد اللعبة ثم اضغط «فحص».";
            totals.Dock=DockStyle.Fill;totals.Font=new Font(Font.FontFamily,9);totals.ForeColor=muted;progress.Dock=DockStyle.Fill;progress.Margin=Padding.Empty;
            outcomeGrid.Controls.Add(status,0,0);outcomeGrid.Controls.Add(totals,0,1);outcomeGrid.Controls.Add(progress,0,2);outcome.Controls.Add(outcomeGrid);body.Controls.Add(outcome);
            var utilities=new FlowLayoutPanel{Dock=DockStyle.Top,Height=44,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,WrapContents=true,Margin=Padding.Empty};
            launch=MakeButton("تشغيل عبر Steam",delegate{Open("steam://rungameid/1030300");});launch.Enabled=false;
            admin=MakeButton("إعادة الفتح كمسؤول",delegate{Elevate();});admin.Visible=false;
            export=MakeButton("حفظ تقرير الفحص",delegate{ExportReport();});
            var help=MakeButton("دليل الاستخدام",delegate{MessageBox.Show(this,
                "التثبيت\nاحفظ تقدمك وأغلق اللعبة، اختر مجلدها ثم افحصه. راجع النتائج واضغط تثبيت. اختر English داخل اللعبة.\n\n"+
                "المقارنة مع اللغة الأصلية\nأغلق اللعبة، ثم اختر تعطيل التعريب مؤقتًا. شغّل اللعبة وجربها، ثم أغلقها واضغط إعادة تفعيل التعريب. لا يعيد التبديل تنزيل الملفات أو نسخ الترجمة كاملة، ولا يغير الحفظ أو بقية التعديلات.\n\n"+
                "الإصلاح\nيعيد الملفات الناقصة التي أضافها المثبت فقط. يحتفظ بالملفات التي عدّلتها وبالنسخ الاحتياطية الأصلية.\n\n"+
                "الإزالة\nاحتفظ بهذا المثبت. يعيد الملفات السابقة ويترك الحفظ خارج نطاق عمله. إذا كان تعريب يدوي موجودًا قبل التثبيت، تستعيد الإزالة ذلك التعريب.\n\n"+
                "بعد تحديث اللعبة\nيلزم إصدار تعريب متوافق للتثبيت والإصلاح. تبقى الاستعادة متاحة إذا كان سجلها ونسخها الاحتياطية سليمين.\n\n"+
                "عند حدوث مشكلة\nاحفظ تقرير الفحص وأرسله إلى من أعطاك التعريب. تُستبدل مسارات اللعبة ومجلد المستخدم بعلامات عامة؛ راجع التقرير قبل إرساله.\n\n"+
                "تعريب مجتمعي غير رسمي، وليس منتجًا من Team Cherry. المثبت يعمل دون تنزيلات أو إرسال تقارير تلقائيًا.","دليل الاستخدام",MessageBoxButtons.OK,MessageBoxIcon.Information);});
            utilities.Controls.Add(launch);utilities.Controls.Add(export);utilities.Controls.Add(help);utilities.Controls.Add(admin);body.Controls.Add(utilities);
            var details=new LinkLabel{Text="إظهار / إخفاء تفاصيل العملية",AutoSize=true,LinkColor=accent,Margin=new Padding(4,8,4,8)};details.LinkClicked+=delegate{log.Visible=!log.Visible;};body.Controls.Add(details);
            log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.Dock=DockStyle.Top;log.Height=135;log.Visible=false;log.BackColor=Color.White;log.AccessibleName="تفاصيل العملية";body.Controls.Add(log);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(20,12,20,12),Margin=Padding.Empty,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
            install=MakeButton("تثبيت التعريب",async delegate{await Operate("install");});install.BackColor=accent;install.ForeColor=Color.White;install.FlatAppearance.BorderSize=0;install.MinimumSize=new Size(155,40);
            toggle=MakeButton("تعطيل التعريب مؤقتًا",async delegate{if(lastReview!=null)await Operate(lastReview.CanEnable?"enable":"disable");});
            review=MakeButton("فحص",async delegate{await Review();});repair=MakeButton("إصلاح الملفات الناقصة",async delegate{await Operate("repair");});remove=MakeButton("إزالة التثبيت",async delegate{await Operate("remove");});
            footer.Controls.Add(install);footer.Controls.Add(toggle);footer.Controls.Add(review);footer.Controls.Add(repair);footer.Controls.Add(remove);root.Controls.Add(footer,0,2);
            Shown+=async delegate{Discover();if(args.Length==2&&args[0]=="--path")folder.Text=args[1];await Review();};
            FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy){e.Cancel=true;status.Text="انتظر اكتمال العملية لحماية النسخ الاحتياطية.";}};UpdateActions();
        }
        Label TextLabel(string text,float size,Color color)
        {return new Label{Text=text,Font=new Font("Segoe UI",size),ForeColor=color,TextAlign=ContentAlignment.MiddleLeft};}
        Panel Card(string title,int height)
        {
            var card=new Panel{Dock=DockStyle.Top,Width=900,Height=height,BackColor=Color.White,Padding=new Padding(16,38,16,12),Margin=new Padding(0,0,0,10)};
            var heading=TextLabel(title,12,ink);heading.Font=new Font(heading.Font,FontStyle.Bold);heading.SetBounds(16,7,card.ClientSize.Width-32,26);heading.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;card.Controls.Add(heading);return card;
        }
        Button MakeButton(string text,EventHandler action)
        {
            var b=new Button{Text=text,AutoSize=true,Height=40,MinimumSize=new Size(110,40),Margin=new Padding(4),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=ink,Cursor=Cursors.Hand};
            b.FlatAppearance.BorderColor=Color.FromArgb(210,219,227);b.Click+=action;return b;
        }
        void OnUI(Action action){if(IsDisposed)return;if(InvokeRequired){BeginInvoke(action);return;}action();}
        void Report(string text){OnUI(()=>{sessionLog.AppendLine(text);log.AppendText(text+Environment.NewLine);});}
        void Discover(){var found=Engine.Discover();folder.Items.Clear();foreach(var path in found)folder.Items.Add(path);if(found.Count>0)folder.SelectedIndex=0;else status.Text="لم يُكتشف المجلد تلقائيًا. استخدم «اختيار مجلد».";}
        void UpdateActions()
        {
            if(install==null)return;bool current=lastReview!=null&&string.Equals(checkedFolder,folder.Text.Trim(),StringComparison.OrdinalIgnoreCase);
            install.Enabled=!busy&&current&&lastReview.CanInstall;repair.Enabled=!busy&&current&&lastReview.CanRepair;remove.Enabled=!busy&&current&&lastReview.CanRemove;
            toggle.Enabled=!busy&&current&&(lastReview.CanEnable||lastReview.CanDisable);
            toggle.Text=current&&(lastReview.TranslationDisabled||lastReview.TogglePending)?"إعادة تفعيل التعريب":"تعطيل التعريب مؤقتًا";
            review.Enabled=!busy;folder.Enabled=!busy;browse.Enabled=!busy;detect.Enabled=!busy;admin.Enabled=!busy;export.Enabled=!busy;
            launch.Enabled=!busy&&current&&lastReview.CanLaunch;
            launch.Text=current&&lastReview.TranslationDisabled?"تشغيل باللغة الأصلية":"تشغيل عبر Steam";
        }
        void Display(InstallReview result,string path)
        {
            lastReview=result;checkedFolder=path;checks.BeginUpdate();checks.Items.Clear();
            foreach(var c in result.Checks){var row=new ListViewItem(c.Name);row.SubItems.Add(c.Passed?"سليم":"يحتاج انتباهًا");row.SubItems.Add(c.Detail);row.ToolTipText=c.Detail;row.ForeColor=c.Passed?ink:Color.FromArgb(154,67,37);checks.Items.Add(row);}
            checks.EndUpdate();status.Text=result.Summary;totals.Text="الحفظ خارج نطاق المثبت  •  النسخ الاحتياطية داخل مجلد اللعبة  •  دون تنزيلات";
        }
        async Task Review()
        {
            if(busy)return;busy=true;UpdateActions();progress.Style=ProgressBarStyle.Marquee;status.Text="جارٍ فحص الإصدار والملفات…";string path=folder.Text.Trim();
            try{Display(await Task.Run(()=>engine.Review(path)),path);}
            catch(Exception e){status.Text="تعذر إكمال الفحص. راجع التفاصيل أو احفظ تقريرًا.";Report(e.Message);lastReview=null;log.Visible=true;}
            finally{busy=false;progress.Style=ProgressBarStyle.Blocks;progress.Value=0;UpdateActions();}
        }
        async Task Operate(string action)
        {
            if(busy||lastReview==null)return;
            if(action=="remove"&&MessageBox.Show(this,"ستُزال ملفات هذا التثبيت وتُستعاد الملفات السابقة. تبقى النسخ الاحتياطية وملفات الحفظ محفوظة.\n\nهل تريد المتابعة؟","إزالة التعريب",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            string path=folder.Text.Trim(),completion=null;bool success=false;busy=true;UpdateActions();progress.Value=0;progress.Style=ProgressBarStyle.Marquee;status.Text="جارٍ العمل والتحقق من الملفات…";admin.Visible=false;Report("--- "+action+" ---");
            try{completion=await Task.Run(()=>RunOperation(action,path));success=true;Report(completion);}
            catch(UnauthorizedAccessException){completion="تعذرت الكتابة. أعد فتح المثبت كمسؤول ثم أعد الفحص.";admin.Visible=true;Report(completion);log.Visible=true;}
            catch(Exception e){completion="لم تكتمل العملية؛ راجع التفاصيل ثم أعد الفحص.";Report(e.Message);log.Visible=true;}
            try{Display(await Task.Run(()=>engine.Review(path)),path);}catch{lastReview=null;}
            status.Text=completion;progress.Style=ProgressBarStyle.Blocks;progress.Value=success?100:0;busy=false;UpdateActions();
        }
        string RunOperation(string action,string path)
        {
            switch(action)
            {
                case "install":return engine.Install(path,Report);
                case "repair":return engine.Repair(path,Report);
                case "remove":return engine.Uninstall(path,Report);
                case "disable":return engine.DisableTranslation(path,Report);
                case "enable":return engine.EnableTranslation(path,Report);
                default:throw new IOException("عملية غير معروفة.");
            }
        }
        void ExportReport()
        {
            using(var dialog=new SaveFileDialog{Title="حفظ تقرير الفحص",Filter="Text report (*.txt)|*.txt",FileName="Silksong-Arabic-report.txt",OverwritePrompt=true})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try
                {
                    var text=new StringBuilder("Silksong Arabic installer "+Version+"\r\nUTC: "+DateTime.UtcNow.ToString("O")+"\r\nSupported game: 1.0.30000 / Steam 22479045\r\n");
                    if(lastReview!=null)foreach(var c in lastReview.Checks)text.AppendLine((c.Passed?"OK | ":"ATTENTION | ")+c.Name+" | "+c.Detail);text.AppendLine().Append(sessionLog);
                    File.WriteAllText(dialog.FileName,SupportReport.Redact(text.ToString(),folder.Text.Trim(),Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),new UTF8Encoding(true));status.Text="حُفظ التقرير. يمكنك مراجعته قبل مشاركته.";
                }
                catch(Exception e){MessageBox.Show(this,e.Message,"تعذر حفظ التقرير",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            }
        }
        void Open(string target){try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,"تعذر الفتح");}}
        void Elevate()
        {
            if(busy)return;
            try{Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location){UseShellExecute=true,Verb="runas",Arguments="--path \""+folder.Text.Trim().TrimEnd('\\').Replace("\"","")+"\""});Close();}
            catch(System.ComponentModel.Win32Exception){status.Text="لم يُمنح إذن المسؤول. لم يبدأ مثبّت آخر.";}
        }
    }
}
