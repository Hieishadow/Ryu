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
    [Activity(Label = "DragoNX", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        ListView _list; List<string> _games = new();
        TextView _status; string _baseDir="", _gamesDir="", _keysDir="", _firmwareDir="";

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            _baseDir = Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "Ryujinx");
            _gamesDir = Path.Combine(_baseDir, "games");
            _keysDir = Path.Combine(_baseDir, "system");
            _firmwareDir = Path.Combine(_baseDir, "bis");
            Directory.CreateDirectory(_gamesDir); Directory.CreateDirectory(_keysDir); Directory.CreateDirectory(_firmwareDir);

            var root = new LinearLayout(this){Orientation=Orientation.Vertical};
            root.SetBackgroundColor(Android.Graphics.Color.ParseColor("#0F0F0F"));

            _status = new TextView(this); _status.SetPadding(20,20,20,20);
            _status.SetTextColor(Android.Graphics.Color.White);
            root.AddView(_status);

            var row = new LinearLayout(this){Orientation=Orientation.Horizontal};
            var btnGames = new Button(this){Text="📁 JOGOS"}; 
            var btnKeys = new Button(this){Text="🔑 KEYS"}; 
            var btnFirm = new Button(this){Text="💾 FIRMWARE"};
            btnGames.Click += (s,e)=>PickFolder(1001);
            btnKeys.Click += (s,e)=>PickFile(1002);
            btnFirm.Click += (s,e)=>PickFile(1003);
            row.AddView(btnGames); row.AddView(btnKeys); row.AddView(btnFirm);
            root.AddView(row);

            _list = new ListView(this);
            _list.ItemClick += (s,e)=>{
                var i = new Intent(this, typeof(GameActivity));
                i.PutExtra("gamePath", _games[e.Position]);
                i.PutExtra("baseDir", _baseDir);
                StartActivity(i);
            };
            root.AddView(_list, new LinearLayout.LayoutParams(-1,-1));
            SetContentView(root);
            RefreshGames();
        }
        void UpdateStatus(){
            bool hasProd=File.Exists(Path.Combine(_keysDir,"prod.keys"));
            bool hasTitle=File.Exists(Path.Combine(_keysDir,"title.keys"));
            bool hasFirm=Directory.Exists(_firmwareDir) && Directory.GetFiles(_firmwareDir,"*.nca",SearchOption.AllDirectories).Length>50;
            _status.Text=$"DragoNX ARM64 | JIT:ON Vulkan:ON\nBase: {_baseDir}\nKeys: prod={(hasProd?"OK":"FALTA")} title={(hasTitle?"OK":"FALTA")} | Firmware: {(hasFirm?"OK":"FALTA")}\nColoque prod.keys em /Ryujinx/system/";
        }
        void PickFolder(int c){StartActivityForResult(new Intent(Intent.ActionOpenDocumentTree), c);}
        void PickFile(int c){var it=new Intent(Intent.ActionOpenDocument); it.SetType("*/*"); StartActivityForResult(it,c);}
        protected override void OnActivityResult(int rc, Result res, Intent? data){
            base.OnActivityResult(rc,res,data);
            if(res!=Result.Ok || data?.Data==null) return;
            try{
                if(rc==1002){using var inp=ContentResolver.OpenInputStream(data.Data); using var outF=File.Create(Path.Combine(_keysDir,"prod.keys")); inp!.CopyTo(outF); Toast.MakeText(this,"prod.keys instalado!",ToastLength.Long).Show();}
                if(rc==1003){using var inp=ContentResolver.OpenInputStream(data.Data); var zp=Path.Combine(_baseDir,"firmware.zip"); using var outF=File.Create(zp); inp!.CopyTo(outF); System.IO.Compression.ZipFile.ExtractToDirectory(zp,_firmwareDir,true); Toast.MakeText(this,"Firmware extraido!",ToastLength.Long).Show();}
            }catch(System.Exception ex){Toast.MakeText(this,ex.Message,ToastLength.Long).Show();}
            RefreshGames();
        }
        void RefreshGames(){
            try{
                _games=Directory.GetFiles(_gamesDir,"*.*",SearchOption.AllDirectories).Where(f=>f.EndsWith(".nsp")||f.EndsWith(".xci")||f.EndsWith(".nsz")||f.EndsWith(".xcz")).ToList();
                _list.Adapter=new ArrayAdapter<string>(this,global::Android.Resource.Layout.SimpleListItem1,_games.Select(Path.GetFileName).ToList()!);
                UpdateStatus();
            }catch{}
        }
    }
}
