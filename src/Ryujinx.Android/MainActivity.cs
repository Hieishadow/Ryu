using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Views;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Android.Content.PM;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX", MainLauncher = true, Exported = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout)]
    public class MainActivity : Activity
    {
        ListView _list = null!;
        List<string> _games = new();
        TextView _status = null!;
        string _baseDir="", _gamesDir="", _keysDir="", _firmwareDir="";

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            try
            {
                _baseDir = Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "Ryujinx");
                _gamesDir = Path.Combine(_baseDir, "games");
                _keysDir = Path.Combine(_baseDir, "system");
                _firmwareDir = Path.Combine(_baseDir, "bis");
                Directory.CreateDirectory(_gamesDir);
                Directory.CreateDirectory(_keysDir);
                Directory.CreateDirectory(_firmwareDir);

                var root = new LinearLayout(this){Orientation=Orientation.Vertical};
                root.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#0F0F0F"));

                _status = new TextView(this);
                _status.SetPadding(20,20,20,20);
                _status.SetTextColor(global::Android.Graphics.Color.White);
                root.AddView(_status);

                var row = new LinearLayout(this){Orientation=Orientation.Horizontal};
                var b1 = new Button(this){Text="JOGOS"};
                var b2 = new Button(this){Text="KEYS"};
                var b3 = new Button(this){Text="FIRMWARE"};
                b2.Click += (s,e)=>{ var it=new Intent(Intent.ActionOpenDocument); it.SetType("*/*"); StartActivityForResult(it,1002); };
                b3.Click += (s,e)=>{ var it=new Intent(Intent.ActionOpenDocument); it.SetType("*/*"); StartActivityForResult(it,1003); };
                row.AddView(b1); row.AddView(b2); row.AddView(b3);
                root.AddView(row);

                _list = new ListView(this);
                _list.ItemClick += (s,e)=>{
                    var intent2 = new Intent(this, typeof(GameActivity));
                    intent2.PutExtra("gamePath", _games[e.Position]);
                    intent2.PutExtra("baseDir", _baseDir);
                    StartActivity(intent2);
                };
                root.AddView(_list, new LinearLayout.LayoutParams(-1,-1));
                SetContentView(root);
                Refresh();
            }
            catch (System.Exception ex)
            {
                global::Android.Util.Log.Error("DragoNX", ex.ToString());
                Toast.MakeText(this, "Erro: " + ex.Message, ToastLength.Long)?.Show();
            }
        }
        void Refresh(){
            try{
                bool hasProd = File.Exists(Path.Combine(_keysDir,"prod.keys"));
                _status.Text = $"DragoNX | Base: {_baseDir}\nprod.keys: {(hasProd?"OK":"FALTA")} | Jogos: {_games.Count}\nJIT: ON | Vulkan: ON | Build 174 FIX";
                _games = Directory.GetFiles(_gamesDir,"*.*",SearchOption.AllDirectories).Where(f=>f.EndsWith(".nsp")||f.EndsWith(".xci")||f.EndsWith(".nsz")||f.EndsWith(".xcz")).ToList();
                _list.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleListItem1, _games.Select(Path.GetFileName).ToList()!);
            }catch{}
        }
        protected override void OnActivityResult(int rc, Result res, Intent? data){
            base.OnActivityResult(rc,res,data);
            if(res!=Result.Ok || data?.Data==null) return;
            try{
                if(rc==1002){ using var inp=ContentResolver.OpenInputStream(data.Data); using var outF=File.Create(Path.Combine(_keysDir,"prod.keys")); inp!.CopyTo(outF); }
                if(rc==1003){ using var inp=ContentResolver.OpenInputStream(data.Data); var zp=Path.Combine(_baseDir,"firmware.zip"); using var outF=File.Create(zp); inp!.CopyTo(outF); System.IO.Compression.ZipFile.ExtractToDirectory(zp,_firmwareDir,true); }
            }catch{}
            Refresh();
        }
    }
}
