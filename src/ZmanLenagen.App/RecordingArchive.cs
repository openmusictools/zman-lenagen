using System.IO;
using System.IO.Compression;
using System.Text.Json;
using NAudio.Wave;
using ZmanLenagen.Core;
namespace ZmanLenagen.App;
public sealed class Recording {
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 public DateTimeOffset Started {get;set;}=DateTimeOffset.UtcNow;
 public DateTimeOffset? Ended {get;set;}
 public string? StepId {get;set;}
 public string Tag {get;set;}="הקלטה חופשית";
 public string? Feedback {get;set;}
 public string Notes {get;set;}="";
 public bool Recovered {get;set;}
}
// One atomic JSON sidecar per WAV: damage to one recording never loses the index.
sealed class RecordingArchive {
 public string Folder {get;}
 static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
 public RecordingArchive(string folder){Folder=folder;Directory.CreateDirectory(folder);Recover();Cleanup();}
 public string Wav(Recording r)=>Path.Combine(Folder,r.Id+".wav");
 public string Partial(Recording r)=>Path.Combine(Folder,r.Id+".partial.wav");
 public void Save(Recording r) {
  var path=Path.Combine(Folder,r.Id+".json");var temp=path+".tmp";
  using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {JsonSerializer.Serialize(file,r,Json);file.Flush(true);}
  File.Move(temp,path,true);
 }
 public List<Recording> List() {
  var list=new List<Recording>();
  foreach(var file in Directory.EnumerateFiles(Folder,"*.json"))try {
   var r=JsonSerializer.Deserialize<Recording>(File.ReadAllText(file));
   if(r!=null&&r.Id==Path.GetFileNameWithoutExtension(file)&&Guid.TryParseExact(r.Id,"N",out _)&&File.Exists(Wav(r)))list.Add(r);
  }catch(JsonException){}catch(IOException){}
  return list.OrderByDescending(r=>r.Started).ToList();
 }
 void Recover() {
  foreach(var file in Directory.EnumerateFiles(Folder,"*.partial.wav"))try {
   var id=Path.GetFileName(file).Replace(".partial.wav","");
   var r=JsonSerializer.Deserialize<Recording>(File.ReadAllText(Path.Combine(Folder,id+".json")));
   if(r==null||r.Id!=id||!Guid.TryParseExact(id,"N",out _))continue;
   using(var reader=new WaveFileReader(file))if(reader.Length==0)continue;
   r.Ended=new DateTimeOffset(File.GetLastWriteTimeUtc(file));r.Recovered=true;Save(r);File.Move(file,Wav(r),true);
  }catch(Exception ex) when(ex is IOException or JsonException or FormatException or ArgumentException){}
 }
 public void Cleanup(string? protectedId=null) {
  var cutoff=DateTimeOffset.UtcNow.AddDays(-7);
  foreach(var file in Directory.EnumerateFiles(Folder,"*.json"))try {
   var r=JsonSerializer.Deserialize<Recording>(File.ReadAllText(file));
   if(r==null||r.Id==protectedId||!Guid.TryParseExact(r.Id,"N",out _)||r.Id!=Path.GetFileNameWithoutExtension(file)||r.Started>cutoff)continue;
   File.Delete(Wav(r));File.Delete(Partial(r));File.Delete(file);
  }catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException){}
 }
 public void Enrich(Recording r,IEnumerable<PracticeStep> steps) {
  var step=steps.FirstOrDefault(s=>s.Id==r.StepId);if(step==null)return;
  if(r.Tag!=step.Tag||r.Feedback!=step.Feedback||r.Notes!=step.Notes){r.Tag=step.Tag;r.Feedback=step.Feedback;r.Notes=step.Notes;Save(r);}
 }
 public void Export(string destination,IReadOnlyList<Recording> recordings) {
  var temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {
   using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create)) {
    foreach(var r in recordings) {
     var tag=new string(r.Tag.Where(c=>!Path.GetInvalidFileNameChars().Contains(c)&&!char.IsControl(c)).Take(50).ToArray());
     zip.CreateEntryFromFile(Wav(r),$"{r.Started.ToLocalTime():yyyy-MM-dd_HH-mm-ss}_{tag}_{r.Id[..8]}.wav",CompressionLevel.Fastest);
    }
    using var stream=zip.CreateEntry("recordings.json").Open();JsonSerializer.Serialize(stream,recordings,Json);
   }
   File.Move(temp,destination,true);
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
