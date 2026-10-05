namespace ZmanLenagen.Core;
public record Exercise(string Tag, int Minutes);
public record Segment(DateTimeOffset Start, DateTimeOffset End);
public class PracticeStep {
 public string Id {get;set;} = Guid.NewGuid().ToString();
 public string SessionId {get;set;} = "";
 public string Tag {get;set;} = "";
 public int PlannedMinutes {get;set;}
 public DateTimeOffset Started {get;set;}
 public DateTimeOffset? Ended {get;set;}
 public string? Feedback {get;set;}
 public string Notes {get;set;} = "";
 public List<Segment> Segments {get;set;} = [];
 public double MeasuredSeconds => Segments.Sum(s=>Math.Max(0,(s.End-s.Start).TotalSeconds));
 public double CreditedSeconds => Feedback switch {"yes"=>MeasuredSeconds,"half"=>MeasuredSeconds/2,_=>0};
}
public class RunState {
 public string SessionId {get;set;} = Guid.NewGuid().ToString();
 public List<Exercise> Circuit {get;set;} = [];
 public int Index {get;set;}
 public PracticeStep? Step {get;set;}
 public bool Paused {get;set;} = true;
 public bool AwaitingFeedback {get;set;}
 public bool Alerted {get;set;}
}
public static class Metrics {
 public static string Timer(double remaining) {
  if(remaining>0 && remaining<60) return "<1";
  if(remaining>0) return Math.Ceiling(remaining/60).ToString("0");
  if(remaining==0) return "0";
  return remaining>-60 ? "−<1" : "−"+Math.Floor(-remaining/60).ToString("0");
 }
 public static Dictionary<DateOnly,double> Daily(IEnumerable<PracticeStep> steps,TimeZoneInfo zone) {
  var result=new Dictionary<DateOnly,double>();
  foreach(var step in steps) {
   var factor=step.Feedback switch {"yes"=>1.0,"half"=>0.5,_=>0.0};
   if(factor==0) continue;
   foreach(var segment in step.Segments) {
    var cursor=segment.Start;
    while(cursor<segment.End) {
     var local=TimeZoneInfo.ConvertTime(cursor,zone);
     var day=DateOnly.FromDateTime(local.DateTime);
     var midnight=DateTime.SpecifyKind(local.Date.AddDays(1),DateTimeKind.Unspecified);
     // Midnight transitions: advance to the first valid local instant.
     while(zone.IsInvalidTime(midnight)) midnight=midnight.AddMinutes(1);
     var boundary=new DateTimeOffset(midnight,zone.GetUtcOffset(midnight));
     var end=boundary<segment.End ? boundary : segment.End;
     if(end<=cursor) end=segment.End;
     result[day]=result.GetValueOrDefault(day)+(end-cursor).TotalSeconds*factor;
     cursor=end;
    }
   }
  }
  return result;
 }
 public static void ValidateCircuit(IEnumerable<Exercise> items) {
  var list=items.ToList();
  if(list.Count==0 || list.Count>100) throw new ArgumentException("בחר לפחות שלב אחד ועד 100 שלבים.");
  if(list.Any(x=>string.IsNullOrWhiteSpace(x.Tag)||x.Tag.Length>120||x.Minutes<1||x.Minutes>1440))
   throw new ArgumentException("בכל שורה יש למלא תגית (עד 120 תווים) ודקות שלמות בין 1 ל־1440.");
 }
}
