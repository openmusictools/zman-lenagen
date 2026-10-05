using Microsoft.Data.Sqlite;
using System.IO;
using System.Text.Json;
using ZmanLenagen.Core;
namespace ZmanLenagen.App;
public sealed class Store : IDisposable {
 public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZmanLenagen");
 private readonly SqliteConnection db;
 private readonly string folder;
 public Store(string? folderOverride=null) {
  folder=folderOverride??Folder;
  Directory.CreateDirectory(folder);
  db=new SqliteConnection($"Data Source={Path.Combine(folder,"practice.db")}");db.Open();
  using var c=db.CreateCommand(); c.CommandText="PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000; CREATE TABLE IF NOT EXISTS kv (key TEXT PRIMARY KEY, value TEXT NOT NULL); CREATE TABLE IF NOT EXISTS steps (id TEXT PRIMARY KEY, data TEXT NOT NULL); PRAGMA user_version=1;";c.ExecuteNonQuery();
  using var integrity=db.CreateCommand();integrity.CommandText="PRAGMA quick_check";
  if((string?)integrity.ExecuteScalar()!="ok") throw new IOException("מסד הנתונים דורש שחזור מגיבוי. הקבצים המקוריים לא נמחקו.");
  Backup();
 }
 public T? Get<T>(string key) {using var c=db.CreateCommand();c.CommandText="SELECT value FROM kv WHERE key=$k";c.Parameters.AddWithValue("$k",key);var v=c.ExecuteScalar();return v is string s ? JsonSerializer.Deserialize<T>(s):default;}
 public void Set<T>(string key,T value) {using var c=db.CreateCommand();c.CommandText="INSERT INTO kv VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=$v";c.Parameters.AddWithValue("$k",key);c.Parameters.AddWithValue("$v",JsonSerializer.Serialize(value));c.ExecuteNonQuery();}
 public List<PracticeStep> Steps() {using var c=db.CreateCommand();c.CommandText="SELECT data FROM steps";using var reader=c.ExecuteReader();var list=new List<PracticeStep>();while(reader.Read()) list.Add(JsonSerializer.Deserialize<PracticeStep>(reader.GetString(0))!);return list.OrderByDescending(s=>s.Started).ToList();}
 public void Save(RunState? run,PracticeStep? step=null) {
  using var tx=db.BeginTransaction();
  if(step!=null) {using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO steps VALUES($id,$data) ON CONFLICT(id) DO UPDATE SET data=$data";c.Parameters.AddWithValue("$id",step.Id);c.Parameters.AddWithValue("$data",JsonSerializer.Serialize(step));c.ExecuteNonQuery();}
  using(var c=db.CreateCommand()) {c.Transaction=tx;c.CommandText="INSERT INTO kv VALUES('run',$v) ON CONFLICT(key) DO UPDATE SET value=$v";c.Parameters.AddWithValue("$v",JsonSerializer.Serialize(run));c.ExecuteNonQuery();}tx.Commit();
 }
 public void Backup() {
  var dir=Path.Combine(folder,"backups");Directory.CreateDirectory(dir);
  var path=Path.Combine(dir,DateTime.UtcNow.ToString("yyyy-MM-dd")+".db");
  if(!File.Exists(path)) {using var target=new SqliteConnection($"Data Source={path}");target.Open();db.BackupDatabase(target);}
  foreach(var f in Directory.GetFiles(dir,"*.db").OrderDescending().Skip(14)) File.Delete(f);
 }
 public void Import(ExportData data) {
  if(data.Schema!=1 || data.Steps==null || data.Circuits==null) throw new ArgumentException("פורמט הגיבוי אינו נתמך.");
  foreach(var s in data.Steps) if(string.IsNullOrEmpty(s.Id)||string.IsNullOrEmpty(s.SessionId)||string.IsNullOrWhiteSpace(s.Tag)||s.PlannedMinutes<1||s.PlannedMinutes>1440||s.Segments==null||s.Segments.Any(x=>x.End<x.Start)||s.Feedback is not (null or "yes" or "no" or "half")) throw new ArgumentException("הגיבוי מכיל נתונים לא תקינים.");
  foreach(var circuit in data.Circuits) Metrics.ValidateCircuit(circuit);
  if(data.Draft is {Count:>0}) Metrics.ValidateCircuit(data.Draft);
  using var tx=db.BeginTransaction();
  foreach(var s in data.Steps) {using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT OR IGNORE INTO steps VALUES($id,$data)";c.Parameters.AddWithValue("$id",s.Id);c.Parameters.AddWithValue("$data",JsonSerializer.Serialize(s));c.ExecuteNonQuery();}
  tx.Commit();
  var circuits=Get<List<List<Exercise>>>("circuits")??[];
  foreach(var circuit in data.Circuits) if(!circuits.Any(x=>x.SequenceEqual(circuit))) circuits.Add(circuit);
  Set("circuits",circuits.Take(8).ToList());
  if(data.RecentTags!=null) Set("tags",data.RecentTags.Where(x=>!string.IsNullOrWhiteSpace(x)&&x.Length<=120).Take(12).ToList());
  if(data.RecentMinutes!=null) Set("minutes",data.RecentMinutes.Where(x=>x>=1&&x<=1440).Take(10).ToList());
  if(data.Draft is {Count:>0}) {Metrics.ValidateCircuit(data.Draft);Set("draft",data.Draft);}
 }
 public void Dispose()=>db.Dispose();
}
public record ExportData(int Schema,DateTimeOffset Exported,List<PracticeStep> Steps,List<List<Exercise>> Circuits, List<string>? RecentTags=null, List<int>? RecentMinutes=null, List<Exercise>? Draft=null, RunState? ActiveRun=null);
