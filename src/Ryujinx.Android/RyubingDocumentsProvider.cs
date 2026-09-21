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
        var cols = projection ?? new[] {
            DocumentsContract.Root.ColumnRootId,
            DocumentsContract.Root.ColumnFlags,
            DocumentsContract.Root.ColumnTitle,
            DocumentsContract.Root.ColumnDocumentId,
            DocumentsContract.Root.ColumnAvailableBytes
        };
        var c = new MatrixCursor(cols);
        var row = c.NewRow();
        int rootFlags = 1 | 8; // LOCAL_ONLY | SUPPORTS_IS_CHILD
        foreach(var col in cols){
            if(col == DocumentsContract.Root.ColumnRootId) row.Add("ryubing_root");
            else if(col == DocumentsContract.Root.ColumnFlags) row.Add(rootFlags);
            else if(col == DocumentsContract.Root.ColumnTitle) row.Add("Ryubing");
            else if(col == DocumentsContract.Root.ColumnDocumentId) row.Add("ryubing:/");
            else if(col == DocumentsContract.Root.ColumnAvailableBytes) row.Add(10000000000L);
            else row.Add(null);
        }
        return c;
    }

    string DocIdToPath(string docId){
        if(docId == "ryubing:/") return Context.FilesDir.AbsolutePath;
        if(docId.StartsWith("ryubing:")) return docId.Substring(8);
        return docId;
    }

    public override ICursor QueryDocument(string docId, string[] projection)
    {
        var cols = projection ?? new[] {
            DocumentsContract.Document.ColumnDocumentId,
            DocumentsContract.Document.ColumnMimeType,
            DocumentsContract.Document.ColumnDisplayName,
            DocumentsContract.Document.ColumnFlags,
            DocumentsContract.Document.ColumnSize,
            DocumentsContract.Document.ColumnLastModified
        };
        var c = new MatrixCursor(cols);
        string path = DocIdToPath(docId);
        bool isDir = Directory.Exists(path);
        var row = c.NewRow();
        int flags = 2 | 4 | 8;
        foreach(var col in cols){
            if(col == DocumentsContract.Document.ColumnDocumentId) row.Add(docId);
            else if(col == DocumentsContract.Document.ColumnMimeType) row.Add(isDir ? DocumentsContract.Document.MimeTypeDir : "application/octet-stream");
            else if(col == DocumentsContract.Document.ColumnDisplayName) row.Add(docId=="ryubing:/" ? "Ryubing" : (isDir ? new DirectoryInfo(path).Name : Path.GetFileName(path)));
            else if(col == DocumentsContract.Document.ColumnFlags) row.Add(flags);
            else if(col == DocumentsContract.Document.ColumnSize) row.Add(isDir ? 0L : (File.Exists(path) ? new FileInfo(path).Length : 0L));
            else if(col == DocumentsContract.Document.ColumnLastModified) row.Add(Java.Lang.JavaSystem.CurrentTimeMillis());
            else row.Add(null);
        }
        return c;
    }

    public override ICursor QueryChildDocuments(string parentDocId, string[] projection, string sortOrder)
    {
        var cols = projection ?? new[] {
            DocumentsContract.Document.ColumnDocumentId,
            DocumentsContract.Document.ColumnMimeType,
            DocumentsContract.Document.ColumnDisplayName,
            DocumentsContract.Document.ColumnFlags,
            DocumentsContract.Document.ColumnSize,
            DocumentsContract.Document.ColumnLastModified
        };
        var c = new MatrixCursor(cols);
        string parentPath = DocIdToPath(parentDocId);
        if(!Directory.Exists(parentPath)) return c;
        foreach(var d in Directory.GetDirectories(parentPath)){
            var r = c.NewRow();
            foreach(var col in cols){
                if(col == DocumentsContract.Document.ColumnDocumentId) r.Add("ryubing:"+d);
                else if(col == DocumentsContract.Document.ColumnMimeType) r.Add(DocumentsContract.Document.MimeTypeDir);
                else if(col == DocumentsContract.Document.ColumnDisplayName) r.Add(Path.GetFileName(d));
                else if(col == DocumentsContract.Document.ColumnFlags) r.Add(8);
                else if(col == DocumentsContract.Document.ColumnSize) r.Add(0L);
                else if(col == DocumentsContract.Document.ColumnLastModified) r.Add(Java.Lang.JavaSystem.CurrentTimeMillis());
                else r.Add(null);
            }
        }
        foreach(var f in Directory.GetFiles(parentPath)){
            var r = c.NewRow();
            foreach(var col in cols){
                if(col == DocumentsContract.Document.ColumnDocumentId) r.Add("ryubing:"+f);
                else if(col == DocumentsContract.Document.ColumnMimeType) r.Add("application/octet-stream");
                else if(col == DocumentsContract.Document.ColumnDisplayName) r.Add(Path.GetFileName(f));
                else if(col == DocumentsContract.Document.ColumnFlags) r.Add(2 | 4);
                else if(col == DocumentsContract.Document.ColumnSize) r.Add(new FileInfo(f).Length);
                else if(col == DocumentsContract.Document.ColumnLastModified) r.Add(Java.Lang.JavaSystem.CurrentTimeMillis());
                else r.Add(null);
            }
        }
        return c;
    }

    public override ParcelFileDescriptor OpenDocument(string documentId, string mode, CancellationSignal signal)
    {
        var file = new Java.IO.File(DocIdToPath(documentId));
        var pMode = mode.Contains("w") ? ParcelFileMode.ReadWrite : ParcelFileMode.ReadOnly;
        return ParcelFileDescriptor.Open(file, pMode);
    }
}
