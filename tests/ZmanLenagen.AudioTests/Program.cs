using System.IO.Compression;
using System.Text.Json;
using NAudio.Wave;
using ZmanLenagen.App;
using ZmanLenagen.Core;
var folder=Path.Combine(Path.GetTempPath(),"zman-audio-tests-"+Guid.NewGuid());
int count=0;
void Check(bool condition,string name){count++;if(!condition)throw new Exception(name);}
void Wav(string path){using var writer=new WaveFileWriter(path,new WaveFormat(8000,16,1));writer.Write(new byte[16000],0,16000);writer.Flush();}
try {
 var archive=new RecordingArchive(folder);
 var step=new PracticeStep{Tag="סולמות",Feedback="half",Notes="צליל יפה"};
 var recent=new Recording{StepId=step.Id,Tag=step.Tag,Ended=DateTimeOffset.UtcNow};archive.Save(recent);Wav(archive.Wav(recent));
 var old=new Recording{Started=DateTimeOffset.UtcNow.AddDays(-8)};archive.Save(old);Wav(archive.Wav(old));
 var protectedOld=new Recording{Started=DateTimeOffset.UtcNow.AddDays(-9)};archive.Save(protectedOld);Wav(archive.Wav(protectedOld));
 archive.Cleanup(protectedOld.Id);Check(!File.Exists(archive.Wav(old)),"7-day retention");Check(File.Exists(archive.Wav(protectedOld)),"active recording protected");
 archive.Enrich(recent,[step]);var saved=archive.List().Single(x=>x.Id==recent.Id);Check(saved.Notes==step.Notes&&saved.Feedback=="half","feedback after recording");
 // Truncated metadata for one recording must not hide the rest.
 File.WriteAllText(Path.Combine(folder,Guid.NewGuid().ToString("N")+".json"),"{");Check(archive.List().Count==2,"isolated metadata corruption");
 var partial=new Recording();archive.Save(partial);Wav(archive.Partial(partial));
 archive=new RecordingArchive(folder);Check(archive.List().Any(r=>r.Id==partial.Id&&r.Recovered),"interrupted WAV recovery");
 var zip=Path.Combine(folder,"selected.zip");archive.Export(zip,[saved]);
 using(var reader=ZipFile.OpenRead(zip)){
  Check(reader.Entries.Count==2,"selected WAV and manifest");var manifest=reader.GetEntry("recordings.json")!;
  using var stream=manifest.Open();var records=JsonSerializer.Deserialize<List<Recording>>(stream)!;Check(records.Count==1&&records[0].Notes==step.Notes,"export feedback metadata");
 }
 Check(!Directory.EnumerateFiles(folder,"*.tmp").Any(),"atomic save completes");
 Console.WriteLine($"Passed {count} audio archive assertions.");
}finally{Directory.Delete(folder,true);}
