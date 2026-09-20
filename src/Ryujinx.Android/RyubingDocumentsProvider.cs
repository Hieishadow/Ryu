#nullable disable
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;
using System.IO;

namespace Ryujinx.Android;

[ContentProvider(new string[]{"com.ryubing.android.documents"}, 
    Name="com.ryubing.android.RyubingDocumentsProvider",
    Exported=true,
    GrantUriPermissions=true,
    Permission="android.permission.MANAGE_DOCUMENTS")]
public class RyubingDocumentsProvider : DocumentsProvider
{
    public override bool OnCreate() => true;

    public override ICursor QueryRoots(string[] projection){
        var result = new MatrixCursor(projection?? new[]{"root_id","flags","icon","title","document_id","available_bytes"});
        var row = result.NewRow();
        row.Add("root_id", "ryubing_root");
        row.Add("flags", (int)(DocumentsContract.Root.FlagSupportsRecents | DocumentsContract.Root.FlagLocalOnly));
        row.Add("icon", 0);
        row.Add("title", "Ryubing");
        row.Add("document_id", "ryubing:/");
        row.Add("available_bytes", 10000000000L);
        result.AddRow(row);
        return result;
    }

    public override ICursor QueryDocument(string docId, string[] projection){
        var cursor = new MatrixCursor(projection?? new[]{"document_id","mime_type","display_name","flags","size","last_modified"});
        var row = cursor.NewRow();
        string path = DocIdToPath(docId);
        bool isDir = Directory.Exists(path);
        row.Add("document_id", docId);
        row.Add("mime_type", isDir? DocumentsContract.Document.MimeTypeDir : "application/octet-stream");
        row.Add("display_name", isDir? new DirectoryInfo(path).Name : Path.GetFileName(path));
        row.Add("flags", (int)(DocumentsContract.Document.FlagSupportsWrite | DocumentsContract.Document.FlagSupportsDelete | DocumentsContract.Document.FlagSupportsThumbnail));
        row.Add("size", isDir? 0 : (File.Exists(path)? new FileInfo(path).Length:0));
        row.Add("last_modified", Java.Lang.JavaSystem.CurrentTimeMillis());
        cursor.AddRow(row);
        return cursor;
    }

    public override ICursor QueryChildDocuments(string parentDocumentId, string[] projection, string sortOrder){
        var cursor = new MatrixCursor(projection?? new[]{"document_id","mime_type","display_name","flags","size","last_modified"});
        string parentPath = DocIdToPath(parentDocumentId);
        if(!Directory.Exists(parentPath)) return cursor;
        foreach(var dir in Directory.GetDirectories(parentPath)){
            var row = cursor.NewRow();
            row.Add("document_id", "ryubing:"+dir);
            row.Add("mime_type", DocumentsContract.Document.MimeTypeDir);
            row.Add("display_name", Path.GetFileName(dir));
            row.Add("flags", 0);
            row.Add("size", 0);
            row.Add("last_modified", Java.Lang.JavaSystem.CurrentTimeMillis());
            cursor.AddRow(row);
        }
        foreach(var file in Directory.GetFiles(parentPath)){
            var info = new FileInfo(file);
            var row = cursor.NewRow();
            row.Add("document_id", "ryubing:"+file);
            row.Add("mime_type", "application/octet-stream");
            row.Add("display_name", info.Name);
            row.Add("flags", 0);
            row.Add("size", info.Length);
            row.Add("last_modified", Java.Lang.JavaSystem.CurrentTimeMillis());
            cursor.AddRow(row);
        }
        return cursor;
    }

    public override ParcelFileDescriptor OpenDocument(string documentId, string mode, CancellationSignal signal){
        string path = DocIdToPath(documentId);
        return ParcelFileDescriptor.Open(new Java.IO.File(path), ParcelFileMode.Parse(mode));
    }

    string DocIdToPath(string docId){
        if(docId=="ryubing:/") return Context.FilesDir.AbsolutePath;
        return docId.Replace("ryubing:", "");
    }
}
