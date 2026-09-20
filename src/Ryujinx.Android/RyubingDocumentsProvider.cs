#nullable disable
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;
using System.IO;

namespace Ryujinx.Android;

[ContentProvider(new string[]{"com.ryubing.android.documents"},
    Name="com.ryubing.android.RyubingDocumentsProvider",
    Exported=true, GrantUriPermissions=true)]
public class RyubingDocumentsProvider : DocumentsProvider
{
    public override bool OnCreate() => true;

    public override ICursor QueryRoots(string[] projection)
    {
        var c = new MatrixCursor(projection?? new[]{"root_id","flags","title","document_id","available_bytes"});
        var row = c.NewRow();
        // NET10 - SEM OBSOLETO: usa DocumentRootFlags
        int flags = (int)(DocumentRootFlags.LocalOnly | DocumentRootFlags.SupportsRecents | DocumentRootFlags.SupportsIsChild);
        row.Add("ryubing_root");
        row.Add(flags);
        row.Add("Ryubing");
        row.Add("ryubing:/");
        row.Add(10000000000L);
        return c;
    }

    public override ICursor QueryDocument(string docId, string[] projection)
    {
        var c = new MatrixCursor(projection?? new[]{"document_id","mime_type","display_name","flags","size","last_modified"});
        string path = DocIdToPath(docId);
        bool isDir = Directory.Exists(path);
        var row = c.NewRow();
        int flags = (int)(DocumentContractFlags.SupportsWrite | DocumentContractFlags.SupportsDelete);
        row.Add(docId);
        row.Add(isDir? DocumentsContract.Document.MimeTypeDir : "application/octet-stream");
        row.Add(isDir? new DirectoryInfo(path).Name : Path.GetFileName(path));
        row.Add(flags);
        row.Add(isDir? 0L : (File.Exists(path)? new FileInfo(path).Length : 0L));
        row.Add(Java.Lang.JavaSystem.CurrentTimeMillis());
        return c;
    }

    public override ICursor QueryChildDocuments(string parentDocId, string[] projection, string sortOrder)
    {
        var c = new MatrixCursor(projection?? new[]{"document_id","mime_type","display_name","size","last_modified"});
        string parentPath = DocIdToPath(parentDocId);
        if(!Directory.Exists(parentPath)) return c;
        foreach(var d in Directory.GetDirectories(parentPath)){
            var r = c.NewRow();
            r.Add("ryubing:"+d);
            r.Add(DocumentsContract.Document.MimeTypeDir);
            r.Add(Path.GetFileName(d));
            r.Add(0L);
            r.Add(Java.Lang.JavaSystem.CurrentTimeMillis());
        }
        foreach(var f in Directory.GetFiles(parentPath)){
            var r = c.NewRow();
            r.Add("ryubing:"+f);
            r.Add("application/octet-stream");
            r.Add(Path.GetFileName(f));
            r.Add(new FileInfo(f).Length);
            r.Add(Java.Lang.JavaSystem.CurrentTimeMillis());
        }
        return c;
    }

    // FIX ERRO L73: Enum.Parse não infere tipo no net10
    public override ParcelFileDescriptor OpenDocument(string documentId, string mode, CancellationSignal signal)
    {
        var file = new Java.IO.File(DocIdToPath(documentId));
        ParcelFileMode pMode = mode.Contains("w")? ParcelFileMode.ReadWrite : ParcelFileMode.ReadOnly;
        return ParcelFileDescriptor.Open(file, pMode);
    }

    string DocIdToPath(string docId) => docId == "ryubing:/"? Context.FilesDir.AbsolutePath : docId.Replace("ryubing:", "");
}
