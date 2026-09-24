using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace SilksongArabicSetup
{
    static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try{Application.Run(new ModernForm(Package.Load(),args));}
            catch(Exception e){MessageBox.Show(e.Message,"تعريب Silksong",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
    }
}
