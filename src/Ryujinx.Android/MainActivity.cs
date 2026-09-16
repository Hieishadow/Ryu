using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RyujinxAndroid
{
    [Activity(Label = "Ryujinx", MainLauncher = true)]
    public class MainActivity : Activity
    {
        TextView logView;
        bool surfaceReady = true;

        protected override void OnCreate(Bundle b)
        {
            base.OnCreate(b);
            var lay = new LinearLayout(this){Orientation=Orientation.Vertical};
            logView = new TextView(this){TextSize=10f};
            var scroll = new ScrollView(this);
            scroll.AddView(logView);
            lay.AddView(scroll);
            SetContentView(lay);
            AddLog("V17.1 HUNTER2 - so 1.1.x");
            TryBoot();
        }

        void AddLog(string s){ RunOnUiThread(()=>{ logView.Text += "\n"+s; }); }

        void TryBoot()
        {
            if(!surfaceReady) return;
            Task.Run(() => {
                try {
                    var allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}}).ToArray();
                    var iRenderer = allTypes.FirstOrDefault(t=>t.FullName=="Ryujinx.Graphics.GAL.IRenderer");
                    var iWindow = allTypes.FirstOrDefault(t=>t.FullName=="Ryujinx.Graphics.GAL.IWindow");
                    var iAudio = allTypes.FirstOrDefault(t=>t.FullName=="Ryujinx.Audio.Integration.IHardwareDeviceDriver");
                    AddLog($"IRenderer:{iRenderer!=null} IWindow:{iWindow!=null} Audio:{iAudio!=null}");
                    if(iRenderer!=null) {
                        AddLog("--- RENDERER ---");
                        foreach(var m in iRenderer.GetMethods()) {
                            var ps = string.Join(", ", m.GetParameters().Select(p=>p.ParameterType.Name+" "+p.Name));
                            AddLog($"R: {m.ReturnType.Name} {m.Name}({ps})");
                        }
                        foreach(var p in iRenderer.GetProperties()) AddLog($"RP: {p.PropertyType.Name} {p.Name}");
                    }
                    if(iWindow!=null) {
                        AddLog("--- WINDOW ---");
                        foreach(var m in iWindow.GetMethods()) {
                            var ps = string.Join(", ", m.GetParameters().Select(p=>p.ParameterType.Name+" "+p.Name));
                            AddLog($"W: {m.ReturnType.Name} {m.Name}({ps})");
                        }
                        foreach(var p in iWindow.GetProperties()) AddLog($"WP: {p.PropertyType.Name} {p.Name}");
                    }
                    if(iAudio!=null) {
                        AddLog("--- AUDIO ---");
                        foreach(var m in iAudio.GetMethods()) {
                            var ps = string.Join(", ", m.GetParameters().Select(p=>p.ParameterType.Name+" "+p.Name));
                            AddLog($"A: {m.ReturnType.Name} {m.Name}({ps})");
                        }
                    }
                } catch(Exception ex){ AddLog("ERRO: "+ex.ToString()); }
            });
        }
    }
}
