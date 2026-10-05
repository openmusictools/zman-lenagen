using ZmanLenagen.Core;
int count=0;
void Equal<T>(T expected,T actual) {count++;if(!Equals(expected,actual))throw new Exception($"Test {count}: expected {expected}, got {actual}");}
Equal("15",Metrics.Timer(900));Equal("2",Metrics.Timer(61));Equal("1",Metrics.Timer(60));Equal("<1",Metrics.Timer(59));Equal("0",Metrics.Timer(0));Equal("−<1",Metrics.Timer(-1));Equal("−1",Metrics.Timer(-60));
var start=new DateTimeOffset(2026,10,5,23,50,0,TimeSpan.Zero);
var step=new PracticeStep{PlannedMinutes=15,Tag="test",Started=start,Segments=[new(start,start.AddMinutes(22))],Feedback="half"};
Equal(1320d,step.MeasuredSeconds);Equal(660d,step.CreditedSeconds);
var daily=Metrics.Daily([step],TimeZoneInfo.Utc);Equal(300d,daily[new DateOnly(2026,10,5)]);Equal(360d,daily[new DateOnly(2026,10,6)]);
step.Feedback="no";Equal(0d,step.CreditedSeconds);Equal(0,Metrics.Daily([step],TimeZoneInfo.Utc).Count);
step.Feedback="yes";step.Segments.Add(new(start.AddHours(1),start.AddHours(1).AddMinutes(2)));Equal(1440d,step.CreditedSeconds);
step.Feedback=null;Equal(0d,step.CreditedSeconds);
Metrics.ValidateCircuit([new("סולמות",10)]);
try{Metrics.ValidateCircuit([new("bad",0)]);throw new Exception("Invalid minutes accepted");}catch(ArgumentException){count++;}
try{Metrics.ValidateCircuit([]);throw new Exception("Empty accepted");}catch(ArgumentException){count++;}
// DST day length must preserve measured total.
var zone=TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows()?"Israel Standard Time":"Asia/Jerusalem");
step.Feedback="yes";step.Segments=[new(new DateTimeOffset(2026,10,24,20,0,0,TimeSpan.Zero),new DateTimeOffset(2026,10,25,5,0,0,TimeSpan.Zero))];
Equal(step.CreditedSeconds,Metrics.Daily([step],zone).Values.Sum());
Console.WriteLine($"Passed {count} assertions.");
