using ZmanLenagen.App;
using ZmanLenagen.Core;
var folder=Path.Combine(Path.GetTempPath(),"zman-test-"+Guid.NewGuid());int assertions=0;
void Check(bool yes){assertions++;if(!yes)throw new Exception("Persistence assertion "+assertions+" failed");}
try {
 var start=DateTimeOffset.Now;
 var step=new PracticeStep{SessionId="session",Tag="English סולמות 123",PlannedMinutes=15,Started=start,Feedback="half",Notes="תווים, \"ציטוט\"\nשורה נוספת",Segments=[new(start,start.AddMinutes(22))]};
 var run=new RunState{Circuit=[new(step.Tag,15)],Step=step};
 using(var db=new Store(folder)){db.Save(run,step);db.Set("tags",new[]{step.Tag});}
 using(var db=new Store(folder)) {
  var saved=db.Steps().Single();Check(saved.Notes==step.Notes);Check(saved.CreditedSeconds==660);Check(db.Get<RunState>("run")!.Step!.Id==step.Id);
  db.Import(new ExportData(1,start,[step],[[new("סולמות",15)]]));db.Import(new ExportData(1,start,[step],[]));Check(db.Steps().Count==1);
  var invalid=new PracticeStep{Tag="bad",PlannedMinutes=1,Feedback="invalid"};
  try{db.Import(new ExportData(1,start,[invalid],[]));throw new Exception("invalid import accepted");}catch(ArgumentException){Check(db.Steps().Count==1);}
  Check(Directory.GetFiles(Path.Combine(folder,"backups"),"*.db").Length>0);
  db.Save(null);Check(db.Get<RunState>("run")==null);
 }
 Console.WriteLine($"Passed {assertions} persistence assertions.");
} finally {Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(folder))Directory.Delete(folder,true);}
