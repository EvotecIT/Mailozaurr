//using System.Data.SQLite;

//using MimeKit.Cryptography;

//namespace Mailozaurr;
//public class MySecureMimeContext : DefaultSecureMimeContext {
//    public static string DatabasePath { get; set; } = "C:\\Temp\\certdb.sqlite";

//    public MySecureMimeContext() : base(OpenDatabase(DatabasePath)) { }

//    static IX509CertificateDatabase OpenDatabase(string fileName) {
//        var builder = new SQLiteConnectionStringBuilder();
//        builder.DateTimeFormat = SQLiteDateFormats.Ticks;
//        builder.DataSource = fileName;

//        if (!File.Exists(fileName)) {
//            SQLiteConnection.CreateFile(fileName);
//        }

//        var sqlite = new SQLiteConnection(builder.ConnectionString);
//        sqlite.Open();

//        return new SqliteCertificateDatabase(sqlite, "password");
//    }
//}
