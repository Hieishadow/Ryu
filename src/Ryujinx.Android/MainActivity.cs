using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX", MainLauncher = true, Exported = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class MainActivity : Activity
    {
        List<string> _games = new();
        ListView _list = null!;
        TextView _status = null!;
        string _baseDir = "/storage/emulated/0/Download/DragoNX";

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                if (!global::Android.OS.Environment.IsExternalStorageManager)
                {
                    try{
                        var i = new Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                        i.SetData(global::Android.Net.Uri.Parse("package:" + PackageName));
                        StartActivity(i);
                    }catch{
                        StartActivity(new Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission));
                    }
                }
            }
            try{ Directory.CreateDirectory(_baseDir); Directory.CreateDirectory(Path.Combine(_baseDir,"games")); Directory.CreateDirectory(Path.Combine(_baseDir,"system")); }catch{}
            var root = new LinearLayout(this){Orientation=Orientation.Vertical};
            root.SetBackgroundColor(global::Android.Graphics.Color.Black);
            _status = new TextView(this);
            _status.SetPadding(20,20,20,20);
            _status.SetTextColor(global::Android.Graphics.Color.White);
            root.AddView(_status);
            var btnRefresh = new Button(this){Text="ATUALIZAR JOGOS"};
            btnRefresh.Click += (s,e)=> RefreshList();
            root.AddView(btnRefresh);
            _list = new ListView(this);
            _list.ItemClick += (s,e)=>{
                if(e.Position < 0 || e.Position >= _games.Count) return;
                var it = new Intent(this, typeof(GameActivity));
                it.PutExtra("gamePath", _games[e.Position]);
                it.PutExtra("baseDir", _baseDir);
                StartActivity(it);
            };
            root.AddView(_list, new LinearLayout.LayoutParams(-1,-1));
            SetContentView(root);
            RefreshList();
        }
        void RefreshList()
        {
            try
            {
                string gamesDir = Path.Combine(_baseDir, "games");
                Directory.CreateDirectory(gamesDir);
                bool hasKeys = File.Exists(Path.Combine(_baseDir,"system","prod.keys"));
                _games = Directory.GetFiles(gamesDir, "*.*", SearchOption.AllDirectories).Where(f=>f.EndsWith(".nsp")||f.EndsWith(".xci")||f.EndsWith(".nsz")||f.EndsWith(".xcz")).ToList();
                _status.Text = $"DragoNX | Base: {_baseDir}\nprod.keys: {(hasKeys?"OK":"FALTA")}\nJogos: {_games.Count}";
                _list.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleListItem1, _games.Select(Path.GetFileName).ToList()!);
            }
            catch(System.Exception ex){ _status.Text = "Erro: " + ex.Message; }
        }
    }
}
